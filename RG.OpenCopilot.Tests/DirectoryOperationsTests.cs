using RG.OpenCopilot.App;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

public class DirectoryOperationsTests {
    [Fact]
    public async Task CreateDirectoryAsync_CreatesDirectory() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "src/components");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("mkdir") &&
            c.Args.Contains("-p") &&
            c.Args.Contains("/workspace/src/components"));
    }

    [Fact]
    public async Task CreateDirectoryAsync_ThrowsWhenCreationFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: "src/components"));

        exception.Message.ShouldBe("Failed to create directory src/components: Command failed");
    }

    [Fact]
    public async Task CreateDirectoryAsync_ThrowsWhenPathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: "../outside"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task DirectoryExistsAsync_ReturnsTrue_WhenDirectoryExists() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var exists = await manager.DirectoryExistsAsync(
            containerId: "test-container",
            dirPath: "src");

        // Assert
        exists.ShouldBeTrue();
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("test") &&
            c.Args.Contains("-d") &&
            c.Args.Contains("/workspace/src"));
    }

    [Fact]
    public async Task DirectoryExistsAsync_ReturnsFalse_WhenDirectoryDoesNotExist() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var exists = await manager.DirectoryExistsAsync(
            containerId: "test-container",
            dirPath: "nonexistent");

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task DirectoryExistsAsync_ThrowsWhenPathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.DirectoryExistsAsync(
                containerId: "test-container",
                dirPath: "../../etc"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task MoveAsync_MovesFileOrDirectory() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.MoveAsync(
            containerId: "test-container",
            source: "old.txt",
            dest: "new.txt");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("mv") &&
            c.Args.Contains("/workspace/old.txt") &&
            c.Args.Contains("/workspace/new.txt"));
    }

    [Fact]
    public async Task MoveAsync_ThrowsWhenMoveFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.MoveAsync(
                containerId: "test-container",
                source: "old.txt",
                dest: "new.txt"));

        exception.Message.ShouldBe("Failed to move old.txt to new.txt: Command failed");
    }

    [Fact]
    public async Task MoveAsync_ThrowsWhenSourcePathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.MoveAsync(
                containerId: "test-container",
                source: "../outside.txt",
                dest: "inside.txt"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task MoveAsync_ThrowsWhenDestPathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.MoveAsync(
                containerId: "test-container",
                source: "inside.txt",
                dest: "../outside.txt"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task CopyAsync_CopiesFileOrDirectory() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CopyAsync(
            containerId: "test-container",
            source: "original.txt",
            dest: "copy.txt");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("cp") &&
            c.Args.Contains("-r") &&
            c.Args.Contains("/workspace/original.txt") &&
            c.Args.Contains("/workspace/copy.txt"));
    }

    [Fact]
    public async Task CopyAsync_ThrowsWhenCopyFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CopyAsync(
                containerId: "test-container",
                source: "original.txt",
                dest: "copy.txt"));

        exception.Message.ShouldBe("Failed to copy original.txt to copy.txt: Command failed");
    }

    [Fact]
    public async Task CopyAsync_ThrowsWhenSourcePathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CopyAsync(
                containerId: "test-container",
                source: "../../etc/passwd",
                dest: "passwd.txt"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task CopyAsync_ThrowsWhenDestPathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CopyAsync(
                containerId: "test-container",
                source: "file.txt",
                dest: "../../../tmp/file.txt"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task DeleteAsync_DeletesFile() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.DeleteAsync(
            containerId: "test-container",
            path: "file.txt");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("rm") &&
            c.Args.Contains("-f") &&
            c.Args.Contains("/workspace/file.txt"));
    }

    [Fact]
    public async Task DeleteAsync_DeletesDirectoryRecursively() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.DeleteAsync(
            containerId: "test-container",
            path: "folder",
            recursive: true);

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("rm") &&
            c.Args.Contains("-rf") &&
            c.Args.Contains("/workspace/folder"));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenDeleteFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.DeleteAsync(
                containerId: "test-container",
                path: "file.txt"));

        exception.Message.ShouldBe("Failed to delete file.txt: Command failed");
    }

    [Fact]
    public async Task DeleteAsync_ThrowsWhenPathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.DeleteAsync(
                containerId: "test-container",
                path: "../../important-file.txt",
                recursive: true));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task ListContentsAsync_ReturnsDirectoryContents() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            CustomOutput = """
                file1.txt
                file2.txt
                folder1
                """
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var contents = await manager.ListContentsAsync(
            containerId: "test-container",
            dirPath: "src");

        // Assert
        contents.Count.ShouldBe(3);
        contents.ShouldContain("file1.txt");
        contents.ShouldContain("file2.txt");
        contents.ShouldContain("folder1");
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("ls") &&
            c.Args.Contains("-1") &&
            c.Args.Contains("/workspace/src"));
    }

    [Fact]
    public async Task ListContentsAsync_ReturnsEmptyList_WhenDirectoryEmpty() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            CustomOutput = ""
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var contents = await manager.ListContentsAsync(
            containerId: "test-container",
            dirPath: "empty");

        // Assert
        contents.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListContentsAsync_ThrowsWhenListFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.ListContentsAsync(
                containerId: "test-container",
                dirPath: "nonexistent"));

        exception.Message.ShouldBe("Failed to list contents of nonexistent: Command failed");
    }

    [Fact]
    public async Task ListContentsAsync_ThrowsWhenPathOutsideWorkspace() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.ListContentsAsync(
                containerId: "test-container",
                dirPath: "../../../etc"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task CreateDirectoryAsync_HandlesLeadingSlash() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "/src/components");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("/workspace/src/components"));
    }

    [Fact]
    public async Task MoveAsync_HandlesDifferentDirectoryLevels() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.MoveAsync(
            containerId: "test-container",
            source: "src/old/file.txt",
            dest: "dest/new/file.txt");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("/workspace/src/old/file.txt") &&
            c.Args.Contains("/workspace/dest/new/file.txt"));
    }

    [Fact]
    public async Task CreateDirectoryAsync_ThrowsWhenPathIsEmpty() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: ""));

        exception.Message.ShouldBe("Path cannot be null or empty.");
    }

    // Test helper classes
    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestCommandExecutor : ICommandExecutor {
        private readonly List<ExecutedCommand> _commands = [];
        public IReadOnlyList<ExecutedCommand> Commands => _commands;
        public string? FailOnCommand { get; set; }
        public string[]? FailOnArgs { get; set; }
        public string? CustomOutput { get; set; }

        public Task<CommandResult> ExecuteCommandAsync(
            string workingDirectory,
            string command,
            string[] args,
            CancellationToken cancellationToken = default) {
            _commands.Add(new ExecutedCommand {
                WorkingDirectory = workingDirectory,
                Command = command,
                Args = args
            });

            // Check if this command should fail
            if (FailOnCommand == command && FailOnArgs != null) {
                var shouldFail = FailOnArgs.All(arg => args.Contains(arg));
                if (shouldFail) {
                    return Task.FromResult(new CommandResult {
                        ExitCode = 1,
                        Output = "",
                        Error = "Command failed"
                    });
                }
            }

            // Return custom output if provided
            var output = CustomOutput ?? "test output";
            
            // Default success response
            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = output,
                Error = ""
            });
        }
    }

    private class ExecutedCommand {
        public string WorkingDirectory { get; init; } = "";
        public string Command { get; init; } = "";
        public IReadOnlyList<string> Args { get; init; } = Array.Empty<string>();
    }
}
