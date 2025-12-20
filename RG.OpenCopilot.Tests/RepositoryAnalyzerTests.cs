using Moq;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class RepositoryAnalyzerTests {
    [Fact]
    public async Task AnalyzeAsync_ReturnsAnalysisWithLanguages() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo> {
            new() { Name = "C#", Bytes = 10000 },
            new() { Name = "JavaScript", Bytes = 5000 }
        };

        var contents = new List<ContentInfo> {
            new() { Name = "README.md", Path = "README.md", IsDirectory = false },
            new() { Name = "package.json", Path = "package.json", IsDirectory = false }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.Languages.ShouldContainKey("C#");
        analysis.Languages["C#"].ShouldBe(10000);
        analysis.Languages.ShouldContainKey("JavaScript");
        analysis.Languages["JavaScript"].ShouldBe(5000);
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsKeyFiles() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo>();
        var contents = new List<ContentInfo> {
            new() { Name = "package.json", Path = "package.json", IsDirectory = false },
            new() { Name = "tsconfig.json", Path = "tsconfig.json", IsDirectory = false },
            new() { Name = "README.md", Path = "README.md", IsDirectory = false },
            new() { Name = "Dockerfile", Path = "Dockerfile", IsDirectory = false }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.KeyFiles.ShouldContain("package.json");
        analysis.KeyFiles.ShouldContain("tsconfig.json");
        analysis.KeyFiles.ShouldContain("README.md");
        analysis.KeyFiles.ShouldContain("Dockerfile");
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsNpmWithPackageJson() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo>();
        var contents = new List<ContentInfo> {
            new() { Name = "package.json", Path = "package.json", IsDirectory = false }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.DetectedBuildTool.ShouldBe("npm");
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsNpmWithYarnLock() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo>();
        var contents = new List<ContentInfo> {
            new() { Name = "package.json", Path = "package.json", IsDirectory = false },
            new() { Name = "yarn.lock", Path = "yarn.lock", IsDirectory = false }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.DetectedBuildTool.ShouldBe("yarn");
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsDotnetWithCsproj() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo>();
        var contents = new List<ContentInfo> {
            new() { Name = "project.csproj", Path = "project.csproj", IsDirectory = false }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.DetectedBuildTool.ShouldBe("dotnet");
    }

    [Fact]
    public async Task AnalyzeAsync_DetectsTestDirectories() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo>();
        var contents = new List<ContentInfo> {
            new() { Name = "tests", Path = "tests", IsDirectory = true },
            new() { Name = "src", Path = "src", IsDirectory = true }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.KeyFiles.ShouldContain("tests/");
    }

    [Fact]
    public async Task AnalyzeAsync_GeneratesCompleteSummary() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo> {
            new() { Name = "C#", Bytes = 10000 }
        };
        var contents = new List<ContentInfo> {
            new() { Name = "project.csproj", Path = "project.csproj", IsDirectory = false },
            new() { Name = "README.md", Path = "README.md", IsDirectory = false }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(contents);

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.Summary.ShouldNotBeNullOrEmpty();
        analysis.Summary.ShouldContain("C#");
        analysis.Summary.ShouldContain("dotnet");
    }

    [Fact]
    public async Task AnalyzeAsync_WithApiError_ReturnsMinimalAnalysis() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Not found", System.Net.HttpStatusCode.NotFound));

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.Summary.ShouldBe("Repository analysis unavailable");
    }

    [Fact]
    public async Task AnalyzeAsync_WithContentError_ContinuesWithoutKeyFiles() {
        // Arrange
        var mockAdapter = new Mock<IGitHubRepositoryAdapter>();

        var languages = new List<LanguageInfo> {
            new() { Name = "Python", Bytes = 5000 }
        };

        mockAdapter.Setup(a => a.GetLanguagesAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(languages);
        mockAdapter.Setup(a => a.GetContentsAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("Error", System.Net.HttpStatusCode.InternalServerError));

        var logger = new TestLogger<RepositoryAnalyzer>();
        var analyzer = new RepositoryAnalyzer(mockAdapter.Object, logger);

        // Act
        var analysis = await analyzer.AnalyzeAsync("owner", "repo");

        // Assert
        analysis.Languages.ShouldContainKey("Python");
        analysis.KeyFiles.ShouldBeEmpty();
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
