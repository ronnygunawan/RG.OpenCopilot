using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Executor;

/// <summary>
/// Orchestrates complete plan step execution with LLM-driven code generation
/// </summary>
internal sealed class SmartStepExecutor : ISmartStepExecutor {
    private readonly IStepAnalyzer _stepAnalyzer;
    private readonly ICodeGenerator _codeGenerator;
    private readonly ITestGenerator _testGenerator;
    private readonly IFileEditor _fileEditor;
    private readonly IBuildVerifier _buildVerifier;
    private readonly ITestValidator _testValidator;
    private readonly ICodeQualityChecker _qualityChecker;
    private readonly IFileAnalyzer _fileAnalyzer;
    private readonly IContainerManager _containerManager;
    private readonly ILogger<SmartStepExecutor> _logger;

    public SmartStepExecutor(
        IStepAnalyzer stepAnalyzer,
        ICodeGenerator codeGenerator,
        ITestGenerator testGenerator,
        IFileEditor fileEditor,
        IBuildVerifier buildVerifier,
        ITestValidator testValidator,
        ICodeQualityChecker qualityChecker,
        IFileAnalyzer fileAnalyzer,
        IContainerManager containerManager,
        ILogger<SmartStepExecutor> logger) {
        _stepAnalyzer = stepAnalyzer;
        _codeGenerator = codeGenerator;
        _testGenerator = testGenerator;
        _fileEditor = fileEditor;
        _buildVerifier = buildVerifier;
        _testValidator = testValidator;
        _qualityChecker = qualityChecker;
        _fileAnalyzer = fileAnalyzer;
        _containerManager = containerManager;
        _logger = logger;
    }

    public async Task<StepExecutionResult> ExecuteStepAsync(
        string containerId,
        PlanStep step,
        RepositoryContext context,
        CancellationToken cancellationToken = default) {
        var stopwatch = Stopwatch.StartNew();
        var metrics = new ExecutionMetrics();
        StepActionPlan? actionPlan = null;

        try {
            _logger.LogInformation("Starting execution of step: {StepTitle}", step.Title);
            _fileEditor.ClearChanges();

            // Phase 1: Analyze the step
            var analysisStart = Stopwatch.StartNew();
            actionPlan = await _stepAnalyzer.AnalyzeStepAsync(step: step, context: context, cancellationToken: cancellationToken);
            analysisStart.Stop();
            _logger.LogInformation("Step analysis complete. Found {ActionCount} actions to perform", actionPlan.Actions.Count);

            metrics.AnalysisDuration = analysisStart.Elapsed;
            metrics.LLMCalls = 1;

            // Phase 2: Execute code generation and file operations
            var codeGenStart = Stopwatch.StartNew();
            await ExecuteCodeActionsAsync(containerId: containerId, actionPlan: actionPlan, cancellationToken: cancellationToken);
            codeGenStart.Stop();
            _logger.LogInformation("Code generation complete");

            metrics.CodeGenerationDuration = codeGenStart.Elapsed;
            metrics.LLMCalls += actionPlan.Actions.Count;

            // Phase 3: Generate tests if required
            if (actionPlan.RequiresTests && !string.IsNullOrEmpty(actionPlan.MainFile)) {
                _logger.LogInformation("Generating tests for {MainFile}", actionPlan.MainFile);
                await GenerateTestsForMainFileAsync(
                    containerId: containerId,
                    actionPlan: actionPlan,
                    context: context,
                    cancellationToken: cancellationToken);

                metrics.LLMCalls += 1;
            }

            // Phase 4: Verify build
            var buildStart = Stopwatch.StartNew();
            var buildResult = await _buildVerifier.VerifyBuildAsync(
                containerId: containerId,
                maxRetries: 3,
                cancellationToken: cancellationToken);
            buildStart.Stop();
            _logger.LogInformation("Build verification complete. Success: {Success}", buildResult.Success);

            metrics.BuildDuration = buildStart.Elapsed;
            metrics.BuildAttempts = buildResult.Attempts;
            metrics.LLMCalls += buildResult.FixesApplied.Count;

            if (!buildResult.Success) {
                return CreateFailureResult(
                    error: $"Build failed after {buildResult.Attempts} attempts: {buildResult.Output}",
                    changes: _fileEditor.GetChanges(),
                    actionPlan: actionPlan,
                    duration: stopwatch.Elapsed,
                    metrics: metrics);
            }

            // Phase 5: Validate tests
            var testStart = Stopwatch.StartNew();
            var testResult = await _testValidator.RunAndValidateTestsAsync(
                containerId: containerId,
                maxRetries: 2,
                cancellationToken: cancellationToken);
            testStart.Stop();
            _logger.LogInformation("Test validation complete. All passed: {AllPassed}", testResult.AllPassed);

            metrics.TestDuration = testStart.Elapsed;
            metrics.TestAttempts = testResult.Attempts;
            metrics.LLMCalls += testResult.FixesApplied.Count;

            if (!testResult.AllPassed) {
                return CreateFailureResult(
                    error: $"Tests failed. {testResult.FailedTests}/{testResult.TotalTests} tests failing: {testResult.Summary}",
                    changes: _fileEditor.GetChanges(),
                    actionPlan: actionPlan,
                    duration: stopwatch.Elapsed,
                    metrics: metrics);
            }

            // Phase 6: Check code quality
            _logger.LogInformation("Running code quality checks");
            var qualityResult = await _qualityChecker.CheckAndFixAsync(
                containerId: containerId,
                cancellationToken: cancellationToken);

            if (!qualityResult.Success) {
                _logger.LogWarning("Code quality checks found issues but continuing");
            }

            // Collect final metrics
            var changes = _fileEditor.GetChanges();
            metrics.FilesCreated = changes.Count(c => c.Type == ChangeType.Created);
            metrics.FilesModified = changes.Count(c => c.Type == ChangeType.Modified);
            metrics.FilesDeleted = changes.Count(c => c.Type == ChangeType.Deleted);

            stopwatch.Stop();
            _logger.LogInformation(
                "Step execution complete in {Duration}. Files: {Created} created, {Modified} modified, {Deleted} deleted",
                stopwatch.Elapsed,
                metrics.FilesCreated,
                metrics.FilesModified,
                metrics.FilesDeleted);

            return StepExecutionResult.CreateSuccess(
                changes: changes,
                buildOutput: buildResult,
                testResults: testResult,
                actionPlan: actionPlan,
                duration: stopwatch.Elapsed,
                metrics: metrics);
        }
        catch (Exception ex) {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing step: {Error}", ex.Message);
            return CreateFailureResult(
                error: ex.Message,
                changes: _fileEditor.GetChanges(),
                actionPlan: actionPlan,
                duration: stopwatch.Elapsed,
                metrics: metrics);
        }
    }

