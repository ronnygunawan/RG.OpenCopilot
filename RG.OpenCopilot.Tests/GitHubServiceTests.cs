using Moq;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class GitHubServiceTests {
    [Fact]
    public async Task CreateWorkingBranchAsync_CreatesNewBranch_ReturnsBoolean() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        mockRepositoryAdapter.Setup(r => r.GetRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new RepositoryInfo { DefaultBranch = "main" });

        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        mockGitAdapter.Setup(g => g.GetReferenceAsync("owner", "repo", "heads/main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new ReferenceInfo { Sha = "abc123" });
        mockGitAdapter.Setup(g => g.CreateReferenceAsync("owner", "repo", "refs/heads/open-copilot/issue-1", "abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new ReferenceInfo { Sha = "abc123" });

        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act
        var branchName = await service.CreateWorkingBranchAsync("owner", "repo", 1);

        // Assert
        branchName.ShouldBe(expected: "open-copilot/issue-1");
        mockGitAdapter.Verify(g => g.CreateReferenceAsync("owner", "repo", "refs/heads/open-copilot/issue-1", "abc123", It.IsAny<CancellationToken>()), times: Times.Once);
    }

    [Fact]
    public async Task CreateWorkingBranchAsync_BranchExists_ReturnsExistingBranch() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        mockRepositoryAdapter.Setup(r => r.GetRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new RepositoryInfo { DefaultBranch = "main" });

        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        mockGitAdapter.Setup(g => g.GetReferenceAsync("owner", "repo", "heads/main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new ReferenceInfo { Sha = "abc123" });
        mockGitAdapter.Setup(g => g.CreateReferenceAsync("owner", "repo", "refs/heads/open-copilot/issue-1", "abc123", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Reference already exists", System.Net.HttpStatusCode.UnprocessableEntity));

        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act
        var branchName = await service.CreateWorkingBranchAsync("owner", "repo", 1);

        // Assert
        branchName.ShouldBe(expected: "open-copilot/issue-1");
    }

    [Fact]
    public async Task CreateWorkingBranchAsync_WithApiException_ThrowsException() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        mockRepositoryAdapter.Setup(r => r.GetRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Not found", System.Net.HttpStatusCode.NotFound));

        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act & Assert
        await Should.ThrowAsync<ApiException>(
            async () => await service.CreateWorkingBranchAsync("owner", "repo", 1));
    }

    [Fact]
    public async Task CreateWipPullRequestAsync_CreatesPR_ReturnsPrNumber() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        mockRepositoryAdapter.Setup(r => r.GetRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new RepositoryInfo { DefaultBranch = "main" });

        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        mockPullRequestAdapter.Setup(p => p.CreateAsync("owner", "repo", "[WIP] Test Issue", "feature-branch", "main", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new PullRequestInfo { Number = 42, HeadRef = "feature-branch" });

        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act
        var prNumber = await service.CreateWipPullRequestAsync("owner", "repo", "feature-branch", 1, "Test Issue", "Issue body");

        // Assert
        prNumber.ShouldBe(expected: 42);
        mockPullRequestAdapter.Verify(p => p.CreateAsync("owner", "repo", "[WIP] Test Issue", "feature-branch", "main", It.IsAny<string>(), It.IsAny<CancellationToken>()), times: Times.Once);
    }

    [Fact]
    public async Task CreateWipPullRequestAsync_WithApiException_ThrowsException() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        mockRepositoryAdapter.Setup(r => r.GetRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Not found", System.Net.HttpStatusCode.NotFound));

        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act & Assert
        await Should.ThrowAsync<ApiException>(
            async () => await service.CreateWipPullRequestAsync("owner", "repo", "branch", 1, "title", "body"));
    }

    [Fact]
    public async Task UpdatePullRequestDescriptionAsync_CallsAdapter() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act
        await service.UpdatePullRequestDescriptionAsync("owner", "repo", 42, "Title", "Body");

        // Assert
        mockPullRequestAdapter.Verify(p => p.UpdateAsync("owner", "repo", 42, "Title", "Body", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPullRequestNumberForBranchAsync_FindsPR_ReturnsPrNumber() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        
        var pullRequests = new List<PullRequestInfo> {
            new PullRequestInfo { Number = 1, HeadRef = "feature-1" },
            new PullRequestInfo { Number = 2, HeadRef = "feature-2" }
        };
        
        mockPullRequestAdapter.Setup(p => p.GetAllForRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: pullRequests);

        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act
        var prNumber = await service.GetPullRequestNumberForBranchAsync("owner", "repo", "feature-2");

        // Assert
        prNumber.ShouldBe(expected: 2);
    }

    [Fact]
    public async Task GetPullRequestNumberForBranchAsync_NoPRFound_ReturnsNull() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        
        var pullRequests = new List<PullRequestInfo> {
            new PullRequestInfo { Number = 1, HeadRef = "feature-1" }
        };
        
        mockPullRequestAdapter.Setup(p => p.GetAllForRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: pullRequests);

        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act
        var prNumber = await service.GetPullRequestNumberForBranchAsync("owner", "repo", "nonexistent");

        // Assert
        prNumber.ShouldBeNull();
    }

    [Fact]
    public async Task GetPullRequestNumberForBranchAsync_WithApiException_ThrowsException() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        mockPullRequestAdapter.Setup(p => p.GetAllForRepositoryAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Not found", System.Net.HttpStatusCode.NotFound));

        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act & Assert
        await Should.ThrowAsync<ApiException>(
            async () => await service.GetPullRequestNumberForBranchAsync("owner", "repo", "branch"));
    }

    [Fact]
    public async Task PostPullRequestCommentAsync_CallsAdapter() {
        // Arrange
        var mockRepositoryAdapter = new Mock<IGitHubRepositoryAdapter>();
        var mockGitAdapter = new Mock<IGitHubGitAdapter>();
        var mockPullRequestAdapter = new Mock<IGitHubPullRequestAdapter>();
        var mockIssueAdapter = new Mock<IGitHubIssueAdapter>();
        var logger = new TestLogger<GitHubService>();
        
        var service = new GitHubService(
            mockRepositoryAdapter.Object,
            mockGitAdapter.Object,
            mockPullRequestAdapter.Object,
            mockIssueAdapter.Object,
            logger);

        // Act
        await service.PostPullRequestCommentAsync("owner", "repo", 42, "comment");

        // Assert
        mockIssueAdapter.Verify(i => i.CreateCommentAsync("owner", "repo", 42, "comment", It.IsAny<CancellationToken>()), Times.Once);
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
