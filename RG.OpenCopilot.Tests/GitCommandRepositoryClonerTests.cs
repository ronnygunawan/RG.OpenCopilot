using Moq;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class GitCommandRepositoryClonerTests {
    [Fact]
    public async Task CloneRepositoryAsync_SuccessfulClone_ReturnsPath() {
        // Arrange
        var mockCommandExecutor = new Mock<ICommandExecutor>();
        mockCommandExecutor.Setup(e => e.ExecuteCommandAsync(
                It.IsAny<string>(),
                "git",
                It.Is<string[]>(args => args[0] == "clone"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CommandResult { ExitCode = 0, Output = "Cloning into '.'\n", Error = "" });

        var logger = new TestLogger<GitCommandRepositoryCloner>();
        var cloner = new GitCommandRepositoryCloner(commandExecutor: mockCommandExecutor.Object, logger: logger);

        // Act
        var path = await cloner.CloneRepositoryAsync(owner: "owner", repo: "repo", token: "token123", branch: "main");

        // Assert
        path.ShouldNotBeNull();
        path.ShouldContain("opencopilot-repos");
        path.ShouldContain("owner-repo");
        mockCommandExecutor.Verify(e => e.ExecuteCommandAsync(
            It.IsAny<string>(),
            "git",
            It.Is<string[]>(args => 
                args[0] == "clone" && 
                args[1] == "--branch" && 
                args[2] == "main" && 
                args[3] == "--single-branch" &&
                args[4].Contains("https://x-access-token:token123@github.com/owner/repo.git") &&
                args[5] == "."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloneRepositoryAsync_CloneFails_ThrowsException() {
        // Arrange
        var mockCommandExecutor = new Mock<ICommandExecutor>();
        mockCommandExecutor.Setup(e => e.ExecuteCommandAsync(
                It.IsAny<string>(),
                "git",
                It.Is<string[]>(args => args[0] == "clone"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(value: new CommandResult { ExitCode = 1, Output = "", Error = "fatal: repository not found" });

        var logger = new TestLogger<GitCommandRepositoryCloner>();
        var cloner = new GitCommandRepositoryCloner(commandExecutor: mockCommandExecutor.Object, logger: logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await cloner.CloneRepositoryAsync(owner: "owner", repo: "repo", token: "token123", branch: "main"));
        
        exception.Message.ShouldContain("Failed to clone repository");
    }

    [Fact]
    public void CleanupRepository_ValidPath_DeletesDirectory() {
        // Arrange
        var mockCommandExecutor = new Mock<ICommandExecutor>();
        var logger = new TestLogger<GitCommandRepositoryCloner>();
        var cloner = new GitCommandRepositoryCloner(commandExecutor: mockCommandExecutor.Object, logger: logger);

        // Create a temp directory to delete
        var tempPath = Path.Combine(Path.GetTempPath(), "opencopilot-repos", $"test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempPath);
        Directory.Exists(tempPath).ShouldBeTrue();

        // Act
        cloner.CleanupRepository(localPath: tempPath);

        // Assert
        Directory.Exists(tempPath).ShouldBeFalse();
    }

    [Fact]
    public void CleanupRepository_PathOutsideTempRoot_DoesNotDelete() {
        // Arrange
        var mockCommandExecutor = new Mock<ICommandExecutor>();
        var logger = new TestLogger<GitCommandRepositoryCloner>();
        var cloner = new GitCommandRepositoryCloner(commandExecutor: mockCommandExecutor.Object, logger: logger);

        // Create a directory outside the temp root
        var invalidPath = Path.Combine(Path.GetTempPath(), $"unsafe-{Guid.NewGuid()}");
        Directory.CreateDirectory(invalidPath);

        // Act
        cloner.CleanupRepository(localPath: invalidPath);

        // Assert
        // Directory should still exist (not deleted for safety)
        Directory.Exists(invalidPath).ShouldBeTrue();

        // Cleanup
        Directory.Delete(invalidPath, recursive: true);
    }

    [Fact]
    public void CleanupRepository_NonExistentPath_DoesNotThrow() {
        // Arrange
        var mockCommandExecutor = new Mock<ICommandExecutor>();
        var logger = new TestLogger<GitCommandRepositoryCloner>();
        var cloner = new GitCommandRepositoryCloner(commandExecutor: mockCommandExecutor.Object, logger: logger);

        var nonExistentPath = Path.Combine(Path.GetTempPath(), "opencopilot-repos", $"nonexistent-{Guid.NewGuid()}");

        // Act & Assert
        Should.NotThrow(() => cloner.CleanupRepository(localPath: nonExistentPath));
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
