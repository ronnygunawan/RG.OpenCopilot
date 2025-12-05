using System.Diagnostics;
using System.Text;
using RG.OpenCopilot.Agent;

namespace RG.OpenCopilot.App;

public interface IGitHubAppTokenProvider {
    Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default);
}

public interface IRepositoryCloner {
    Task<string> CloneRepositoryAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default);
    void CleanupRepository(string localPath);
}

public interface ICommandExecutor {
    Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default);
}

public sealed class CommandResult {
    public int ExitCode { get; init; }
    public string Output { get; init; } = string.Empty;
    public string Error { get; init; } = string.Empty;
    public bool Success => ExitCode == 0;
}

public sealed class ExecutorService : IExecutorService {
    private readonly IGitHubAppTokenProvider _tokenProvider;
    private readonly IRepositoryCloner _repositoryCloner;
    private readonly ICommandExecutor _commandExecutor;
    private readonly IGitHubService _gitHubService;
    private readonly IAgentTaskStore _taskStore;
    private readonly ILogger<ExecutorService> _logger;

    public ExecutorService(
        IGitHubAppTokenProvider tokenProvider,
        IRepositoryCloner repositoryCloner,
        ICommandExecutor commandExecutor,
        IGitHubService gitHubService,
        IAgentTaskStore taskStore,
        ILogger<ExecutorService> logger) {
        _tokenProvider = tokenProvider;
        _repositoryCloner = repositoryCloner;
        _commandExecutor = commandExecutor;
        _gitHubService = gitHubService;
        _taskStore = taskStore;
        _logger = logger;
    }

    public async Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default) {
        if (task.Plan == null) {
            throw new InvalidOperationException("Cannot execute task without a plan");
        }

        _logger.LogInformation("Starting execution of task {TaskId}", task.Id);
        task.Status = AgentTaskStatus.Executing;
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
            var hasChanges = await HasUncommittedChanges(localRepoPath, cancellationToken);
            if (hasChanges) {
                _logger.LogInformation("Committing and pushing changes");
                await CommitAndPushChanges(localRepoPath, token, task, completedSteps, cancellationToken);
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

    private async Task<bool> HasUncommittedChanges(string repoPath, CancellationToken cancellationToken) {
        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: repoPath,
            command: "git",
            args: new[] { "status", "--porcelain" },
            cancellationToken: cancellationToken);

        return !string.IsNullOrWhiteSpace(result.Output);
    }

    private async Task CommitAndPushChanges(
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

/// <summary>
/// Clones repositories using Git command line
/// </summary>
public sealed class GitCommandRepositoryCloner : IRepositoryCloner {
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger<GitCommandRepositoryCloner> _logger;

    public GitCommandRepositoryCloner(ICommandExecutor commandExecutor, ILogger<GitCommandRepositoryCloner> logger) {
        _commandExecutor = commandExecutor;
        _logger = logger;
    }

    public async Task<string> CloneRepositoryAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
        // Create a temporary directory for the clone
        var tempPath = Path.Combine(Path.GetTempPath(), "opencopilot-repos", $"{owner}-{repo}-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempPath);

        try {
            var repoUrl = $"https://x-access-token:{token}@github.com/{owner}/{repo}.git";

            _logger.LogInformation("Cloning {Owner}/{Repo} to {Path}", owner, repo, tempPath);

            var result = await _commandExecutor.ExecuteCommandAsync(
                tempPath,
                "git",
                new[] { "clone", "--branch", branch, "--single-branch", repoUrl, "." },
                cancellationToken);

            if (!result.Success) {
                throw new InvalidOperationException($"Failed to clone repository: {result.Error}");
            }

            return tempPath;
        }
        catch {
            // Cleanup on failure
            CleanupRepository(tempPath);
            throw;
        }
    }

    public void CleanupRepository(string localPath) {
        if (Directory.Exists(localPath)) {
            try {
                // Safety check: ensure we're only deleting from the temporary directory
                var tempRoot = Path.Combine(Path.GetTempPath(), "opencopilot-repos");
                var normalizedPath = Path.GetFullPath(localPath);
                var normalizedTempRoot = Path.GetFullPath(tempRoot);

                if (!normalizedPath.StartsWith(normalizedTempRoot, StringComparison.OrdinalIgnoreCase)) {
                    _logger.LogError("Attempted to delete directory outside of temporary root: {Path}", localPath);
                    return;
                }

                Directory.Delete(localPath, recursive: true);
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to delete directory {Path}", localPath);
            }
        }
    }
}

/// <summary>
/// Executes commands in a subprocess
/// </summary>
public sealed class ProcessCommandExecutor : ICommandExecutor {
    private readonly ILogger<ProcessCommandExecutor> _logger;

    public ProcessCommandExecutor(ILogger<ProcessCommandExecutor> logger) {
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteCommandAsync(
        string workingDirectory,
        string command,
        string[] args,
        CancellationToken cancellationToken = default) {
        var startInfo = new ProcessStartInfo {
            FileName = command,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args) {
            startInfo.ArgumentList.Add(arg);
        }

        _logger.LogDebug("Executing: {Command} {Args} in {WorkingDirectory}",
            command, string.Join(" ", args), workingDirectory);

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => {
            if (e.Data != null) {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) => {
            if (e.Data != null) {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var result = new CommandResult {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString()
        };

        if (!result.Success) {
            _logger.LogWarning("Command failed with exit code {ExitCode}: {Error}",
                result.ExitCode, result.Error);
        }

        return result;
    }
}
