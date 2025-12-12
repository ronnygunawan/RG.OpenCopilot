using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Executor;

/// <summary>
/// Service for running tests, analyzing failures, and auto-fixing with LLM assistance
/// </summary>
public sealed class TestValidator : ITestValidator {
    private readonly IContainerManager _containerManager;
    private readonly IFileEditor _fileEditor;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<TestValidator> _logger;

    // Cached regex patterns for performance
    private static readonly Regex DotnetSummaryPattern = new(@"Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)", RegexOptions.Compiled);
    private static readonly Regex DotnetPassedPattern = new(@"Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)", RegexOptions.Compiled);
    private static readonly Regex DotnetFailurePattern = new(@"Failed\s+(.+)\.([^\s.]+)\s+\[", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex JestSummaryPattern = new(@"Tests:\s+(?:(\d+)\s+failed,\s+)?(\d+)\s+passed(?:,\s+(\d+)\s+skipped)?,\s+(\d+)\s+total", RegexOptions.Compiled);
    private static readonly Regex JestFailurePattern = new(@"●\s+(.+?)\s+›\s+(.+?)$", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex PytestSummaryPattern = new(@"===\s+(?:(\d+)\s+failed(?:,\s+)?)?(?:(\d+)\s+passed(?:,\s+)?)?(?:(\d+)\s+skipped)?.*===", RegexOptions.Compiled);
    private static readonly Regex PytestFailurePattern = new(@"FAILED\s+(.+?)::(.+?)::(.*?)\s+-\s+(.+?)(?=\n(?:FAILED|===|$))", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);
    private static readonly Regex JunitSummaryPattern = new(@"Tests run:\s+(\d+),\s+Failures:\s+(\d+),\s+Errors:\s+(\d+),\s+Skipped:\s+(\d+)", RegexOptions.Compiled);
    private static readonly Regex JunitFailurePattern = new(@"(\w+)\((.+?)\)\s+Time elapsed:.*?<<<\s+(FAILURE|ERROR)!", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex LineCoveragePattern = new(@"Line.*?(\d+(?:\.\d+)?)%", RegexOptions.Compiled);
    private static readonly Regex BranchCoveragePattern = new(@"Branch.*?(\d+(?:\.\d+)?)%", RegexOptions.Compiled);

    public TestValidator(
        IContainerManager containerManager,
        IFileEditor fileEditor,
        ExecutorKernel executorKernel,
        ILogger<TestValidator> logger) {
        _containerManager = containerManager;
        _fileEditor = fileEditor;
        _kernel = executorKernel.Kernel;
        _logger = logger;
        _chatService = executorKernel.Kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<TestValidationResult> RunAndValidateTestsAsync(string containerId, int maxRetries = 2, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Starting test validation for container {ContainerId} with max {MaxRetries} retries", containerId, maxRetries);
        
        var stopwatch = Stopwatch.StartNew();
        var allFixesApplied = new List<TestFix>();
        var attempts = 0;

        for (attempts = 1; attempts <= maxRetries; attempts++) {
            _logger.LogInformation("Test run attempt {Attempt} of {MaxRetries}", attempts, maxRetries);

            var testResult = await RunTestsAsync(containerId, testFilter: null, cancellationToken);

            if (testResult.Success) {
                _logger.LogInformation("All tests passed on attempt {Attempt}", attempts);
                stopwatch.Stop();
                return new TestValidationResult {
                    AllPassed = true,
                    TotalTests = testResult.Total,
                    PassedTests = testResult.Passed,
                    FailedTests = testResult.Failed,
                    SkippedTests = testResult.Skipped,
                    Attempts = attempts,
                    RemainingFailures = [],
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed,
                    Summary = $"All {testResult.Total} tests passed in {attempts} attempt(s)"
                };
            }

            if (testResult.Failures.Count == 0) {
                _logger.LogWarning("Tests failed but no failures could be parsed");
                stopwatch.Stop();
                return new TestValidationResult {
                    AllPassed = false,
                    TotalTests = testResult.Total,
                    PassedTests = testResult.Passed,
                    FailedTests = testResult.Failed,
                    SkippedTests = testResult.Skipped,
                    Attempts = attempts,
                    RemainingFailures = [],
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed,
                    Summary = $"{testResult.Failed} test(s) failed but no failures could be parsed"
                };
            }

            _logger.LogInformation("Found {FailureCount} test failures", testResult.Failures.Count);

            // If this is the last attempt, don't generate fixes
            if (attempts >= maxRetries) {
                _logger.LogWarning("Max retries reached, returning failure");
                stopwatch.Stop();
                return new TestValidationResult {
                    AllPassed = false,
                    TotalTests = testResult.Total,
                    PassedTests = testResult.Passed,
                    FailedTests = testResult.Failed,
                    SkippedTests = testResult.Skipped,
                    Attempts = attempts,
                    RemainingFailures = testResult.Failures,
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed,
                    Summary = $"{testResult.Failed} test(s) still failing after {attempts} attempts"
                };
            }

            // Analyze failures and generate fixes using LLM
            var analyzedFailures = await AnalyzeTestFailuresAsync(testResult.Failures, cancellationToken);
            var fixes = await GenerateTestFixesAsync(analyzedFailures, cancellationToken);
            
            if (fixes.Count == 0) {
                _logger.LogWarning("No fixes could be generated for the test failures");
                stopwatch.Stop();
                return new TestValidationResult {
                    AllPassed = false,
                    TotalTests = testResult.Total,
                    PassedTests = testResult.Passed,
                    FailedTests = testResult.Failed,
                    SkippedTests = testResult.Skipped,
                    Attempts = attempts,
                    RemainingFailures = testResult.Failures,
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed,
                    Summary = $"No fixes could be generated for {testResult.Failed} failing test(s)"
                };
            }

            _logger.LogInformation("Generated {FixCount} fixes", fixes.Count);

            // Apply fixes
            await ApplyTestFixesAsync(containerId, fixes, cancellationToken);
            allFixesApplied.AddRange(fixes);

            // Add delay before retry (exponential backoff)
            if (attempts < maxRetries) {
                var delaySeconds = Math.Pow(2, attempts - 1);
                _logger.LogInformation("Waiting {Delay}s before retry", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        stopwatch.Stop();
        return new TestValidationResult {
            AllPassed = false,
            TotalTests = 0,
            PassedTests = 0,
            FailedTests = 0,
            SkippedTests = 0,
            Attempts = attempts,
            RemainingFailures = [],
            FixesApplied = allFixesApplied,
            Duration = stopwatch.Elapsed,
            Summary = "Maximum retries reached without full test success"
        };
    }

    public async Task<TestExecutionResult> RunTestsAsync(string containerId, string? testFilter = null, CancellationToken cancellationToken = default) {
        var framework = await DetectTestFrameworkAsync(containerId, cancellationToken);

        if (framework == null) {
            _logger.LogWarning("No test framework detected, cannot run tests");
            return new TestExecutionResult {
                Success = false,
                Total = 0,
                Passed = 0,
                Failed = 0,
                Skipped = 0,
                Failures = [],
                Output = "No test framework detected",
                Duration = TimeSpan.Zero
            };
        }

        _logger.LogInformation("Running tests with {Framework}", framework);

        var stopwatch = Stopwatch.StartNew();
        var result = await ExecuteTestCommandAsync(containerId, framework, testFilter, cancellationToken);
        stopwatch.Stop();

        var output = result.Output + (result.Error != null ? "\n" + result.Error : "");
        var testResult = await ParseTestResultsAsync(output, framework, cancellationToken);
        
        return new TestExecutionResult {
            Success = testResult.Success,
            Total = testResult.Total,
            Passed = testResult.Passed,
            Failed = testResult.Failed,
            Skipped = testResult.Skipped,
            Failures = testResult.Failures,
            Output = testResult.Output,
            Duration = stopwatch.Elapsed
        };
    }

    public async Task<List<TestFailure>> AnalyzeTestFailuresAsync(List<TestFailure> failures, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Analyzing {FailureCount} test failures using LLM", failures.Count);

        try {
            var prompt = BuildFailureAnalysisPrompt(failures);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(GetFailureAnalysisSystemPrompt());
            chatHistory.AddUserMessage(prompt);

            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.2,
                MaxTokens = 4000,
                ResponseFormat = "json_object"
            };

            var response = await _chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            var responseContent = response.FirstOrDefault()?.Content ?? "{}";
            _logger.LogDebug("LLM response: {Response}", responseContent);

            var analyzedFailures = ParseAnalyzedFailuresFromResponse(responseContent);
            _logger.LogInformation("Analyzed {AnalyzedCount} failures", analyzedFailures.Count);
            return analyzedFailures;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error analyzing failures with LLM");
            return failures; // Return original failures if analysis fails
        }
    }

    public async Task ApplyTestFixesAsync(string containerId, List<TestFix> fixes, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Applying {FixCount} test fixes", fixes.Count);

        foreach (var fix in fixes) {
            try {
                await _fileEditor.ModifyFileAsync(
                    containerId: containerId,
                    filePath: fix.FilePath,
                    transform: content => content.Replace(fix.OriginalCode, fix.FixedCode),
                    cancellationToken: cancellationToken);
                
                _logger.LogInformation("Applied fix to {FilePath}: {Description}", fix.FilePath, fix.Description);
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to apply fix to {FilePath}", fix.FilePath);
            }
        }
    }

    public async Task<CoverageReport?> GetCoverageAsync(string containerId, CancellationToken cancellationToken = default) {
        var framework = await DetectTestFrameworkAsync(containerId, cancellationToken);

        if (framework == null) {
            _logger.LogWarning("No test framework detected, cannot get coverage");
            return null;
        }

        _logger.LogInformation("Getting coverage for {Framework}", framework);

        var result = await ExecuteCoverageCommandAsync(containerId, framework, cancellationToken);

        if (result.ExitCode != 0) {
            _logger.LogWarning("Coverage command failed: {Error}", result.Error);
            return null;
        }

        return ParseCoverageReport(result.Output, framework);
    }

    private async Task<string?> DetectTestFrameworkAsync(string containerId, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Detecting test framework for container {ContainerId}", containerId);

        // Check for xUnit (.NET)
        var xunitCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "3", "-name", "*.csproj", "-exec", "grep", "-l", "xunit", "{}", ";" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(xunitCheck.Output)) {
            _logger.LogInformation("Detected xUnit test framework");
            return "xunit";
        }

        // Check for NUnit (.NET)
        var nunitCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "3", "-name", "*.csproj", "-exec", "grep", "-l", "nunit", "{}", ";" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(nunitCheck.Output)) {
            _logger.LogInformation("Detected NUnit test framework");
            return "nunit";
        }

        // Check for MSTest (.NET)
        var mstestCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "3", "-name", "*.csproj", "-exec", "grep", "-l", "MSTest", "{}", ";" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(mstestCheck.Output)) {
            _logger.LogInformation("Detected MSTest test framework");
            return "mstest";
        }

        // Check for Jest (JavaScript/TypeScript)
        var jestCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "2", "-name", "package.json", "-exec", "grep", "-l", "jest", "{}", ";" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(jestCheck.Output)) {
            _logger.LogInformation("Detected Jest test framework");
            return "jest";
        }

        // Check for pytest (Python)
        var pytestCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "3", "-name", "pytest.ini", "-o", "-name", "test_*.py", "-o", "-name", "*_test.py" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(pytestCheck.Output)) {
            _logger.LogInformation("Detected pytest test framework");
            return "pytest";
        }

        // Check for JUnit (Java)
        var junitCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "3", "-name", "pom.xml", "-exec", "grep", "-l", "junit", "{}", ";" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(junitCheck.Output)) {
            _logger.LogInformation("Detected JUnit test framework");
            return "junit";
        }

        _logger.LogWarning("No test framework detected");
        return null;
    }

