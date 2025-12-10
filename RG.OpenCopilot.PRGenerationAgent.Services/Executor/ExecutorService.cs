using System.Text;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Authentication;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Git;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Executor;

public sealed class ExecutorService : IExecutorService {
    private readonly IGitHubAppTokenProvider _tokenProvider;
    private readonly IRepositoryCloner _repositoryCloner;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IGitHubService _gitHubService;
    private readonly IAgentTaskStore _taskStore;
    private readonly IProgressReporter _progressReporter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ExecutorService> _logger;
    private readonly IAuditLogger _auditLogger;

    public ExecutorService(
        IGitHubAppTokenProvider tokenProvider,
        IRepositoryCloner repositoryCloner,
        ICommandExecutor commandExecutor,
        IGitHubService gitHubService,
        IAgentTaskStore taskStore,
        IProgressReporter progressReporter,
        TimeProvider timeProvider,
        ILogger<ExecutorService> logger,
        IAuditLogger auditLogger) {
        _tokenProvider = tokenProvider;
        _repositoryCloner = repositoryCloner;
        _commandExecutor = commandExecutor;
        _gitHubService = gitHubService;
        _taskStore = taskStore;
        _progressReporter = progressReporter;
        _timeProvider = timeProvider;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    public async Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default) {
        if (task.Plan == null) {
            throw new InvalidOperationException("Cannot execute task without a plan");
        }

        var startTime = _timeProvider.GetUtcNow().DateTime;
        var correlationId = task.Id;

        _logger.LogInformation("Starting execution of task {TaskId}", task.Id);
        _auditLogger.LogPlanExecution(taskId: task.Id, correlationId: correlationId, durationMs: null, success: true);

        task.Status = AgentTaskStatus.Executing;
        task.StartedAt = DateTime.UtcNow;
        await _taskStore.UpdateTaskAsync(task, cancellationToken);

        string? localRepoPath = null;
        try {
            // Get installation token for authentication
            var token = await _tokenProvider.GetInstallationTokenAsync(task.InstallationId, cancellationToken);

            // Determine the branch name
            var branchName = $"open-copilot/issue-{task.IssueNumber}";

            // Clone the repository
            _logger.LogInformation("Cloning repository {Owner}/{Repo} on branch {Branch}",
                task.RepositoryOwner, task.RepositoryName, branchName);
            localRepoPath = await _repositoryCloner.CloneRepositoryAsync(
                owner: task.RepositoryOwner,
                repo: task.RepositoryName,
                token: token,
                branch: branchName,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Repository cloned to {Path}", localRepoPath);

            // Execute each step in the plan
            var completedSteps = new List<string>();
            var failedSteps = new List<string>();

            foreach (var step in task.Plan.Steps.Where(s => !s.Done)) {
                _logger.LogInformation("Executing step: {StepTitle}", step.Title);

                try {
                    // TODO: In a full implementation, this would call an LLM to determine what changes to make
                    // For now, we mark steps as done to demonstrate the workflow without actual code modification
                    // This is intentional for the current phase - LLM-driven code changes will be added later
                    step.Done = true;
                    completedSteps.Add(step.Title);

                    await _taskStore.UpdateTaskAsync(task, cancellationToken);
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "Failed to execute step: {StepTitle}", step.Title);
                    failedSteps.Add(step.Title);
                }
            }

            // Commit and push changes if there are any
            var hasChanges = await HasUncommittedChangesAsync(localRepoPath, cancellationToken);
            if (hasChanges) {
                _logger.LogInformation("Committing and pushing changes");
                await CommitAndPushChangesAsync(localRepoPath, token, task, completedSteps, cancellationToken);
            }

            // Post progress comment to PR
            var prNumber = await _gitHubService.GetPullRequestNumberForBranchAsync(
                task.RepositoryOwner,
                task.RepositoryName,
                branchName,
                cancellationToken);

            if (prNumber.HasValue) {
                var progressComment = FormatProgressComment(completedSteps, failedSteps);
                await _gitHubService.PostPullRequestCommentAsync(
                    task.RepositoryOwner,
                    task.RepositoryName,
                    prNumber.Value,
                    progressComment,
                    cancellationToken);
            }

            // Update task status
            if (task.Plan.Steps.All(s => s.Done)) {
                task.Status = AgentTaskStatus.Completed;
                task.CompletedAt = _timeProvider.GetUtcNow().DateTime;
                _logger.LogInformation("Task {TaskId} completed successfully", task.Id);
                
                _auditLogger.LogPlanExecution(
                    taskId: task.Id,
                    correlationId: correlationId,
                    durationMs: (long)(_timeProvider.GetUtcNow().DateTime - startTime).TotalMilliseconds,
                    success: true);
                
                // Finalize the PR: remove [WIP], rewrite description, archive WIP details
                if (prNumber.HasValue) {
                    await _progressReporter.FinalizePullRequestAsync(task, prNumber.Value, cancellationToken);
                }
            }
            else if (failedSteps.Any()) {
                task.Status = AgentTaskStatus.Failed;
                task.CompletedAt = _timeProvider.GetUtcNow().DateTime;
                _logger.LogWarning("Task {TaskId} failed with {FailedCount} failed steps", task.Id, failedSteps.Count);

                _auditLogger.LogPlanExecution(
                    taskId: task.Id,
                    correlationId: correlationId,
                    durationMs: (long)(_timeProvider.GetUtcNow().DateTime - startTime).TotalMilliseconds,
                    success: false,
                    errorMessage: $"{failedSteps.Count} steps failed");
            }

            await _taskStore.UpdateTaskAsync(task, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error executing task {TaskId}", task.Id);
            
            _auditLogger.LogPlanExecution(
                taskId: task.Id,
                correlationId: correlationId,
                durationMs: (long)(_timeProvider.GetUtcNow().DateTime - startTime).TotalMilliseconds,
                success: false,
                errorMessage: ex.Message);

            task.Status = AgentTaskStatus.Failed;
            task.CompletedAt = _timeProvider.GetUtcNow().DateTime;
            await _taskStore.UpdateTaskAsync(task, cancellationToken);
            throw;
        }
        finally {
            // Cleanup the cloned repository
            if (localRepoPath != null) {
                try {
                    _repositoryCloner.CleanupRepository(localRepoPath);
                    _logger.LogInformation("Cleaned up repository at {Path}", localRepoPath);
                }
                catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to cleanup repository at {Path}", localRepoPath);
                }
            }
        }
    }

