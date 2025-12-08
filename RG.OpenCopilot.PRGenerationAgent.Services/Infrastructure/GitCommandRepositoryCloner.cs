using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

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