    private async Task<CommandResult> ExecuteTestCommandAsync(string containerId, string framework, string? testFilter, CancellationToken cancellationToken) {
        return framework switch {
            "xunit" or "nunit" or "mstest" => await _containerManager.ExecuteInContainerAsync(
                containerId, 
                "dotnet", 
                testFilter != null 
                    ? new[] { "test", "--filter", testFilter }
                    : new[] { "test" }, 
                cancellationToken),
            
            "jest" => await _containerManager.ExecuteInContainerAsync(
                containerId, 
                "npm", 
                testFilter != null 
                    ? new[] { "test", "--", testFilter }
                    : new[] { "test" }, 
                cancellationToken),
            
            "pytest" => await _containerManager.ExecuteInContainerAsync(
                containerId, 
                "pytest", 
                testFilter != null 
                    ? new[] { "-v", testFilter }
                    : new[] { "-v" }, 
                cancellationToken),
            
            "junit" => await _containerManager.ExecuteInContainerAsync(
                containerId, 
                "mvn", 
                testFilter != null 
                    ? new[] { "test", $"-Dtest={testFilter}" }
                    : new[] { "test" }, 
                cancellationToken),
            
            _ => new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = $"Unsupported test framework: {framework}"
            }
        };
    }

    private async Task<CommandResult> ExecuteCoverageCommandAsync(string containerId, string framework, CancellationToken cancellationToken) {
        return framework switch {
            "xunit" or "nunit" or "mstest" => await _containerManager.ExecuteInContainerAsync(
                containerId, 
                "dotnet", 
                new[] { "test", "--collect", "Code Coverage" }, 
                cancellationToken),
            
            "jest" => await _containerManager.ExecuteInContainerAsync(
                containerId, 
                "npm", 
                new[] { "test", "--", "--coverage" }, 
                cancellationToken),
            
            "pytest" => await _containerManager.ExecuteInContainerAsync(
                containerId, 
                "pytest", 
                new[] { "--cov" }, 
                cancellationToken),
            
            _ => new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = $"Coverage not supported for framework: {framework}"
            }
        };
    }

    private async Task<TestExecutionResult> ParseTestResultsAsync(string output, string framework, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Parsing test results for {Framework}", framework);

        var result = framework switch {
            "xunit" or "nunit" or "mstest" => ParseDotnetTestResults(output),
            "jest" => ParseJestTestResults(output),
            "pytest" => ParsePytestTestResults(output),
            "junit" => ParseJUnitTestResults(output),
            _ => new TestExecutionResult {
                Success = false,
                Total = 0,
                Passed = 0,
                Failed = 0,
                Skipped = 0,
                Failures = [],
                Output = output,
                Duration = TimeSpan.Zero
            }
        };

        _logger.LogInformation("Parsed {Total} tests: {Passed} passed, {Failed} failed, {Skipped} skipped", 
            result.Total, result.Passed, result.Failed, result.Skipped);
        
        return await Task.FromResult(result);
    }

    private TestExecutionResult ParseDotnetTestResults(string output) {
        var failures = new List<TestFailure>();
        var total = 0;
        var passed = 0;
        var failed = 0;
        var skipped = 0;

        // Pattern: Failed!  - Failed:     5, Passed:    42, Skipped:     3, Total:    50
        var summaryMatch = DotnetSummaryPattern.Match(output);
        if (summaryMatch.Success) {
            failed = int.Parse(summaryMatch.Groups[1].Value);
            passed = int.Parse(summaryMatch.Groups[2].Value);
            skipped = int.Parse(summaryMatch.Groups[3].Value);
            total = int.Parse(summaryMatch.Groups[4].Value);
        }

        // Pattern: Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50
        var passedMatch = DotnetPassedPattern.Match(output);
        if (passedMatch.Success && !summaryMatch.Success) {
            passed = int.Parse(passedMatch.Groups[1].Value);
            skipped = int.Parse(passedMatch.Groups[2].Value);
            total = int.Parse(passedMatch.Groups[3].Value);
            failed = 0;
        }

        // Parse individual failures
        // Pattern: Failed TestClassName.TestMethodName [Duration]
        // Followed by error message
        var matches = DotnetFailurePattern.Matches(output);

        foreach (Match match in matches) {
            var className = match.Groups[1].Value.Trim();
            var testName = match.Groups[2].Value.Trim();
            
            // Try to extract error message (typically follows the test name)
            var errorStartIndex = match.Index + match.Length;
            var nextFailureIndex = output.IndexOf("Failed ", errorStartIndex);
            var errorEndIndex = nextFailureIndex > 0 ? nextFailureIndex : output.Length;
            var errorMessage = output.Substring(errorStartIndex, errorEndIndex - errorStartIndex).Trim();
            
            // Limit error message to first few lines
            var errorLines = errorMessage.Split('\n').Take(10).ToArray();
            errorMessage = string.Join("\n", errorLines);

            var failureType = DetermineFailureType(errorMessage);
            
            failures.Add(new TestFailure {
                TestName = testName,
                ClassName = className,
                ErrorMessage = errorMessage,
                Type = failureType
            });
        }

        return new TestExecutionResult {
            Success = failed == 0,
            Total = total,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Failures = failures,
            Output = output,
            Duration = TimeSpan.Zero
        };
    }

    private TestExecutionResult ParseJestTestResults(string output) {
        var failures = new List<TestFailure>();
        var total = 0;
        var passed = 0;
        var failed = 0;
        var skipped = 0;

        // Pattern: Tests:       3 failed, 47 passed, 50 total
        var summaryMatch = JestSummaryPattern.Match(output);
        if (summaryMatch.Success) {
            failed = summaryMatch.Groups[1].Success ? int.Parse(summaryMatch.Groups[1].Value) : 0;
            passed = int.Parse(summaryMatch.Groups[2].Value);
            skipped = summaryMatch.Groups[3].Success ? int.Parse(summaryMatch.Groups[3].Value) : 0;
            total = int.Parse(summaryMatch.Groups[4].Value);
        }

        // Parse individual failures
        // Pattern: ● TestSuite › TestName
        var matches = JestFailurePattern.Matches(output);

        foreach (Match match in matches) {
            var className = match.Groups[1].Value.Trim();
            var testName = match.Groups[2].Value.Trim();
            
            var errorStartIndex = match.Index + match.Length;
            var nextFailureIndex = output.IndexOf("●", errorStartIndex);
            var errorEndIndex = nextFailureIndex > 0 ? nextFailureIndex : output.Length;
            var errorMessage = output.Substring(errorStartIndex, Math.Min(errorEndIndex - errorStartIndex, 1000)).Trim();

            var failureType = DetermineFailureType(errorMessage);
            
            failures.Add(new TestFailure {
                TestName = testName,
                ClassName = className,
                ErrorMessage = errorMessage,
                Type = failureType
            });
        }

        return new TestExecutionResult {
            Success = failed == 0,
            Total = total,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Failures = failures,
            Output = output,
            Duration = TimeSpan.Zero
        };
    }

    private TestExecutionResult ParsePytestTestResults(string output) {
        var failures = new List<TestFailure>();
        var total = 0;
        var passed = 0;
        var failed = 0;
        var skipped = 0;

        // Pattern: === 3 failed, 47 passed in 2.50s ===
        var summaryMatch = PytestSummaryPattern.Match(output);
        if (summaryMatch.Success) {
            failed = summaryMatch.Groups[1].Success ? int.Parse(summaryMatch.Groups[1].Value) : 0;
            passed = summaryMatch.Groups[2].Success ? int.Parse(summaryMatch.Groups[2].Value) : 0;
            skipped = summaryMatch.Groups[3].Success ? int.Parse(summaryMatch.Groups[3].Value) : 0;
            total = failed + passed + skipped;
        }

        // Parse individual failures
        // Pattern: FAILED test_module.py::TestClass::test_method - AssertionError: ...
        var matches = PytestFailurePattern.Matches(output);

        foreach (Match match in matches) {
            var module = match.Groups[1].Value.Trim();
            var className = match.Groups[2].Value.Trim();
            var testName = match.Groups[3].Value.Trim();
            var errorMessage = match.Groups[4].Value.Trim();

            var failureType = DetermineFailureType(errorMessage);
            
            failures.Add(new TestFailure {
                TestName = testName,
                ClassName = $"{module}.{className}",
                ErrorMessage = errorMessage,
                Type = failureType
            });
        }

        return new TestExecutionResult {
            Success = failed == 0,
            Total = total,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Failures = failures,
            Output = output,
            Duration = TimeSpan.Zero
        };
    }

    private TestExecutionResult ParseJUnitTestResults(string output) {
        var failures = new List<TestFailure>();
        var total = 0;
        var passed = 0;
        var failed = 0;
        var skipped = 0;

        // Pattern: Tests run: 50, Failures: 3, Errors: 0, Skipped: 2
        var summaryMatch = JunitSummaryPattern.Match(output);
        if (summaryMatch.Success) {
            total = int.Parse(summaryMatch.Groups[1].Value);
            failed = int.Parse(summaryMatch.Groups[2].Value) + int.Parse(summaryMatch.Groups[3].Value);
            skipped = int.Parse(summaryMatch.Groups[4].Value);
            passed = total - failed - skipped;
        }

        // Parse individual failures
        // Pattern: testMethod(com.example.TestClass)  Time elapsed: 0.05 s  <<< FAILURE!
        var matches = JunitFailurePattern.Matches(output);

        foreach (Match match in matches) {
            var testName = match.Groups[1].Value.Trim();
            var className = match.Groups[2].Value.Trim();
            
            var errorStartIndex = match.Index + match.Length;
            var nextFailureIndex = output.IndexOf("<<<", errorStartIndex);
            var errorEndIndex = nextFailureIndex > 0 ? nextFailureIndex : output.Length;
            var errorMessage = output.Substring(errorStartIndex, Math.Min(errorEndIndex - errorStartIndex, 1000)).Trim();

            var failureType = DetermineFailureType(errorMessage);
            
            failures.Add(new TestFailure {
                TestName = testName,
                ClassName = className,
                ErrorMessage = errorMessage,
                Type = failureType
            });
        }

        return new TestExecutionResult {
            Success = failed == 0,
            Total = total,
            Passed = passed,
            Failed = failed,
            Skipped = skipped,
            Failures = failures,
            Output = output,
            Duration = TimeSpan.Zero
        };
    }

    private static FailureType DetermineFailureType(string errorMessage) {
        var lowerMessage = errorMessage.ToLowerInvariant();

        if (lowerMessage.Contains("timeout") || lowerMessage.Contains("timed out")) {
            return FailureType.Timeout;
        }

        if (lowerMessage.Contains("setup") || lowerMessage.Contains("beforeeach") || lowerMessage.Contains("beforeall")) {
            return FailureType.Setup;
        }

        if (lowerMessage.Contains("teardown") || lowerMessage.Contains("aftereach") || lowerMessage.Contains("afterall")) {
            return FailureType.Teardown;
        }

        if (lowerMessage.Contains("assert") || lowerMessage.Contains("expected") || lowerMessage.Contains("should")) {
            return FailureType.Assertion;
        }

        return FailureType.Exception;
    }

    private CoverageReport? ParseCoverageReport(string output, string framework) {
        // This is a simplified implementation
        // Real coverage parsing would be more complex and framework-specific
        
        var lineCoverageMatch = LineCoveragePattern.Match(output);
        var branchCoverageMatch = BranchCoveragePattern.Match(output);

        if (!lineCoverageMatch.Success && !branchCoverageMatch.Success) {
            return null;
        }

        var lineCoverage = lineCoverageMatch.Success ? double.Parse(lineCoverageMatch.Groups[1].Value) : 0.0;
        var branchCoverage = branchCoverageMatch.Success ? double.Parse(branchCoverageMatch.Groups[1].Value) : 0.0;

        return new CoverageReport {
            LineCoverage = lineCoverage,
            BranchCoverage = branchCoverage,
            TotalLines = 0,
            CoveredLines = 0,
            TotalBranches = 0,
            CoveredBranches = 0,
            Summary = $"Line Coverage: {lineCoverage:F2}%, Branch Coverage: {branchCoverage:F2}%"
        };
    }

    private static string GetFailureAnalysisSystemPrompt() {
        return """
            You are an expert software testing specialist and debugger.
            Your role is to analyze test failures and provide detailed insights.

            You must respond with a JSON object that strictly follows this schema:

            {
              "failures": [
                {
                  "testName": "name of the test",
                  "className": "class or module name",
                  "errorMessage": "analyzed and clarified error message",
                  "type": "Assertion|Exception|Timeout|Setup|Teardown",
                  "expectedValue": "expected value if applicable",
                  "actualValue": "actual value if applicable",
                  "rootCause": "identified root cause of the failure"
                }
              ]
            }

            Guidelines:
            - Analyze each test failure to understand the root cause
            - Extract expected and actual values when dealing with assertions
            - Identify whether the failure is in the test itself or the code being tested
            - Classify failures accurately (Assertion, Exception, Timeout, Setup, Teardown)
            - Provide clear, actionable insights
            """;
    }

    private static string BuildFailureAnalysisPrompt(List<TestFailure> failures) {
        var sb = new StringBuilder();
        sb.AppendLine("I have the following test failures that need to be analyzed:");
        sb.AppendLine();

        foreach (var failure in failures.Take(10)) {
            sb.AppendLine($"Test: {failure.ClassName}.{failure.TestName}");
            sb.AppendLine($"  Type: {failure.Type}");
            sb.AppendLine($"  Error: {failure.ErrorMessage}");
            if (failure.StackTrace != null) {
                sb.AppendLine($"  Stack Trace: {failure.StackTrace}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Please analyze these test failures and provide detailed insights in JSON format.");
        return sb.ToString();
    }

    private List<TestFailure> ParseAnalyzedFailuresFromResponse(string jsonResponse) {
        try {
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            var response = JsonSerializer.Deserialize<AnalysisResponse>(jsonResponse, options);
            return response?.Failures ?? [];
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing analyzed failures from LLM response");
            return [];
        }
    }

    private async Task<List<TestFix>> GenerateTestFixesAsync(List<TestFailure> failures, CancellationToken cancellationToken) {
        _logger.LogInformation("Generating fixes for {FailureCount} test failures using LLM", failures.Count);

        try {
            var prompt = BuildFixGenerationPrompt(failures);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(GetFixGenerationSystemPrompt());
            chatHistory.AddUserMessage(prompt);

            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.2,
                MaxTokens = 4000,
                ResponseFormat = "json_object"
            };

            var response = await _chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            var responseContent = response.FirstOrDefault()?.Content ?? "{}";
            _logger.LogDebug("LLM response: {Response}", responseContent);

            var fixes = ParseFixesFromResponse(responseContent);
            _logger.LogInformation("Generated {FixCount} fixes from LLM", fixes.Count);
            return fixes;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating fixes with LLM");
            return [];
        }
    }

    private static string GetFixGenerationSystemPrompt() {
        return """
            You are an expert software developer specializing in debugging and fixing test failures.
            Your role is to analyze test failures and generate precise fixes.

            You must respond with a JSON object that strictly follows this schema:

            {
              "fixes": [
                {
                  "target": "Code|Test",
                  "filePath": "path/to/file",
                  "description": "Brief description of what the fix does",
                  "originalCode": "exact code snippet that needs fixing",
                  "fixedCode": "corrected code snippet"
                }
              ]
            }

            Guidelines:
            - Determine if the fix should be in the test code or the production code
            - Only generate fixes you are confident about
            - Provide exact code snippets, not approximations
            - Include enough context in code snippets to uniquely identify the location
            - If the test is wrong, fix the test (target: "Test")
            - If the code is wrong, fix the code (target: "Code")
            - If you cannot confidently fix a failure, omit it from the response
            """;
    }

    private static string BuildFixGenerationPrompt(List<TestFailure> failures) {
        var sb = new StringBuilder();
        sb.AppendLine("I have the following test failures that need to be fixed:");
        sb.AppendLine();

        foreach (var failure in failures.Take(10)) {
            sb.AppendLine($"Test: {failure.ClassName}.{failure.TestName}");
            sb.AppendLine($"  Type: {failure.Type}");
            sb.AppendLine($"  Error: {failure.ErrorMessage}");
            if (failure.ExpectedValue != null) {
                sb.AppendLine($"  Expected: {failure.ExpectedValue}");
            }
            if (failure.ActualValue != null) {
                sb.AppendLine($"  Actual: {failure.ActualValue}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Please generate fixes for these test failures in JSON format.");
        return sb.ToString();
    }

    private List<TestFix> ParseFixesFromResponse(string jsonResponse) {
        try {
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            var response = JsonSerializer.Deserialize<FixResponse>(jsonResponse, options);
            return response?.Fixes ?? [];
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing fixes from LLM response");
            return [];
        }
    }

    private sealed class AnalysisResponse {
        public List<TestFailure> Failures { get; set; } = [];
    }

    private sealed class FixResponse {
        public List<TestFix> Fixes { get; set; } = [];
    }
}