    private async Task<bool> HasUncommittedChangesAsync(string repoPath, CancellationToken cancellationToken) {
        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "status", "--porcelain" },
            cancellationToken: cancellationToken);

        return !string.IsNullOrWhiteSpace(result.Output);
    }

    private async Task CommitAndPushChangesAsync(
        string repoPath,
        string token,
        AgentTask task,
        List<string> completedSteps,
        CancellationToken cancellationToken) {
        // Configure git user
        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "config", "user.name", "RG.OpenCopilot[bot]" },
            cancellationToken: cancellationToken);

        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "config", "user.email", "opencopilot@users.noreply.github.com" },
            cancellationToken: cancellationToken);

        // Stage all changes
        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "add", "." },
            cancellationToken: cancellationToken);

        // Commit changes
        var commitMessage = completedSteps.Count == 1
            ? $"Implement: {completedSteps[0]}"
            : $"Implement {completedSteps.Count} changes for issue #{task.IssueNumber}";

        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "commit", "-m", commitMessage },
            cancellationToken: cancellationToken);

        // Push changes
        var branchName = $"open-copilot/issue-{task.IssueNumber}";
        var remoteUrl = $"https://x-access-token:{token}@github.com/{task.RepositoryOwner}/{task.RepositoryName}.git";

        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "remote", "set-url", "origin", remoteUrl },
            cancellationToken: cancellationToken);

        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "push", "origin", branchName },
            cancellationToken: cancellationToken);
    }

    private static string FormatProgressComment(List<string> completedSteps, List<string> failedSteps) {
        var sb = new StringBuilder();
        sb.AppendLine("## Progress Update");
        sb.AppendLine();

        if (completedSteps.Any()) {
            sb.AppendLine("### ✅ Completed Steps");
            foreach (var step in completedSteps) {
                sb.AppendLine($"- {step}");
            }
            sb.AppendLine();
        }

        if (failedSteps.Any()) {
            sb.AppendLine("### ❌ Failed Steps");
            foreach (var step in failedSteps) {
                sb.AppendLine($"- {step}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("_Automated progress update by RG.OpenCopilot_");

        return sb.ToString();
    }
}

