using Moq;
using Octokit;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class GitHubServiceTests {
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
