using Octokit;
using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Git.Services;

public interface IGitHubService {
    Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default);
    Task<int> CreateWipPullRequestAsync(string owner, string repo, string branchName, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default);
    Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default);
    Task<int?> GetPullRequestNumberForBranchAsync(string owner, string repo, string branchName, CancellationToken cancellationToken = default);
    Task PostPullRequestCommentAsync(string owner, string repo, int prNumber, string comment, CancellationToken cancellationToken = default);
    Task<PullRequestInfo> GetPullRequestAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default);
}

public sealed class GitHubService : IGitHubService {
    private readonly IGitHubRepositoryAdapter _repositoryAdapter;
    private readonly IGitHubGitAdapter _gitAdapter;
    private readonly IGitHubPullRequestAdapter _pullRequestAdapter;
    private readonly IGitHubIssueAdapter _issueAdapter;
    private readonly ILogger<GitHubService> _logger;
    private readonly IAuditLogger _auditLogger;

    public GitHubService(
        IGitHubRepositoryAdapter repositoryAdapter,
        IGitHubGitAdapter gitAdapter,
        IGitHubPullRequestAdapter pullRequestAdapter,
        IGitHubIssueAdapter issueAdapter,
        ILogger<GitHubService> logger,
        IAuditLogger auditLogger) {
        _repositoryAdapter = repositoryAdapter;
        _gitAdapter = gitAdapter;
        _pullRequestAdapter = pullRequestAdapter;
        _issueAdapter = issueAdapter;
        _logger = logger;
        _auditLogger = auditLogger;
    }

    public async Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default) {
        var startTime = DateTime.UtcNow;
        var correlationId = $"branch-{owner}/{repo}/issue-{issueNumber}";

        try {
            // Get the default branch reference
            var repository = await _repositoryAdapter.GetRepositoryAsync(owner, repo, cancellationToken);
            var defaultBranchName = repository.DefaultBranch;
            
            var mainBranch = await _gitAdapter.GetReferenceAsync(owner, repo, $"heads/{defaultBranchName}", cancellationToken);
            var sha = mainBranch.Sha;

            // Create new branch name
            var branchName = $"open-copilot/issue-{issueNumber}";

            try {
                // Create the new branch from the default branch
                await _gitAdapter.CreateReferenceAsync(owner, repo, $"refs/heads/{branchName}", sha, cancellationToken);

                var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _auditLogger.LogGitHubApiCall(
                    operation: "CreateBranch",
                    correlationId: correlationId,
                    durationMs: duration,
                    success: true);

                _logger.LogInformation("Created branch {BranchName} for issue #{IssueNumber}", branchName, issueNumber);
                return branchName;
            }
            catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity &&
                                           ex.Message.Contains("Reference already exists", StringComparison.OrdinalIgnoreCase)) {
                // Branch already exists, return it
                var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                _auditLogger.LogGitHubApiCall(
                    operation: "CreateBranch",
                    correlationId: correlationId,
                    durationMs: duration,
                    success: true);

                _logger.LogInformation("Branch {BranchName} already exists for issue #{IssueNumber}", branchName, issueNumber);
                return branchName;
            }
        }
        catch (Exception ex) {
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _auditLogger.LogGitHubApiCall(
                operation: "CreateBranch",
                correlationId: correlationId,
                durationMs: duration,
                success: false,
                errorMessage: ex.Message);
            throw;
        }
    }

    public async Task<int> CreateWipPullRequestAsync(string owner, string repo, string branchName, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default) {
        var startTime = DateTime.UtcNow;
        var correlationId = $"pr-{owner}/{repo}/issue-{issueNumber}";

        try {
            var repository = await _repositoryAdapter.GetRepositoryAsync(owner, repo, cancellationToken);
            var defaultBranchName = repository.DefaultBranch;

            var body = $@"## Original Issue Prompt

**Issue #{issueNumber}: {issueTitle}**

{issueBody}

---

_This PR was automatically created by RG.OpenCopilot._
_The plan and progress will be updated here as the agent works on this issue._";

            var pr = await _pullRequestAdapter.CreateAsync(
                owner, 
                repo, 
                $"[WIP] {issueTitle}", 
                branchName, 
                defaultBranchName, 
                body, 
                cancellationToken);

            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _auditLogger.LogGitHubApiCall(
                operation: "CreatePullRequest",
                correlationId: correlationId,
                durationMs: duration,
                success: true);
            
            _logger.LogInformation("Created WIP PR #{PrNumber} for issue #{IssueNumber}", pr.Number, issueNumber);

            return pr.Number;
        }
        catch (Exception ex) {
            var duration = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            _auditLogger.LogGitHubApiCall(
                operation: "CreatePullRequest",
                correlationId: correlationId,
                durationMs: duration,
                success: false,
                errorMessage: ex.Message);
            throw;
        }
    }

    public async Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default) {
        await _pullRequestAdapter.UpdateAsync(owner, repo, prNumber, title, body, cancellationToken);
        _logger.LogInformation("Updated PR #{PrNumber} with new description", prNumber);
    }

    public async Task<int?> GetPullRequestNumberForBranchAsync(string owner, string repo, string branchName, CancellationToken cancellationToken = default) {
        var pullRequests = await _pullRequestAdapter.GetAllForRepositoryAsync(owner, repo, cancellationToken);

        var pr = pullRequests.FirstOrDefault(p => p.HeadRef == $"{owner}:{branchName}" || p.HeadRef == branchName);
        if (pr != null) {
            _logger.LogInformation("Found PR #{PrNumber} for branch {BranchName}", pr.Number, branchName);
            return pr.Number;
        }

        _logger.LogWarning("No open PR found for branch {BranchName}", branchName);
        return null;
    }

    public async Task PostPullRequestCommentAsync(string owner, string repo, int prNumber, string comment, CancellationToken cancellationToken = default) {
        await _issueAdapter.CreateCommentAsync(owner, repo, prNumber, comment, cancellationToken);
        _logger.LogInformation("Posted comment to PR #{PrNumber}", prNumber);
    }

    public async Task<PullRequestInfo> GetPullRequestAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default) {
        var pr = await _pullRequestAdapter.GetAsync(owner, repo, prNumber, cancellationToken);
        _logger.LogInformation("Retrieved PR #{PrNumber}", prNumber);
        return pr;
    }
}
