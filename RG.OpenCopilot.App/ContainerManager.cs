using RG.OpenCopilot.Agent;
using System.Text;

namespace RG.OpenCopilot.App;

/// <summary>
/// Manages Docker containers for executing agent tasks in isolated environments
/// </summary>
public interface IContainerManager
{
    Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default);
    Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default);
    Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default);
    Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default);
    Task CommitAndPushAsync(string containerId, string commitMessage, string owner, string repo, string branch, string token, CancellationToken cancellationToken = default);
    Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default);
}

public sealed class DockerContainerManager : IContainerManager
{
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger<DockerContainerManager> _logger;
    private const string WorkDir = "/workspace";

    public DockerContainerManager(ICommandExecutor commandExecutor, ILogger<DockerContainerManager> logger)
    {
        _commandExecutor = commandExecutor;
        _logger = logger;
    }

    public async Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default)
    {
        // Create a unique container name
        var containerName = $"opencopilot-{owner}-{repo}-{Guid.NewGuid():N}".ToLowerInvariant();
        
        _logger.LogInformation("Creating container {ContainerName}", containerName);

        // Use a base image with git and common build tools
        // We'll use Ubuntu with git, dotnet, node, python pre-installed
        var result = await _commandExecutor.ExecuteCommandAsync(
            Directory.GetCurrentDirectory(),
            "docker",
            new[] { 
                "run", 
                "-d",
                "--name", containerName,
                "-w", WorkDir,
                "mcr.microsoft.com/dotnet/sdk:10.0",
                "sleep", "infinity"
            },
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create container: {result.Error}");
        }

        var containerId = result.Output.Trim();
        _logger.LogInformation("Created container {ContainerId}", containerId);

        // Install git in the container
        await ExecuteInContainerAsync(containerId, "apt-get", new[] { "update" }, cancellationToken);
        await ExecuteInContainerAsync(containerId, "apt-get", new[] { "install", "-y", "git" }, cancellationToken);

        // Clone the repository inside the container
        var repoUrl = $"https://x-access-token:{token}@github.com/{owner}/{repo}.git";
        var cloneResult = await _commandExecutor.ExecuteCommandAsync(
            Directory.GetCurrentDirectory(),
            "docker",
            new[] { "exec", containerId, "git", "clone", "--branch", branch, "--single-branch", repoUrl, WorkDir },
            cancellationToken);

        if (!cloneResult.Success)
        {
            await CleanupContainerAsync(containerId, cancellationToken);
            throw new InvalidOperationException($"Failed to clone repository: {cloneResult.Error}");
        }

        _logger.LogInformation("Cloned repository into container {ContainerId}", containerId);
        return containerId;
    }

    public async Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default)
    {
        var dockerArgs = new List<string> { "exec", "-w", WorkDir, containerId, command };
        dockerArgs.AddRange(args);

        var result = await _commandExecutor.ExecuteCommandAsync(
            Directory.GetCurrentDirectory(),
            "docker",
            dockerArgs.ToArray(),
            cancellationToken);

        _logger.LogDebug("Executed {Command} in container {ContainerId}: exit code {ExitCode}",
            command, containerId, result.ExitCode);

        return result;
    }

    public async Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(WorkDir, filePath.TrimStart('/'));
        
        var result = await _commandExecutor.ExecuteCommandAsync(
            Directory.GetCurrentDirectory(),
            "docker",
            new[] { "exec", containerId, "cat", fullPath },
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to read file {filePath}: {result.Error}");
        }

        return result.Output;
    }

    public async Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(WorkDir, filePath.TrimStart('/'));
        
        // Escape single quotes in content and use printf to write the file
        var escapedContent = content.Replace("'", "'\\''");
        
        var result = await _commandExecutor.ExecuteCommandAsync(
            Directory.GetCurrentDirectory(),
            "docker",
            new[] { 
                "exec", 
                containerId, 
                "sh", 
                "-c", 
                $"printf '%s' '{escapedContent}' > {fullPath}"
            },
            cancellationToken);

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to write file {filePath}: {result.Error}");
        }

        _logger.LogInformation("Wrote file {FilePath} in container {ContainerId}", filePath, containerId);
    }

    public async Task CommitAndPushAsync(
        string containerId, 
        string commitMessage, 
        string owner, 
        string repo, 
        string branch, 
        string token, 
        CancellationToken cancellationToken = default)
    {
        // Configure git user
        await ExecuteInContainerAsync(containerId, "git", new[] { "config", "user.name", "RG.OpenCopilot[bot]" }, cancellationToken);
        await ExecuteInContainerAsync(containerId, "git", new[] { "config", "user.email", "opencopilot@users.noreply.github.com" }, cancellationToken);

        // Stage all changes
        await ExecuteInContainerAsync(containerId, "git", new[] { "add", "." }, cancellationToken);

        // Check if there are changes to commit
        var statusResult = await ExecuteInContainerAsync(containerId, "git", new[] { "status", "--porcelain" }, cancellationToken);
        if (string.IsNullOrWhiteSpace(statusResult.Output))
        {
            _logger.LogInformation("No changes to commit in container {ContainerId}", containerId);
            return;
        }

        // Commit
        var commitResult = await ExecuteInContainerAsync(containerId, "git", new[] { "commit", "-m", commitMessage }, cancellationToken);
        if (!commitResult.Success)
        {
            throw new InvalidOperationException($"Failed to commit: {commitResult.Error}");
        }

        // Set remote URL with token
        var remoteUrl = $"https://x-access-token:{token}@github.com/{owner}/{repo}.git";
        await ExecuteInContainerAsync(containerId, "git", new[] { "remote", "set-url", "origin", remoteUrl }, cancellationToken);

        // Push
        var pushResult = await ExecuteInContainerAsync(containerId, "git", new[] { "push", "origin", branch }, cancellationToken);
        if (!pushResult.Success)
        {
            throw new InvalidOperationException($"Failed to push: {pushResult.Error}");
        }

        _logger.LogInformation("Committed and pushed changes from container {ContainerId}", containerId);
    }

    public async Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cleaning up container {ContainerId}", containerId);

        // Stop the container
        await _commandExecutor.ExecuteCommandAsync(
            Directory.GetCurrentDirectory(),
            "docker",
            new[] { "stop", containerId },
            cancellationToken);

        // Remove the container
        await _commandExecutor.ExecuteCommandAsync(
            Directory.GetCurrentDirectory(),
            "docker",
            new[] { "rm", containerId },
            cancellationToken);

        _logger.LogInformation("Cleaned up container {ContainerId}", containerId);
    }
}
