using Moq;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class InstructionsLoaderTests {
    [Fact]
    public async Task LoadInstructionsAsync_WithIssueSpecificFile_ReturnsContent() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoContent = new Mock<IRepositoryContentsClient>();
        
        mockClient.Setup(c => c.Repository.Content).Returns(mockRepoContent.Object);
        
        var content = new RepositoryContent(
            name: "42.md",
            path: ".github/open-copilot/42.md",
            sha: "sha1",
            size: 100,
            type: ContentType.File,
            downloadUrl: "https://example.com/download",
            url: "https://api.github.com/repos/owner/repo/contents/.github/open-copilot/42.md",
            gitUrl: "https://api.github.com/repos/owner/repo/git/blobs/sha1",
            htmlUrl: "https://github.com/owner/repo/blob/main/.github/open-copilot/42.md",
            encoding: "base64",
            encodedContent: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("Issue-specific instructions")),
            target: null,
            submoduleGitUrl: null);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/42.md"))
            .ReturnsAsync(new[] { content });
        
        var logger = new TestLogger<InstructionsLoader>();
        var loader = new InstructionsLoader(mockClient.Object, logger);

        // Act
        var instructions = await loader.LoadInstructionsAsync(owner: "owner", repo: "repo", issueNumber: 42);

        // Assert
        instructions.ShouldBe("Issue-specific instructions");
    }

    [Fact]
    public async Task LoadInstructionsAsync_WithGeneralInstructions_ReturnsContent() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoContent = new Mock<IRepositoryContentsClient>();
        
        mockClient.Setup(c => c.Repository.Content).Returns(mockRepoContent.Object);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/42.md"))
            .ThrowsAsync(new NotFoundException(message: "Not found", statusCode: System.Net.HttpStatusCode.NotFound));
        
        var content = new RepositoryContent(
            name: "instructions.md",
            path: ".github/open-copilot/instructions.md",
            sha: "sha2",
            size: 100,
            type: ContentType.File,
            downloadUrl: "https://example.com/download",
            url: "https://api.github.com/repos/owner/repo/contents/.github/open-copilot/instructions.md",
            gitUrl: "https://api.github.com/repos/owner/repo/git/blobs/sha2",
            htmlUrl: "https://github.com/owner/repo/blob/main/.github/open-copilot/instructions.md",
            encoding: "base64",
            encodedContent: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("General instructions")),
            target: null,
            submoduleGitUrl: null);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/instructions.md"))
            .ReturnsAsync(new[] { content });
        
        var logger = new TestLogger<InstructionsLoader>();
        var loader = new InstructionsLoader(mockClient.Object, logger);

        // Act
        var instructions = await loader.LoadInstructionsAsync(owner: "owner", repo: "repo", issueNumber: 42);

        // Assert
        instructions.ShouldBe("General instructions");
    }

    [Fact]
    public async Task LoadInstructionsAsync_WithReadmeFallback_ReturnsContent() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoContent = new Mock<IRepositoryContentsClient>();
        
        mockClient.Setup(c => c.Repository.Content).Returns(mockRepoContent.Object);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/42.md"))
            .ThrowsAsync(new NotFoundException(message: "Not found", statusCode: System.Net.HttpStatusCode.NotFound));
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/instructions.md"))
            .ThrowsAsync(new NotFoundException(message: "Not found", statusCode: System.Net.HttpStatusCode.NotFound));
        
        var content = new RepositoryContent(
            name: "README.md",
            path: ".github/open-copilot/README.md",
            sha: "sha3",
            size: 100,
            type: ContentType.File,
            downloadUrl: "https://example.com/download",
            url: "https://api.github.com/repos/owner/repo/contents/.github/open-copilot/README.md",
            gitUrl: "https://api.github.com/repos/owner/repo/git/blobs/sha3",
            htmlUrl: "https://github.com/owner/repo/blob/main/.github/open-copilot/README.md",
            encoding: "base64",
            encodedContent: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("README instructions")),
            target: null,
            submoduleGitUrl: null);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/README.md"))
            .ReturnsAsync(new[] { content });
        
        var logger = new TestLogger<InstructionsLoader>();
        var loader = new InstructionsLoader(mockClient.Object, logger);

        // Act
        var instructions = await loader.LoadInstructionsAsync(owner: "owner", repo: "repo", issueNumber: 42);

        // Assert
        instructions.ShouldBe("README instructions");
    }

    [Fact]
    public async Task LoadInstructionsAsync_WithNoInstructionsFound_ReturnsNull() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoContent = new Mock<IRepositoryContentsClient>();
        
        mockClient.Setup(c => c.Repository.Content).Returns(mockRepoContent.Object);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", It.IsAny<string>()))
            .ThrowsAsync(new NotFoundException(message: "Not found", statusCode: System.Net.HttpStatusCode.NotFound));
        
        var logger = new TestLogger<InstructionsLoader>();
        var loader = new InstructionsLoader(mockClient.Object, logger);

        // Act
        var instructions = await loader.LoadInstructionsAsync(owner: "owner", repo: "repo", issueNumber: 42);

        // Assert
        instructions.ShouldBeNull();
    }

    [Fact]
    public async Task LoadInstructionsAsync_WithEmptyContent_ReturnsNull() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoContent = new Mock<IRepositoryContentsClient>();
        
        mockClient.Setup(c => c.Repository.Content).Returns(mockRepoContent.Object);
        
        var content = new RepositoryContent(
            name: "42.md",
            path: ".github/open-copilot/42.md",
            sha: "sha1",
            size: 0,
            type: ContentType.File,
            downloadUrl: "https://example.com/download",
            url: "https://api.github.com/repos/owner/repo/contents/.github/open-copilot/42.md",
            gitUrl: "https://api.github.com/repos/owner/repo/git/blobs/sha1",
            htmlUrl: "https://github.com/owner/repo/blob/main/.github/open-copilot/42.md",
            encoding: "base64",
            encodedContent: "",
            target: null,
            submoduleGitUrl: null);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/42.md"))
            .ReturnsAsync(new[] { content });
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/instructions.md"))
            .ThrowsAsync(new NotFoundException(message: "Not found", statusCode: System.Net.HttpStatusCode.NotFound));
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/README.md"))
            .ThrowsAsync(new NotFoundException(message: "Not found", statusCode: System.Net.HttpStatusCode.NotFound));
        
        var logger = new TestLogger<InstructionsLoader>();
        var loader = new InstructionsLoader(mockClient.Object, logger);

        // Act
        var instructions = await loader.LoadInstructionsAsync(owner: "owner", repo: "repo", issueNumber: 42);

        // Assert
        instructions.ShouldBeNull();
    }

    [Fact]
    public async Task LoadInstructionsAsync_WithApiError_ContinuesToNextPath() {
        // Arrange
        var mockClient = new Mock<IGitHubClient>();
        var mockRepoContent = new Mock<IRepositoryContentsClient>();
        
        mockClient.Setup(c => c.Repository.Content).Returns(mockRepoContent.Object);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/42.md"))
            .ThrowsAsync(new ApiException(message: "API error", httpStatusCode: System.Net.HttpStatusCode.InternalServerError));
        
        var content = new RepositoryContent(
            name: "instructions.md",
            path: ".github/open-copilot/instructions.md",
            sha: "sha2",
            size: 100,
            type: ContentType.File,
            downloadUrl: "https://example.com/download",
            url: "https://api.github.com/repos/owner/repo/contents/.github/open-copilot/instructions.md",
            gitUrl: "https://api.github.com/repos/owner/repo/git/blobs/sha2",
            htmlUrl: "https://github.com/owner/repo/blob/main/.github/open-copilot/instructions.md",
            encoding: "base64",
            encodedContent: Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("General instructions")),
            target: null,
            submoduleGitUrl: null);
        
        mockRepoContent.Setup(r => r.GetAllContents("owner", "repo", ".github/open-copilot/instructions.md"))
            .ReturnsAsync(new[] { content });
        
        var logger = new TestLogger<InstructionsLoader>();
        var loader = new InstructionsLoader(mockClient.Object, logger);

        // Act
        var instructions = await loader.LoadInstructionsAsync(owner: "owner", repo: "repo", issueNumber: 42);

        // Assert
        instructions.ShouldBe("General instructions");
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
