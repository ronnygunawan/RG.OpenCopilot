using Octokit;

namespace RG.OpenCopilot.App;

public interface IInstructionsLoader {
    Task<string?> LoadInstructionsAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default);
}

public sealed class InstructionsLoader : IInstructionsLoader {
    private readonly IGitHubClient _client;
    private readonly ILogger<InstructionsLoader> _logger;

    // Possible locations for instructions files
    private static readonly string[] InstructionsPaths = new[]
    {
        ".github/open-copilot/{issueNumber}.md",
        ".github/open-copilot/instructions.md",
        ".github/open-copilot/README.md"
    };

    public InstructionsLoader(IGitHubClient client, ILogger<InstructionsLoader> logger) {
        _client = client;
        _logger = logger;
    }

    public async Task<string?> LoadInstructionsAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Looking for instructions for issue #{IssueNumber} in {Owner}/{Repo}",
            issueNumber, owner, repo);

        // Try issue-specific instructions first, then fall back to general instructions
        foreach (var pathTemplate in InstructionsPaths) {
            var path = pathTemplate.Replace("{issueNumber}", issueNumber.ToString());

            try {
                var contents = await _client.Repository.Content.GetAllContents(owner: owner, name: repo, path: path);

                if (contents != null && contents.Count > 0) {
                    var content = contents[0];

                    if (!string.IsNullOrEmpty(content.Content)) {
                        _logger.LogInformation("Found instructions at {Path} for issue #{IssueNumber}",
                            path, issueNumber);

                        return content.Content;
                    }
                }
            }
            catch (NotFoundException) {
                // File doesn't exist, continue to next path
                _logger.LogDebug("Instructions not found at {Path}", path);
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "Error loading instructions from {Path}", path);
            }
        }

        _logger.LogInformation("No custom instructions found for issue #{IssueNumber}", issueNumber);
        return null;
    }
}
