using Moq;
using Octokit;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

// These tests verify the adapter implementations correctly delegate to IGitHubClient
// and map Octokit types to our DTOs.

public class GitHubRepositoryAdapterTests {
    [Fact]
    public void Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubRepositoryAdapter(client: mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task GetRepositoryAsync_CallsClientGet() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoClient = new Mock<IRepositoriesClient>();
        
        mockRepoClient.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("Expected exception"));
        mockClient.Setup(c => c.Repository).Returns(value: mockRepoClient.Object);
        
        var adapter = new GitHubRepositoryAdapter(client: mockClient.Object);
        
        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => await adapter.GetRepositoryAsync(owner: "owner", repo: "repo"));
        mockRepoClient.Verify(r => r.Get("owner", "repo"), times: Times.Once);
    }
    
    [Fact]
    public async Task GetLanguagesAsync_CallsClientAndMapsToLanguageInfo() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoClient = new Mock<IRepositoriesClient>();
        
        var languages = new List<RepositoryLanguage> {
            new RepositoryLanguage("C#", 10000),
            new RepositoryLanguage("JavaScript", 5000)
        };
        
        mockRepoClient.Setup(r => r.GetAllLanguages(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(value: languages);
        mockClient.Setup(c => c.Repository).Returns(value: mockRepoClient.Object);
        
        var adapter = new GitHubRepositoryAdapter(client: mockClient.Object);
        
        // Act
        var result = await adapter.GetLanguagesAsync(owner: "owner", repo: "repo");
        
        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(expected: 2);
        result[0].Name.ShouldBe(expected: "C#");
        result[0].Bytes.ShouldBe(expected: 10000);
        result[1].Name.ShouldBe(expected: "JavaScript");
        result[1].Bytes.ShouldBe(expected: 5000);
        mockRepoClient.Verify(r => r.GetAllLanguages("owner", "repo"), times: Times.Once);
    }
    
    [Fact]
    public async Task GetContentsAsync_CallsClientGetAllContents() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoClient = new Mock<IRepositoriesClient>();
        var mockContentClient = new Mock<IRepositoryContentsClient>();
        
        mockContentClient.Setup(c => c.GetAllContents(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("Expected exception"));
        mockRepoClient.Setup(r => r.Content).Returns(value: mockContentClient.Object);
        mockClient.Setup(c => c.Repository).Returns(value: mockRepoClient.Object);
        
        var adapter = new GitHubRepositoryAdapter(client: mockClient.Object);
        
        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => await adapter.GetContentsAsync(owner: "owner", repo: "repo"));
        mockContentClient.Verify(c => c.GetAllContents("owner", "repo"), times: Times.Once);
    }
}

public class GitHubGitAdapterTests {
    [Fact]
    public void Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubGitAdapter(client: mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task GetReferenceAsync_CallsClientGet() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockGitClient = new Mock<IGitDatabaseClient>();
        var mockRefClient = new Mock<IReferencesClient>();
        
        mockRefClient.Setup(r => r.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new Exception("Expected exception"));
        mockGitClient.Setup(g => g.Reference).Returns(value: mockRefClient.Object);
        mockClient.Setup(c => c.Git).Returns(value: mockGitClient.Object);
        
        var adapter = new GitHubGitAdapter(client: mockClient.Object);
        
        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => await adapter.GetReferenceAsync(owner: "owner", repo: "repo", reference: "refs/heads/main"));
        mockRefClient.Verify(r => r.Get("owner", "repo", "refs/heads/main"), times: Times.Once);
    }
    
    [Fact]
    public async Task CreateReferenceAsync_CallsClientCreate() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockGitClient = new Mock<IGitDatabaseClient>();
        var mockRefClient = new Mock<IReferencesClient>();
        
        mockRefClient.Setup(r => r.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NewReference>())).ThrowsAsync(new Exception("Expected exception"));
        mockGitClient.Setup(g => g.Reference).Returns(value: mockRefClient.Object);
        mockClient.Setup(c => c.Git).Returns(value: mockGitClient.Object);
        
        var adapter = new GitHubGitAdapter(client: mockClient.Object);
        
        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => await adapter.CreateReferenceAsync(owner: "owner", repo: "repo", refName: "refs/heads/feature", sha: "abc123"));
        mockRefClient.Verify(r => r.Create("owner", "repo", It.Is<NewReference>(nr => 
            nr.Ref == "refs/heads/feature" && nr.Sha == "abc123")), times: Times.Once);
    }
}

