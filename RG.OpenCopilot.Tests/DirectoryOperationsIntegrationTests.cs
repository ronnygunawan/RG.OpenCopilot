using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for directory operations that use actual Docker containers.
/// These tests require Docker to be installed and running.
/// </summary>
public class DirectoryOperationsIntegrationTests : IDisposable {
    private readonly DockerContainerManager _containerManager;
    private readonly string _containerId;
    private readonly ILogger<DockerContainerManager> _logger;

    public DirectoryOperationsIntegrationTests() {
        var commandExecutor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        _logger = new TestLogger<DockerContainerManager>();
        _containerManager = new DockerContainerManager(commandExecutor, _logger, new TestAuditLogger(), new FakeTimeProvider());
        
        // Create a test container - this is a slow operation
        _containerId = CreateTestContainerAsync().GetAwaiter().GetResult();
    }

    private async Task<string> CreateTestContainerAsync() {
        var commandExecutor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        var containerName = $"opencopilot-dirops-test-{Guid.NewGuid():N}".ToLowerInvariant();

        var result = await commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] {
                "run",
                "-d",
                "--name", containerName,
                "-w", "/workspace",
                "mcr.microsoft.com/dotnet/sdk:10.0",
                "sleep", "infinity"
            });

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to create test container: {result.Error}");
        }

        var containerId = result.Output.Trim();

        // Create workspace directory
        await commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] { "exec", containerId, "mkdir", "-p", "/workspace" });

        return containerId;
    }

    public void Dispose() {
        // Cleanup the test container
        try {
            _containerManager.CleanupContainerAsync(_containerId).GetAwaiter().GetResult();
        }
        catch {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task CreateDirectoryAsync_IntegrationTest_CreatesDirectory() {
        // Act
        await _containerManager.CreateDirectoryAsync(
            containerId: _containerId,
            dirPath: "test-dir");

        // Assert
        var exists = await _containerManager.DirectoryExistsAsync(
            containerId: _containerId,
            dirPath: "test-dir");
        
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateDirectoryAsync_IntegrationTest_CreatesNestedDirectories() {
        // Act
        await _containerManager.CreateDirectoryAsync(
            containerId: _containerId,
            dirPath: "src/components/ui");

        // Assert
        var exists = await _containerManager.DirectoryExistsAsync(
            containerId: _containerId,
            dirPath: "src/components/ui");
        
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task DirectoryExistsAsync_IntegrationTest_ReturnsFalse_WhenNotExists() {
        // Act
        var exists = await _containerManager.DirectoryExistsAsync(
            containerId: _containerId,
            dirPath: "nonexistent-directory");

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task MoveAsync_IntegrationTest_MovesDirectory() {
        // Arrange
        await _containerManager.CreateDirectoryAsync(_containerId, "source-dir");
        await _containerManager.WriteFileInContainerAsync(_containerId, "source-dir/file.txt", "test content");

        // Act
        await _containerManager.MoveAsync(
            containerId: _containerId,
            source: "source-dir",
            dest: "dest-dir");

        // Assert
        var sourceExists = await _containerManager.DirectoryExistsAsync(_containerId, "source-dir");
        var destExists = await _containerManager.DirectoryExistsAsync(_containerId, "dest-dir");
        var fileContent = await _containerManager.ReadFileInContainerAsync(_containerId, "dest-dir/file.txt");

        sourceExists.ShouldBeFalse();
        destExists.ShouldBeTrue();
        fileContent.TrimEnd().ShouldBe("test content");
    }

    [Fact]
    public async Task MoveAsync_IntegrationTest_RenamesFile() {
        // Arrange
        await _containerManager.WriteFileInContainerAsync(_containerId, "old-name.txt", "file content");

        // Act
        await _containerManager.MoveAsync(
            containerId: _containerId,
            source: "old-name.txt",
            dest: "new-name.txt");

        // Assert
        var newContent = await _containerManager.ReadFileInContainerAsync(_containerId, "new-name.txt");
        newContent.TrimEnd().ShouldBe("file content");
        
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _containerManager.ReadFileInContainerAsync(_containerId, "old-name.txt"));
        exception.Message.ShouldContain("Failed to read file");
    }

    [Fact]
    public async Task CopyAsync_IntegrationTest_CopiesDirectory() {
        // Arrange
        await _containerManager.CreateDirectoryAsync(_containerId, "original-dir");
        await _containerManager.WriteFileInContainerAsync(_containerId, "original-dir/file.txt", "test content");

        // Act
        await _containerManager.CopyAsync(
            containerId: _containerId,
            source: "original-dir",
            dest: "copy-dir");

        // Assert
        var originalExists = await _containerManager.DirectoryExistsAsync(_containerId, "original-dir");
        var copyExists = await _containerManager.DirectoryExistsAsync(_containerId, "copy-dir");
        var originalContent = await _containerManager.ReadFileInContainerAsync(_containerId, "original-dir/file.txt");
        var copyContent = await _containerManager.ReadFileInContainerAsync(_containerId, "copy-dir/file.txt");

        originalExists.ShouldBeTrue();
        copyExists.ShouldBeTrue();
        originalContent.TrimEnd().ShouldBe("test content");
        copyContent.TrimEnd().ShouldBe("test content");
    }

    [Fact]
    public async Task CopyAsync_IntegrationTest_CopiesFile() {
        // Arrange
        await _containerManager.WriteFileInContainerAsync(_containerId, "original.txt", "file content");

        // Act
        await _containerManager.CopyAsync(
            containerId: _containerId,
            source: "original.txt",
            dest: "copy.txt");

        // Assert
        var originalContent = await _containerManager.ReadFileInContainerAsync(_containerId, "original.txt");
        var copyContent = await _containerManager.ReadFileInContainerAsync(_containerId, "copy.txt");

        originalContent.TrimEnd().ShouldBe("file content");
        copyContent.TrimEnd().ShouldBe("file content");
    }

    [Fact]
    public async Task DeleteAsync_IntegrationTest_DeletesFile() {
        // Arrange
        await _containerManager.WriteFileInContainerAsync(_containerId, "to-delete.txt", "content");

        // Act
        await _containerManager.DeleteAsync(
            containerId: _containerId,
            path: "to-delete.txt");

        // Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _containerManager.ReadFileInContainerAsync(_containerId, "to-delete.txt"));
        exception.Message.ShouldContain("Failed to read file");
    }

    [Fact]
    public async Task DeleteAsync_IntegrationTest_DeletesDirectoryRecursively() {
        // Arrange
        await _containerManager.CreateDirectoryAsync(_containerId, "to-delete/nested");
        await _containerManager.WriteFileInContainerAsync(_containerId, "to-delete/file1.txt", "content1");
        await _containerManager.WriteFileInContainerAsync(_containerId, "to-delete/nested/file2.txt", "content2");

        // Act
        await _containerManager.DeleteAsync(
            containerId: _containerId,
            path: "to-delete",
            recursive: true);

        // Assert
        var exists = await _containerManager.DirectoryExistsAsync(_containerId, "to-delete");
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ListContentsAsync_IntegrationTest_ListsDirectoryContents() {
        // Arrange
        await _containerManager.CreateDirectoryAsync(_containerId, "list-test");
        await _containerManager.WriteFileInContainerAsync(_containerId, "list-test/file1.txt", "content1");
        await _containerManager.WriteFileInContainerAsync(_containerId, "list-test/file2.txt", "content2");
        await _containerManager.CreateDirectoryAsync(_containerId, "list-test/subdir");

        // Act
        var contents = await _containerManager.ListContentsAsync(
            containerId: _containerId,
            dirPath: "list-test");

        // Assert
        contents.Count.ShouldBe(3);
        contents.ShouldContain("file1.txt");
        contents.ShouldContain("file2.txt");
        contents.ShouldContain("subdir");
    }

    [Fact]
    public async Task ListContentsAsync_IntegrationTest_ReturnsEmptyList_ForEmptyDirectory() {
        // Arrange
        await _containerManager.CreateDirectoryAsync(_containerId, "empty-dir");

        // Act
        var contents = await _containerManager.ListContentsAsync(
            containerId: _containerId,
            dirPath: "empty-dir");

        // Assert
        contents.ShouldBeEmpty();
    }

    [Fact]
    public async Task DirectoryOperations_IntegrationTest_EnforcesWorkspaceRestriction() {
        // Test that operations outside /workspace are rejected
        var exception1 = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _containerManager.CreateDirectoryAsync(_containerId, "../outside"));
        exception1.Message.ShouldContain("outside the workspace directory");

        var exception2 = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _containerManager.MoveAsync(_containerId, "file.txt", "../../etc/file.txt"));
        exception2.Message.ShouldContain("outside the workspace directory");

        var exception3 = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _containerManager.DeleteAsync(_containerId, "../../../important"));
        exception3.Message.ShouldContain("outside the workspace directory");
    }

    // Test helper class
    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            // Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
