using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class FileEditorTests {
    [Fact]
    public async Task CreateFileAsync_CreatesNewFile() {
        // Arrange
        var containerManager = new TestContainerManager();
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());
        var content = "Hello, World!";

        // Act
        await editor.CreateFileAsync(containerId: "test-container", filePath: "test.txt", content: content);

        // Assert
        containerManager.WrittenFiles.ShouldContainKey("test.txt");
        containerManager.WrittenFiles["test.txt"].ShouldBe(content);
    }

    [Fact]
    public async Task CreateFileAsync_TracksChange() {
        // Arrange
        var containerManager = new TestContainerManager();
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());
        var content = "Hello, World!";

        // Act
        await editor.CreateFileAsync(containerId: "test-container", filePath: "test.txt", content: content);

        // Assert
        var changes = editor.GetChanges();
        changes.Count.ShouldBe(1);
        changes[0].Type.ShouldBe(ChangeType.Created);
        changes[0].Path.ShouldBe("test.txt");
        changes[0].OldContent.ShouldBeNull();
        changes[0].NewContent.ShouldBe(content);
    }

    [Fact]
    public async Task CreateFileAsync_ThrowsWhenFileExists() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("existing.txt", content: "existing");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await editor.CreateFileAsync(containerId: "test-container", filePath: "existing.txt", content: "new content"));
    }

    [Fact]
    public async Task CreateFileAsync_CreatesParentDirectory() {
        // Arrange
        var containerManager = new TestContainerManager();
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.CreateFileAsync(containerId: "test-container", filePath: "dir/subdir/test.txt", content: "content");

        // Assert
        containerManager.CreatedDirectories.ShouldContain("dir/subdir");
    }

    [Fact]
    public async Task ModifyFileAsync_ModifiesExistingFile() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("test.txt", content: "original");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.ModifyFileAsync(
            containerId: "test-container",
            filePath: "test.txt",
            transform: content => content.Replace("original", "modified"));

        // Assert
        containerManager.WrittenFiles["test.txt"].ShouldBe("modified");
    }

    [Fact]
    public async Task ModifyFileAsync_TracksChange() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("test.txt", content: "original");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.ModifyFileAsync(
            containerId: "test-container",
            filePath: "test.txt",
            transform: content => content.Replace("original", "modified"));

        // Assert
        var changes = editor.GetChanges();
        changes.Count.ShouldBe(1);
        changes[0].Type.ShouldBe(ChangeType.Modified);
        changes[0].Path.ShouldBe("test.txt");
        changes[0].OldContent.ShouldBe("original");
        changes[0].NewContent.ShouldBe("modified");
    }

    [Fact]
    public async Task ModifyFileAsync_ThrowsWhenFileDoesNotExist() {
        // Arrange
        var containerManager = new TestContainerManager();
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await editor.ModifyFileAsync(
                containerId: "test-container",
                filePath: "nonexistent.txt",
                transform: content => content));
    }

    [Fact]
    public async Task ModifyFileAsync_SkipsWriteWhenContentUnchanged() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("test.txt", content: "same");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.ModifyFileAsync(
            containerId: "test-container",
            filePath: "test.txt",
            transform: content => content);

        // Assert
        containerManager.WrittenFiles.ShouldBeEmpty();
        editor.GetChanges().ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteFileAsync_DeletesExistingFile() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("test.txt", content: "to delete");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.DeleteFileAsync(containerId: "test-container", filePath: "test.txt");

        // Assert
        containerManager.DeletedFiles.ShouldContain("test.txt");
    }

    [Fact]
    public async Task DeleteFileAsync_TracksChange() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("test.txt", content: "to delete");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.DeleteFileAsync(containerId: "test-container", filePath: "test.txt");

        // Assert
        var changes = editor.GetChanges();
        changes.Count.ShouldBe(1);
        changes[0].Type.ShouldBe(ChangeType.Deleted);
        changes[0].Path.ShouldBe("test.txt");
        changes[0].OldContent.ShouldBe("to delete");
        changes[0].NewContent.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteFileAsync_SkipsWhenFileDoesNotExist() {
        // Arrange
        var containerManager = new TestContainerManager();
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.DeleteFileAsync(containerId: "test-container", filePath: "nonexistent.txt");

        // Assert
        containerManager.DeletedFiles.ShouldBeEmpty();
        editor.GetChanges().ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteFileAsync_ThrowsForCriticalFiles() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("README.md", content: "readme");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await editor.DeleteFileAsync(containerId: "test-container", filePath: "README.md"));
    }

    [Fact]
    public async Task DeleteFileAsync_ThrowsForGitDirectory() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists(".git/config", content: "git config");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await editor.DeleteFileAsync(containerId: "test-container", filePath: ".git/config"));
    }

    [Fact]
    public async Task GetChanges_ReturnsAllTrackedChanges() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("modify.txt", content: "original");
        containerManager.SetFileExists("delete.txt", content: "to delete");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.CreateFileAsync(containerId: "test-container", filePath: "create.txt", content: "new");
        await editor.ModifyFileAsync(containerId: "test-container", filePath: "modify.txt", transform: c => "modified");
        await editor.DeleteFileAsync(containerId: "test-container", filePath: "delete.txt");

        // Assert
        var changes = editor.GetChanges();
        changes.Count.ShouldBe(3);
        changes[0].Type.ShouldBe(ChangeType.Created);
        changes[1].Type.ShouldBe(ChangeType.Modified);
        changes[2].Type.ShouldBe(ChangeType.Deleted);
    }

    [Fact]
    public async Task ClearChanges_RemovesAllTrackedChanges() {
        // Arrange
        var containerManager = new TestContainerManager();
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.CreateFileAsync(containerId: "test-container", filePath: "test.txt", content: "content");
        editor.ClearChanges();

        // Assert
        editor.GetChanges().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChanges_ReturnsNewList() {
        // Arrange
        var containerManager = new TestContainerManager();
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.CreateFileAsync(containerId: "test-container", filePath: "test.txt", content: "content");
        var changes1 = editor.GetChanges();
        var changes2 = editor.GetChanges();

        // Assert
        changes1.ShouldNotBeSameAs(changes2);
        changes1.Count.ShouldBe(changes2.Count);
    }

    [Fact]
    public async Task MultipleOperations_TrackedInOrder() {
        // Arrange
        var containerManager = new TestContainerManager();
        containerManager.SetFileExists("existing.txt", content: "v1");
        var editor = new FileEditor(containerManager, new TestLogger<FileEditor>());

        // Act
        await editor.CreateFileAsync(containerId: "test-container", filePath: "new1.txt", content: "content1");
        await editor.CreateFileAsync(containerId: "test-container", filePath: "new2.txt", content: "content2");
        await editor.ModifyFileAsync(containerId: "test-container", filePath: "existing.txt", transform: c => "v2");

        // Assert
        var changes = editor.GetChanges();
        changes.Count.ShouldBe(3);
        changes[0].Path.ShouldBe("new1.txt");
        changes[1].Path.ShouldBe("new2.txt");
        changes[2].Path.ShouldBe("existing.txt");
    }

    // Test helper classes
    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestContainerManager : IContainerManager {
        private readonly Dictionary<string, string> _files = new();
        public Dictionary<string, string> WrittenFiles { get; } = new();
        public List<string> DeletedFiles { get; } = new();
        public List<string> CreatedDirectories { get; } = new();

        public void SetFileExists(string path, string content) {
            _files[path] = content;
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container");
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, ContainerImageType imageType, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container");
        }

        public Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default) {
            if (command == "test" && args.Length >= 2 && args[0] == "-f") {
                var filePath = args[1];
                var exists = _files.ContainsKey(filePath);
                return Task.FromResult(new CommandResult {
                    ExitCode = exists ? 0 : 1,
                    Output = "",
                    Error = ""
                });
            }

            if (command == "mkdir" && args.Length >= 2 && args[0] == "-p") {
                CreatedDirectories.Add(args[1]);
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = "",
                    Error = ""
                });
            }

            if (command == "rm" && args.Length >= 2 && args[0] == "-f") {
                var filePath = args[1];
                DeletedFiles.Add(filePath);
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = "",
                    Error = ""
                });
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "",
                Error = ""
            });
        }

        public Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
            if (_files.TryGetValue(filePath, out var content)) {
                return Task.FromResult(content);
            }
            throw new InvalidOperationException($"Failed to read file {filePath}: No such file");
        }

        public Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
            WrittenFiles[filePath] = content;
            return Task.CompletedTask;
        }

        public Task CommitAndPushAsync(string containerId, string commitMessage, string owner, string repo, string branch, string token, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            CreatedDirectories.Add(dirPath);
            return Task.CompletedTask;
        }

        public Task<bool> DirectoryExistsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(true);
        }

        public Task MoveAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CopyAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string containerId, string path, bool recursive = false, CancellationToken cancellationToken = default) {
            DeletedFiles.Add(path);
            return Task.CompletedTask;
        }

        public Task<List<string>> ListContentsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<string>());
        }
    }
}
