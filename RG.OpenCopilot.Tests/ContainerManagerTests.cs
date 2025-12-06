using RG.OpenCopilot.App;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

public class ContainerManagerTests {
    [Fact]
    public async Task CreateContainerAsync_ReturnsContainerId() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var containerId = await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main");

        // Assert
        containerId.ShouldNotBeNullOrEmpty();
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("run"));
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("exec"));
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("clone"));
    }

    [Fact]
    public async Task CreateContainerAsync_ThrowsWhenDockerRunFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "run" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateContainerAsync(
                owner: "test-owner",
                repo: "test-repo",
                token: "test-token",
                branch: "main"));

        exception.Message.ShouldBe("Failed to create container: Command failed");
    }

    [Fact]
    public async Task CreateContainerAsync_CleansUpContainerWhenCloneFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateContainerAsync(
                owner: "test-owner",
                repo: "test-repo",
                token: "test-token",
                branch: "main"));

        exception.Message.ShouldBe("Failed to clone repository: Command failed");

        // Verify cleanup was called
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("stop"));
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("rm"));
    }

    [Fact]
    public async Task ExecuteInContainerAsync_ReturnsCommandResult() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.ExecuteInContainerAsync(
            containerId: "test-container",
            command: "echo",
            args: new[] { "hello" });

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("exec"));
    }

    [Fact]
    public async Task ReadFileInContainerAsync_ReturnsFileContent() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var content = await manager.ReadFileInContainerAsync(
            containerId: "test-container",
            filePath: "test.txt");

        // Assert
        content.ShouldBe("test output");
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("cat"));
    }

    [Fact]
    public async Task ReadFileInContainerAsync_ThrowsWhenReadFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.ReadFileInContainerAsync(
                containerId: "test-container",
                filePath: "test.txt"));

        exception.Message.ShouldBe("Failed to read file test.txt: Command failed");
    }

    [Fact]
    public async Task WriteFileInContainerAsync_WritesFile() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.WriteFileInContainerAsync(
            containerId: "test-container",
            filePath: "test.txt",
            content: "test content");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("sh"));
    }

    [Fact]
    public async Task WriteFileInContainerAsync_ThrowsWhenWriteFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.WriteFileInContainerAsync(
                containerId: "test-container",
                filePath: "test.txt",
                content: "test content"));

        exception.Message.ShouldBe("Failed to write file test.txt: Command failed");
    }

    [Fact]
    public async Task CommitAndPushAsync_CommitsAndPushesChanges() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            ReturnNonEmptyStatusOnce = true
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CommitAndPushAsync(
            containerId: "test-container",
            commitMessage: "Test commit",
            owner: "test-owner",
            repo: "test-repo",
            branch: "main",
            token: "test-token");

        // Assert
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("git") && c.Args.Contains("config"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("git") && c.Args.Contains("add"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("git") && c.Args.Contains("status"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("git") && c.Args.Contains("commit"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("git") && c.Args.Contains("push"));
    }

    [Fact]
    public async Task CommitAndPushAsync_SkipsCommitWhenNoChanges() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CommitAndPushAsync(
            containerId: "test-container",
            commitMessage: "Test commit",
            owner: "test-owner",
            repo: "test-repo",
            branch: "main",
            token: "test-token");

        // Assert - should check status but not commit or push
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("git") && c.Args.Contains("status"));
        commandExecutor.Commands.ShouldNotContain(c => c.Args.Contains("git") && c.Args.Contains("commit"));
        commandExecutor.Commands.ShouldNotContain(c => c.Args.Contains("git") && c.Args.Contains("push"));
    }

    [Fact]
    public async Task CommitAndPushAsync_ThrowsWhenCommitFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            ReturnNonEmptyStatusOnce = true,
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CommitAndPushAsync(
                containerId: "test-container",
                commitMessage: "Test commit",
                owner: "test-owner",
                repo: "test-repo",
                branch: "main",
                token: "test-token"));

        exception.Message.ShouldBe("Failed to commit: Command failed");
    }

    [Fact]
    public async Task CleanupContainerAsync_StopsAndRemovesContainer() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CleanupContainerAsync(containerId: "test-container");

        // Assert
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("stop"));
        commandExecutor.Commands.ShouldContain(c => c.Command == "docker" && c.Args.Contains("rm"));
    }

    [Fact]
    public async Task CommitAndPushAsync_ThrowsWhenPushFails() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            ReturnNonEmptyStatusOnce = true,
            FailOnPush = true
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CommitAndPushAsync(
                containerId: "test-container",
                commitMessage: "Test commit",
                owner: "test-owner",
                repo: "test-repo",
                branch: "main",
                token: "test-token"));

        exception.Message.ShouldBe("Failed to push: Command failed");
    }

    [Fact]
    public async Task WriteFileInContainerAsync_EscapesSingleQuotesInContent() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.WriteFileInContainerAsync(
            containerId: "test-container",
            filePath: "test.txt",
            content: "It's a test with 'quotes'");

        // Assert
        var execCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" &&
            c.Args.Contains("exec") &&
            c.Args.Contains("sh"));
        execCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateContainerAsync_InstallsGitInContainer() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("update"));
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("install") && c.Args.Contains("git"));
    }

    [Fact]
    public async Task ExecuteInContainerAsync_UsesWorkingDirectory() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.ExecuteInContainerAsync(
            containerId: "test-container",
            command: "ls",
            args: new[] { "-la" });

        // Assert
        var execCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" && c.Args.Contains("exec"));
        execCommand.ShouldNotBeNull();
        execCommand.Args.ShouldContain("-w");
    }

    [Fact]
    public async Task CommitAndPushAsync_ConfiguresGitUserBeforeCommit() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            ReturnNonEmptyStatusOnce = true
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CommitAndPushAsync(
            containerId: "test-container",
            commitMessage: "Test commit",
            owner: "test-owner",
            repo: "test-repo",
            branch: "main",
            token: "test-token");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("config") && c.Args.Contains("user.name") && c.Args.Contains("RG.OpenCopilot[bot]"));
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("config") && c.Args.Contains("user.email"));
    }

    [Fact]
    public async Task CommitAndPushAsync_SetsRemoteUrlWithToken() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            ReturnNonEmptyStatusOnce = true
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CommitAndPushAsync(
            containerId: "test-container",
            commitMessage: "Test commit",
            owner: "test-owner",
            repo: "test-repo",
            branch: "main",
            token: "test-token");

        // Assert
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("remote") && c.Args.Contains("set-url"));
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
        public bool ReturnNonEmptyStatusOnce { get; set; }
        public bool FailOnPush { get; set; }
        private bool _statusReturned = false;

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

            // Special handling for git status to simulate changes/no changes
            if (args.Contains("git") && args.Contains("status") && args.Contains("--porcelain")) {
                if (ReturnNonEmptyStatusOnce && !_statusReturned) {
                    _statusReturned = true;
                    return Task.FromResult(new CommandResult {
                        ExitCode = 0,
                        Output = "M test.txt",
                        Error = ""
                    });
                }
                // Return empty status (no changes)
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = "",
                    Error = ""
                });
            }

            // Special handling for git push failure
            if (FailOnPush && args.Contains("git") && args.Contains("push")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = 1,
                    Output = "",
                    Error = "Command failed"
                });
            }

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

            // Default success response
            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "test output",
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
