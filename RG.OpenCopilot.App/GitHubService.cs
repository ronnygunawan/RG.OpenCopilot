using Octokit;
using RG.OpenCopilot.Agent;

namespace RG.OpenCopilot.App;

public interface IGitHubService
{
    Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default);
    Task<int> CreateWipPullRequestAsync(string owner, string repo, string branchName, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default);
    Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default);
}

public sealed class GitHubService : IGitHubService
{
    private readonly IGitHubClient _client;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(IGitHubClient client, ILogger<GitHubService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default)
    {
        // Get the default branch reference
        var defaultBranch = await _client.Repository.Get(owner, repo);
        var mainBranch = await _client.Git.Reference.Get(owner, repo, $"heads/{defaultBranch.DefaultBranch}");
        
        // Create new branch name
        var branchName = $"open-copilot/issue-{issueNumber}";
        
        try
        {
            // Create the new branch from the default branch
            var newBranch = new NewReference($"refs/heads/{branchName}", mainBranch.Object.Sha);
            await _client.Git.Reference.Create(owner, repo, newBranch);
            
            _logger.LogInformation("Created branch {BranchName} for issue #{IssueNumber}", branchName, issueNumber);
            return branchName;
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
        {
            // Branch already exists, return it
            _logger.LogInformation("Branch {BranchName} already exists for issue #{IssueNumber}", branchName, issueNumber);
            return branchName;
        }
    }

    public async Task<int> CreateWipPullRequestAsync(string owner, string repo, string branchName, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default)
    {
        var repository = await _client.Repository.Get(owner, repo);
        
        var newPullRequest = new NewPullRequest(
            $"[WIP] {issueTitle}",
            branchName,
            repository.DefaultBranch)
        {
            Body = $@"## Original Issue Prompt

**Issue #{issueNumber}: {issueTitle}**

{issueBody}

---

_This PR was automatically created by RG.OpenCopilot._
_The plan and progress will be updated here as the agent works on this issue._"
        };

        var pr = await _client.PullRequest.Create(owner, repo, newPullRequest);
        _logger.LogInformation("Created WIP PR #{PrNumber} for issue #{IssueNumber}", pr.Number, issueNumber);
        
        return pr.Number;
    }

    public async Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default)
    {
        var update = new PullRequestUpdate
        {
            Title = title,
            Body = body
        };

        await _client.PullRequest.Update(owner, repo, prNumber, update);
        _logger.LogInformation("Updated PR #{PrNumber} with new description", prNumber);
    }
}