public class GitHubPullRequestAdapterTests {
    [Fact]
    public void Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubPullRequestAdapter(client: mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task CreateAsync_CallsClientCreate() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockPrClient = new Mock<IPullRequestsClient>();
        
        mockPrClient.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<NewPullRequest>())).ThrowsAsync(new Exception("Expected exception"));
        mockClient.Setup(c => c.PullRequest).Returns(value: mockPrClient.Object);
        
        var adapter = new GitHubPullRequestAdapter(client: mockClient.Object);
        
        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => await adapter.CreateAsync(owner: "owner", repo: "repo", title: "Test PR", head: "feature-branch", baseRef: "main", body: "Test body"));
        mockPrClient.Verify(p => p.Create("owner", "repo", It.Is<NewPullRequest>(npr => 
            npr.Title == "Test PR" && npr.Head == "feature-branch" && npr.Base == "main" && npr.Body == "Test body")), times: Times.Once);
    }
    
    [Fact]
    public async Task UpdateAsync_CallsClientUpdate() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockPrClient = new Mock<IPullRequestsClient>();
        
        mockPrClient.Setup(pr => pr.Update(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<PullRequestUpdate>()))
            .ReturnsAsync(value: It.IsAny<PullRequest>());
        mockClient.Setup(c => c.PullRequest).Returns(value: mockPrClient.Object);
        
        var adapter = new GitHubPullRequestAdapter(client: mockClient.Object);
        
        // Act
        await adapter.UpdateAsync(owner: "owner", repo: "repo", number: 42, title: "Updated PR", body: "Updated description");
        
        // Assert
        mockPrClient.Verify(pr => pr.Update("owner", "repo", 42, It.Is<PullRequestUpdate>(u => 
            u.Title == "Updated PR" && u.Body == "Updated description")), times: Times.Once);
    }
    
    [Fact]
    public async Task GetAllForRepositoryAsync_CallsClientGetAllForRepository() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockPrClient = new Mock<IPullRequestsClient>();
        
        mockPrClient.Setup(p => p.GetAllForRepository(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PullRequestRequest>())).ThrowsAsync(new Exception("Expected exception"));
        mockClient.Setup(c => c.PullRequest).Returns(value: mockPrClient.Object);
        
        var adapter = new GitHubPullRequestAdapter(client: mockClient.Object);
        
        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => await adapter.GetAllForRepositoryAsync(owner: "owner", repo: "repo"));
        mockPrClient.Verify(p => p.GetAllForRepository("owner", "repo", It.Is<PullRequestRequest>(r => r.State == ItemStateFilter.All)), times: Times.Once);
    }
}

public class GitHubIssueAdapterTests {
    [Fact]
    public void Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubIssueAdapter(client: mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task CreateCommentAsync_CallsClientCreate() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockIssueClient = new Mock<IIssuesClient>();
        var mockCommentClient = new Mock<IIssueCommentsClient>();
        
        mockCommentClient.Setup(c => c.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(value: It.IsAny<IssueComment>());
        mockIssueClient.Setup(i => i.Comment).Returns(value: mockCommentClient.Object);
        mockClient.Setup(c => c.Issue).Returns(value: mockIssueClient.Object);
        
        var adapter = new GitHubIssueAdapter(client: mockClient.Object);
        
        // Act
        await adapter.CreateCommentAsync(owner: "owner", repo: "repo", number: 1, comment: "Test comment");
        
        // Assert
        mockCommentClient.Verify(c => c.Create("owner", "repo", 1, "Test comment"), times: Times.Once);
    }
}