    public async Task<StepExecutionResult> ExecuteStepWithRetryAsync(
        string containerId,
        PlanStep step,
        RepositoryContext context,
        int maxRetries = 1,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Executing step with up to {MaxRetries} retries", maxRetries);

        StepExecutionResult? lastResult = null;
        for (var attempt = 0; attempt <= maxRetries; attempt++) {
            if (attempt > 0) {
                _logger.LogInformation("Retry attempt {Attempt} of {MaxRetries}", attempt, maxRetries);
                
                // Rollback previous attempt if it failed
                if (lastResult != null && !lastResult.Success) {
                    await RollbackStepAsync(
                        containerId: containerId,
                        failedResult: lastResult,
                        cancellationToken: cancellationToken);
                }
            }

            lastResult = await ExecuteStepAsync(
                containerId: containerId,
                step: step,
                context: context,
                cancellationToken: cancellationToken);

            if (lastResult.Success) {
                _logger.LogInformation("Step executed successfully on attempt {Attempt}", attempt + 1);
                return lastResult;
            }

            _logger.LogWarning("Step execution failed on attempt {Attempt}: {Error}", attempt + 1, lastResult.Error);
        }

        _logger.LogError("Step execution failed after {TotalAttempts} attempts", maxRetries + 1);
        return lastResult ?? StepExecutionResult.CreateFailure("Step execution failed with no result");
    }

    public async Task RollbackStepAsync(
        string containerId,
        StepExecutionResult failedResult,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Rolling back {ChangeCount} changes from failed step execution", failedResult.Changes.Count);

        try {
            // Reverse the changes in reverse order (LIFO)
            foreach (var change in failedResult.Changes.AsEnumerable().Reverse()) {
                try {
                    await RollbackSingleChangeAsync(containerId: containerId, change: change, cancellationToken: cancellationToken);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Error rolling back change to {Path}: {Error}", change.Path, ex.Message);
                    // Continue with other rollbacks even if one fails
                }
            }

            _fileEditor.ClearChanges();
            _logger.LogInformation("Rollback complete");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error during rollback: {Error}", ex.Message);
            throw new InvalidOperationException($"Rollback failed: {ex.Message}", ex);
        }
    }

    private async Task ExecuteCodeActionsAsync(
        string containerId,
        StepActionPlan actionPlan,
        CancellationToken cancellationToken) {
        foreach (var action in actionPlan.Actions) {
            _logger.LogInformation("Executing action: {ActionType} {FilePath}", action.Type, action.FilePath);

            switch (action.Type) {
                case ActionType.CreateFile:
                    await CreateFileFromActionAsync(
                        containerId: containerId,
                        action: action,
                        cancellationToken: cancellationToken);
                    break;

                case ActionType.ModifyFile:
                    await ModifyFileFromActionAsync(
                        containerId: containerId,
                        action: action,
                        cancellationToken: cancellationToken);
                    break;

                case ActionType.DeleteFile:
                    await _fileEditor.DeleteFileAsync(
                        containerId: containerId,
                        filePath: action.FilePath,
                        cancellationToken: cancellationToken);
                    break;

                default:
                    _logger.LogWarning("Unknown action type: {ActionType}", action.Type);
                    break;
            }
        }
    }

