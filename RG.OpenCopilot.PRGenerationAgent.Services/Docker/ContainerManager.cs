using System.Text;
using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Docker;

/// <summary>
/// Manages Docker containers for executing agent tasks in isolated environments
/// </summary>
public interface IContainerManager : IDirectoryOperations {
    Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default);
    Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default);
    Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default);
    Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default);
    Task CommitAndPushAsync(string containerId, string commitMessage, string owner, string repo, string branch, string token, CancellationToken cancellationToken = default);
    Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default);
}

public sealed class DockerContainerManager : IContainerManager {
    private readonly ICommandExecutor _commandExecutor;
    private readonly ILogger<DockerContainerManager> _logger;
    private const string WorkDir = "/workspace";

    public DockerContainerManager(ICommandExecutor commandExecutor, ILogger<DockerContainerManager> logger) {
        _commandExecutor = commandExecutor;
        _logger = logger;
    }

    public async Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
        // Create a unique container name
        var containerName = $"opencopilot-{owner}-{repo}-{Guid.NewGuid():N}".ToLowerInvariant();

        _logger.LogInformation("Creating container {ContainerName}", containerName);

        // Use a base image with git and common build tools
        // We'll use Ubuntu with git, dotnet, node, python pre-installed
        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] {
                "run",
                "-d",
                "--name", containerName,
                "-w", WorkDir,
                "mcr.microsoft.com/dotnet/sdk:10.0",
                "sleep", "infinity"
            },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to create container: {result.Error}");
        }

        var containerId = result.Output.Trim();
        _logger.LogInformation("Created container {ContainerId}", containerId);

        // Install git in the container
        await ExecuteInContainerAsync(containerId: containerId, command: "apt-get", args: new[] { "update" }, cancellationToken: cancellationToken);
        await ExecuteInContainerAsync(containerId: containerId, command: "apt-get", args: new[] { "install", "-y", "git" }, cancellationToken: cancellationToken);

        // Clone the repository inside the container
        var repoUrl = $"https://x-access-token:{token}@github.com/{owner}/{repo}.git";
        var cloneResult = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "git", "clone", "--branch", branch, "--single-branch", repoUrl, WorkDir },
            cancellationToken: cancellationToken);

        if (!cloneResult.Success) {
            await CleanupContainerAsync(containerId, cancellationToken);
            throw new InvalidOperationException($"Failed to clone repository: {cloneResult.Error}");
        }

        _logger.LogInformation("Cloned repository into container {ContainerId}", containerId);
        return containerId;
    }

    public async Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default) {
        var dockerArgs = new List<string> { "exec", "-w", WorkDir, containerId, command };
        dockerArgs.AddRange(args);

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: dockerArgs.ToArray(),
            cancellationToken: cancellationToken);

        _logger.LogDebug("Executed {Command} in container {ContainerId}: exit code {ExitCode}",
            command, containerId, result.ExitCode);

        return result;
    }

    public async Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
        var fullPath = CombineContainerPath(WorkDir, filePath.TrimStart('/'));

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "cat", fullPath },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to read file {filePath}: {result.Error}");
        }

        return result.Output;
    }

    public async Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
        var fullPath = CombineContainerPath(WorkDir, filePath.TrimStart('/'));

        // Escape single quotes in content and use printf to write the file
        var escapedContent = content.Replace("'", "'\\''");

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] {
                "exec",
                containerId,
                "sh",
                "-c",
                $"printf '%s' '{escapedContent}' > {fullPath}"
            },
            cancellationToken: cancellationToken);

        if (!result.Success) {
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
        CancellationToken cancellationToken = default) {
        // Configure git user
        await ExecuteInContainerAsync(containerId: containerId, command: "git", args: new[] { "config", "user.name", "RG.OpenCopilot[bot]" }, cancellationToken: cancellationToken);
        await ExecuteInContainerAsync(containerId: containerId, command: "git", args: new[] { "config", "user.email", "opencopilot@users.noreply.github.com" }, cancellationToken: cancellationToken);

        // Stage all changes
        await ExecuteInContainerAsync(containerId: containerId, command: "git", args: new[] { "add", "." }, cancellationToken: cancellationToken);

        // Check if there are changes to commit
        var statusResult = await ExecuteInContainerAsync(containerId: containerId, command: "git", args: new[] { "status", "--porcelain" }, cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(statusResult.Output)) {
            _logger.LogInformation("No changes to commit in container {ContainerId}", containerId);
            return;
        }

        // Commit
        var commitResult = await ExecuteInContainerAsync(containerId: containerId, command: "git", args: new[] { "commit", "-m", commitMessage }, cancellationToken: cancellationToken);
        if (!commitResult.Success) {
            throw new InvalidOperationException($"Failed to commit: {commitResult.Error}");
        }

        // Set remote URL with token
        var remoteUrl = $"https://x-access-token:{token}@github.com/{owner}/{repo}.git";
        await ExecuteInContainerAsync(containerId: containerId, command: "git", args: new[] { "remote", "set-url", "origin", remoteUrl }, cancellationToken: cancellationToken);

        // Push
        var pushResult = await ExecuteInContainerAsync(containerId: containerId, command: "git", args: new[] { "push", "origin", branch }, cancellationToken: cancellationToken);
        if (!pushResult.Success) {
            throw new InvalidOperationException($"Failed to push: {pushResult.Error}");
        }

        _logger.LogInformation("Committed and pushed changes from container {ContainerId}", containerId);
    }

    public async Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Cleaning up container {ContainerId}", containerId);

        // Stop the container
        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "stop", containerId },
            cancellationToken: cancellationToken);

        // Remove the container
        await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "rm", containerId },
            cancellationToken: cancellationToken);

        _logger.LogInformation("Cleaned up container {ContainerId}", containerId);
    }

    public async Task CreateDirectoryAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
        ValidateWorkspacePath(dirPath);
        var fullPath = CombineContainerPath(WorkDir, dirPath.TrimStart('/'));

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "mkdir", "-p", fullPath },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to create directory {dirPath}: {result.Error}");
        }

        _logger.LogInformation("Created directory {DirPath} in container {ContainerId}", dirPath, containerId);
    }

    public async Task<bool> DirectoryExistsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
        ValidateWorkspacePath(dirPath);
        var fullPath = CombineContainerPath(WorkDir, dirPath.TrimStart('/'));

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "test", "-d", fullPath },
            cancellationToken: cancellationToken);

        return result.Success;
    }

    public async Task MoveAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
        ValidateWorkspacePath(source);
        ValidateWorkspacePath(dest);

        var fullSource = CombineContainerPath(WorkDir, source.TrimStart('/'));
        var fullDest = CombineContainerPath(WorkDir, dest.TrimStart('/'));

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "mv", fullSource, fullDest },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to move {source} to {dest}: {result.Error}");
        }

        _logger.LogInformation("Moved {Source} to {Dest} in container {ContainerId}", source, dest, containerId);
    }

    public async Task CopyAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
        ValidateWorkspacePath(source);
        ValidateWorkspacePath(dest);

        var fullSource = CombineContainerPath(WorkDir, source.TrimStart('/'));
        var fullDest = CombineContainerPath(WorkDir, dest.TrimStart('/'));

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "cp", "-r", fullSource, fullDest },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to copy {source} to {dest}: {result.Error}");
        }

        _logger.LogInformation("Copied {Source} to {Dest} in container {ContainerId}", source, dest, containerId);
    }

    public async Task DeleteAsync(string containerId, string path, bool recursive = false, CancellationToken cancellationToken = default) {
        ValidateWorkspacePath(path);
        var fullPath = CombineContainerPath(WorkDir, path.TrimStart('/'));

        var args = new List<string> { "exec", containerId, "rm" };
        if (recursive) {
            args.Add("-rf");
        } else {
            args.Add("-f");
        }
        args.Add(fullPath);

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: args.ToArray(),
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to delete {path}: {result.Error}");
        }

        _logger.LogInformation("Deleted {Path} in container {ContainerId}", path, containerId);
    }

    public async Task<List<string>> ListContentsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
        ValidateWorkspacePath(dirPath);
        var fullPath = CombineContainerPath(WorkDir, dirPath.TrimStart('/'));

        var result = await _commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "ls", "-1", fullPath },
            cancellationToken: cancellationToken);

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to list contents of {dirPath}: {result.Error}");
        }

        var contents = result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrEmpty(line))
            .ToList();

        return contents;
    }

    /// <summary>
    /// Combines container paths using forward slashes regardless of host OS.
    /// Container paths are always Linux-style paths.
    /// </summary>
    private static string CombineContainerPath(string basePath, string relativePath) {
        if (string.IsNullOrEmpty(basePath)) {
            throw new ArgumentException("Base path cannot be null or empty", nameof(basePath));
        }
        if (relativePath == null) {
            throw new ArgumentNullException(nameof(relativePath));
        }
        
        // Replace backslashes with forward slashes for consistency
        var normalizedBase = basePath.Replace('\\', '/').TrimEnd('/');
        var normalizedRelative = relativePath.Replace('\\', '/').TrimStart('/');
        
        if (string.IsNullOrEmpty(normalizedRelative)) {
            return normalizedBase;
        }
        
        return normalizedBase + "/" + normalizedRelative;
    }

    private static void ValidateWorkspacePath(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            throw new InvalidOperationException("Path cannot be null or empty.");
        }

        // Remove leading slash for consistency
        var cleanPath = path.TrimStart('/');
        
        // Normalize the container path to prevent directory traversal
        // Note: WorkDir is a container path (always Linux), not a host path
        // We use forward slashes for all container paths regardless of host OS
        var normalizedPath = NormalizeContainerPath(WorkDir, cleanPath);
        
        // Ensure the path is within the workspace (must start with /workspace and either end there or continue with /)
        if (!normalizedPath.Equals(WorkDir, StringComparison.Ordinal) && 
            !normalizedPath.StartsWith(WorkDir + "/", StringComparison.Ordinal)) {
            throw new InvalidOperationException($"Path {path} is outside the workspace directory. Only paths within /workspace are allowed.");
        }
    }

    /// <summary>
    /// Normalizes a container path to prevent directory traversal attacks.
    /// Container paths are always Linux-style (forward slashes) regardless of host OS.
    /// </summary>
    private static string NormalizeContainerPath(string basePath, string relativePath) {
        if (string.IsNullOrEmpty(basePath)) {
            throw new ArgumentException("Base path cannot be null or empty", nameof(basePath));
        }
        if (relativePath == null) {
            throw new ArgumentNullException(nameof(relativePath));
        }
        
        // Replace backslashes with forward slashes for consistency
        var normalizedRelative = relativePath.Replace('\\', '/');
        
        // Combine paths using forward slashes
        var normalizedBase = basePath.TrimEnd('/');
        var trimmedRelative = normalizedRelative.TrimStart('/');
        var combined = string.IsNullOrEmpty(trimmedRelative) 
            ? normalizedBase 
            : normalizedBase + "/" + trimmedRelative;
        
        // Split into components and resolve . and ..
        var components = combined.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var resolvedComponents = new List<string>();
        
        foreach (var component in components) {
            if (component == ".") {
                // Current directory - skip
                continue;
            } else if (component == "..") {
                // Parent directory - remove last component if possible
                if (resolvedComponents.Count > 0) {
                    resolvedComponents.RemoveAt(resolvedComponents.Count - 1);
                }
                // If no components left, we're trying to go above root
                // The resulting path will be validated to ensure it's within workspace
            } else {
                resolvedComponents.Add(component);
            }
        }
        
        // Reconstruct the path
        if (resolvedComponents.Count == 0) {
            return "/";
        }
        return "/" + string.Join("/", resolvedComponents);
    }
}
