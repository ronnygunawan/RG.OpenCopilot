using Moq;
using Octokit;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

// These tests verify the adapter implementations correctly delegate to IGitHubClient
// and map Octokit types to our DTOs. Since Octokit types are difficult to mock,
// we focus on verifying the adapters call the correct methods.

public class GitHubRepositoryAdapterTests {
    [Fact]
    public async Task Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubRepositoryAdapter(mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
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
        
        mockRepoClient.Setup(r => r.GetAllLanguages("owner", "repo")).ReturnsAsync(languages);
        mockClient.Setup(c => c.Repository).Returns(mockRepoClient.Object);
        
        var adapter = new GitHubRepositoryAdapter(mockClient.Object);
        
        // Act
        var result = await adapter.GetLanguagesAsync("owner", "repo");
        
        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("C#");
        result[0].Bytes.ShouldBe(10000);
        result[1].Name.ShouldBe("JavaScript");
        result[1].Bytes.ShouldBe(5000);
    }
}

public class GitHubGitAdapterTests {
    [Fact]
    public async Task Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubGitAdapter(mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
    }
}

public class GitHubPullRequestAdapterTests {
    [Fact]
    public async Task Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubPullRequestAdapter(mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task UpdateAsync_CallsClientUpdate() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockPrClient = new Mock<IPullRequestsClient>();
        
        mockPrClient.Setup(pr => pr.Update("owner", "repo", 42, It.IsAny<PullRequestUpdate>()))
            .ReturnsAsync(It.IsAny<PullRequest>());
        mockClient.Setup(c => c.PullRequest).Returns(mockPrClient.Object);
        
        var adapter = new GitHubPullRequestAdapter(mockClient.Object);
        
        // Act
        await adapter.UpdateAsync("owner", "repo", 42, "Updated PR", "Updated description");
        
        // Assert
        mockPrClient.Verify(pr => pr.Update("owner", "repo", 42, It.Is<PullRequestUpdate>(u => 
            u.Title == "Updated PR" && u.Body == "Updated description")), Times.Once);
    }
}

public class GitHubIssueAdapterTests {
    [Fact]
    public async Task Constructor_AcceptsIGitHubClient() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        
        // Act
        var adapter = new GitHubIssueAdapter(mockClient.Object);
        
        // Assert
        adapter.ShouldNotBeNull();
    }
    
    [Fact]
    public async Task CreateCommentAsync_CallsClientCreate() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockIssueClient = new Mock<IIssuesClient>();
        var mockCommentClient = new Mock<IIssueCommentsClient>();
        
        mockCommentClient.Setup(c => c.Create("owner", "repo", 1, "Test comment"))
            .ReturnsAsync(It.IsAny<IssueComment>());
        mockIssueClient.Setup(i => i.Comment).Returns(mockCommentClient.Object);
        mockClient.Setup(c => c.Issue).Returns(mockIssueClient.Object);
        
        var adapter = new GitHubIssueAdapter(mockClient.Object);
        
        // Act
        await adapter.CreateCommentAsync("owner", "repo", 1, "Test comment");
        
        // Assert
        mockCommentClient.Verify(c => c.Create("owner", "repo", 1, "Test comment"), Times.Once);
    }
}