    private async Task CreateFileFromActionAsync(
        string containerId,
        CodeAction action,
        CancellationToken cancellationToken) {
        var content = action.Request.Content;

        // If content is empty, generate it using LLM
        if (string.IsNullOrWhiteSpace(content)) {
            _logger.LogInformation("Generating content for new file {FilePath}", action.FilePath);
            var request = new LlmCodeGenerationRequest {
                Description = action.Description,
                FilePath = action.FilePath,
                Language = DetermineLanguageFromPath(action.FilePath)
            };
            content = await _codeGenerator.GenerateCodeAsync(request: request, cancellationToken: cancellationToken);
        }

        await _fileEditor.CreateFileAsync(
            containerId: containerId,
            filePath: action.FilePath,
            content: content,
            cancellationToken: cancellationToken);
    }

    private async Task ModifyFileFromActionAsync(
        string containerId,
        CodeAction action,
        CancellationToken cancellationToken) {
        var existingContent = await _containerManager.ReadFileInContainerAsync(
            containerId: containerId,
            filePath: action.FilePath,
            cancellationToken: cancellationToken);

        var modifiedContent = action.Request.Content;

        // If modified content is empty, generate changes using LLM
        if (string.IsNullOrWhiteSpace(modifiedContent)) {
            _logger.LogInformation("Generating modifications for file {FilePath}", action.FilePath);
            var request = new LlmCodeGenerationRequest {
                Description = action.Description,
                FilePath = action.FilePath,
                Language = DetermineLanguageFromPath(action.FilePath)
            };
            modifiedContent = await _codeGenerator.GenerateCodeAsync(
                request: request,
                existingCode: existingContent,
                cancellationToken: cancellationToken);
        }

        await _fileEditor.ModifyFileAsync(
            containerId: containerId,
            filePath: action.FilePath,
            transform: _ => modifiedContent,
            cancellationToken: cancellationToken);
    }

    private async Task GenerateTestsForMainFileAsync(
        string containerId,
        StepActionPlan actionPlan,
        RepositoryContext context,
        CancellationToken cancellationToken) {
        var mainFilePath = actionPlan.MainFile!;
        var testFilePath = actionPlan.TestFile;

        var codeContent = await _containerManager.ReadFileInContainerAsync(
            containerId: containerId,
            filePath: mainFilePath,
            cancellationToken: cancellationToken);

        var testContent = await _testGenerator.GenerateTestsAsync(
            containerId: containerId,
            codeFilePath: mainFilePath,
            codeContent: codeContent,
            testFramework: context.TestFramework,
            cancellationToken: cancellationToken);

        if (!string.IsNullOrWhiteSpace(testFilePath)) {
            // Check if test file already exists
            var fileExistsResult = await _containerManager.ExecuteInContainerAsync(
                containerId: containerId,
                command: "test",
                args: ["-f", testFilePath],
                cancellationToken: cancellationToken);

            if (fileExistsResult.Success) {
                await _fileEditor.ModifyFileAsync(
                    containerId: containerId,
                    filePath: testFilePath,
                    transform: _ => testContent,
                    cancellationToken: cancellationToken);
            }
            else {
                await _fileEditor.CreateFileAsync(
                    containerId: containerId,
                    filePath: testFilePath,
                    content: testContent,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task RollbackSingleChangeAsync(
        string containerId,
        FileChange change,
        CancellationToken cancellationToken) {
        _logger.LogDebug("Rolling back {ChangeType} for {Path}", change.Type, change.Path);

        switch (change.Type) {
            case ChangeType.Created:
                // Rollback create by deleting the file
                var fileExistsResult = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "test",
                    args: ["-f", change.Path],
                    cancellationToken: cancellationToken);

                if (fileExistsResult.Success) {
                    await _containerManager.ExecuteInContainerAsync(
                        containerId: containerId,
                        command: "rm",
                        args: ["-f", change.Path],
                        cancellationToken: cancellationToken);
                }
                break;

            case ChangeType.Modified:
                // Rollback modify by restoring old content
                if (change.OldContent != null) {
                    await _containerManager.WriteFileInContainerAsync(
                        containerId: containerId,
                        filePath: change.Path,
                        content: change.OldContent,
                        cancellationToken: cancellationToken);
                }
                break;

            case ChangeType.Deleted:
                // Rollback delete by recreating the file with old content
                if (change.OldContent != null) {
                    await _containerManager.WriteFileInContainerAsync(
                        containerId: containerId,
                        filePath: change.Path,
                        content: change.OldContent,
                        cancellationToken: cancellationToken);
                }
                break;

            default:
                _logger.LogWarning("Unknown change type for rollback: {ChangeType}", change.Type);
                break;
        }
    }

    private static string DetermineLanguageFromPath(string filePath) {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rs" => "rust",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" => "c",
            ".rb" => "ruby",
            ".php" => "php",
            _ => "text"
        };
    }

    private static StepExecutionResult CreateFailureResult(
        string error,
        List<FileChange> changes,
        StepActionPlan? actionPlan,
        TimeSpan duration,
        ExecutionMetrics metrics) {
        return StepExecutionResult.CreateFailure(
            error: error,
            changes: changes,
            actionPlan: actionPlan,
            duration: duration,
            metrics: metrics);
    }
}
