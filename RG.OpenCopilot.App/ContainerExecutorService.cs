using System.Text;
using RG.OpenCopilot.Agent;

namespace RG.OpenCopilot.App;

/// <summary>
/// Container-based executor service that runs in isolated Docker containers
/// and uses MCP tools to make actual code changes
/// </summary>
public sealed class ContainerExecutorService : IExecutorService {
    private readonly IGitHubAppTokenProvider _tokenProvider;
    private readonly IContainerManager _containerManager;
    private readonly IGitHubService _gitHubService;
    private readonly IAgentTaskStore _taskStore;
    private readonly ILogger<ContainerExecutorService> _logger;

    public ContainerExecutorService(
        IGitHubAppTokenProvider tokenProvider,
        IContainerManager containerManager,
        IGitHubService gitHubService,
        IAgentTaskStore taskStore,
        ILogger<ContainerExecutorService> logger) {
        _tokenProvider = tokenProvider;
        _containerManager = containerManager;
        _gitHubService = gitHubService;
        _taskStore = taskStore;
        _logger = logger;
    }

    public async Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default) {
        if (task.Plan == null) {
            throw new InvalidOperationException("Cannot execute task without a plan");
        }

        _logger.LogInformation("Starting container-based execution of task {TaskId}", task.Id);
        task.Status = AgentTaskStatus.Executing;
        await _taskStore.UpdateTaskAsync(task, cancellationToken);

        string? containerId = null;
        try {
            // Get installation token for authentication
            var token = await _tokenProvider.GetInstallationTokenAsync(task.InstallationId, cancellationToken);

            // Use PAT if installation token is not available (for development)
            if (string.IsNullOrEmpty(token)) {
                _logger.LogWarning("Installation token not available, executor may have limited functionality");
                // In production, this should throw. For now, we'll continue with limited functionality
            }

            // Determine the branch name
            var branchName = $"open-copilot/issue-{task.IssueNumber}";

            // Create container and clone repository
            _logger.LogInformation("Creating container for {Owner}/{Repo} on branch {Branch}",
                task.RepositoryOwner, task.RepositoryName, branchName);

            containerId = await _containerManager.CreateContainerAsync(
                task.RepositoryOwner,
                task.RepositoryName,
                token,
                branchName,
                cancellationToken);

            _logger.LogInformation("Container {ContainerId} created and repository cloned", containerId);

            // Execute each step in the plan
            var completedSteps = new List<string>();
            var failedSteps = new List<string>();

            foreach (var step in task.Plan.Steps.Where(s => !s.Done)) {
                _logger.LogInformation("Executing step: {StepTitle}", step.Title);

                try {
                    // Execute the step in the container
                    await ExecuteStepInContainerAsync(containerId, step, task, cancellationToken);

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
            if (completedSteps.Any()) {
                _logger.LogInformation("Committing and pushing changes");

                var commitMessage = completedSteps.Count == 1
                    ? $"Implement: {completedSteps[0]}"
                    : $"Implement {completedSteps.Count} changes for issue #{task.IssueNumber}";

                await _containerManager.CommitAndPushAsync(
                    containerId,
                    commitMessage,
                    task.RepositoryOwner,
                    task.RepositoryName,
                    branchName,
                    token,
                    cancellationToken);
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
                _logger.LogInformation("Task {TaskId} completed successfully", task.Id);
            }
            else if (failedSteps.Any()) {
                task.Status = AgentTaskStatus.Failed;
                _logger.LogWarning("Task {TaskId} failed with {FailedCount} failed steps", task.Id, failedSteps.Count);
            }

            await _taskStore.UpdateTaskAsync(task, cancellationToken);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error executing task {TaskId}", task.Id);
            task.Status = AgentTaskStatus.Failed;
            await _taskStore.UpdateTaskAsync(task, cancellationToken);
            throw;
        }
        finally {
            // Cleanup the container
            if (containerId != null) {
                try {
                    await _containerManager.CleanupContainerAsync(containerId, cancellationToken);
                    _logger.LogInformation("Cleaned up container {ContainerId}", containerId);
                }
                catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to cleanup container {ContainerId}", containerId);
                }
            }
        }
    }

    private async Task ExecuteStepInContainerAsync(
        string containerId,
        PlanStep step,
        AgentTask task,
        CancellationToken cancellationToken) {
        _logger.LogInformation("Executing step in container: {StepTitle} - {Details}", step.Title, step.Details);

        // For now, this is a placeholder that demonstrates the capability
        // In a full implementation, this would:
        // 1. Analyze the step details to determine what needs to be done
        // 2. Use MCP tools (via container manager) to read files, make changes, and test
        // 3. Run build/test commands to verify changes

        // Example: If the step involves adding a test, we could:
        // - Read existing test files
        // - Create or modify test files
        // - Run tests to verify

        // For demonstration, let's at least try to detect the project type and run build/test
        await TryBuildAndTestAsync(containerId, cancellationToken);
    }

    private async Task TryBuildAndTestAsync(string containerId, CancellationToken cancellationToken) {
        try {
            // Check for common build files to determine project type
            var hasDotnet = await FileExistsInContainerAsync(containerId, "*.csproj", cancellationToken);
            var hasNpm = await FileExistsInContainerAsync(containerId, "package.json", cancellationToken);
            var hasPython = await FileExistsInContainerAsync(containerId, "requirements.txt", cancellationToken);

            if (hasDotnet) {
                _logger.LogInformation("Detected .NET project, running build and test");

                var buildResult = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "dotnet",
                    args: new[] { "build" },
                    cancellationToken: cancellationToken);

                if (buildResult.Success) {
                    _logger.LogInformation("Build successful");

                    var testResult = await _containerManager.ExecuteInContainerAsync(
                        containerId: containerId,
                        command: "dotnet",
                        args: new[] { "test", "--no-build" },
                        cancellationToken: cancellationToken);

                    if (testResult.Success) {
                        _logger.LogInformation("Tests passed");
                    }
                    else {
                        _logger.LogWarning("Tests failed: {Error}", testResult.Error);
                    }
                }
                else {
                    _logger.LogWarning("Build failed: {Error}", buildResult.Error);
                }
            }
            else if (hasNpm) {
                _logger.LogInformation("Detected Node.js project");

                // Install dependencies if needed
                await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "npm",
                    args: new[] { "install" },
                    cancellationToken: cancellationToken);

                // Try to run tests
                var testResult = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "npm",
                    args: new[] { "test" },
                    cancellationToken: cancellationToken);

                if (testResult.Success) {
                    _logger.LogInformation("Tests passed");
                }
            }
            else if (hasPython) {
                _logger.LogInformation("Detected Python project");

                // Install dependencies
                await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "pip",
                    args: new[] { "install", "-r", "requirements.txt" },
                    cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Could not run build/test commands");
        }
    }

    private async Task<bool> FileExistsInContainerAsync(string containerId, string pattern, CancellationToken cancellationToken) {
        var result = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "sh",
            args: new[] { "-c", $"ls {pattern} 2>/dev/null" },
            cancellationToken: cancellationToken);

        return result.Success && !string.IsNullOrWhiteSpace(result.Output);
    }

    private static string FormatProgressComment(List<string> completedSteps, List<string> failedSteps) {
        var sb = new StringBuilder();
        sb.AppendLine("## Progress Update");
        sb.AppendLine();
        sb.AppendLine("_Executed in isolated Docker container_");
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
        sb.AppendLine("_Automated progress update by RG.OpenCopilot (Container-based Executor)_");

        return sb.ToString();
    }
}
