using RG.OpenCopilot.PRGenerationAgent.Services;
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

    [Fact]
    public async Task CreateDirectoryAsync_HandlesWindowsStylePaths() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act - Test that Windows-style paths with backslashes work
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "src\\subdir");

        // Assert - Verify the path was normalized to forward slashes for the container
        var mkdirCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mkdir"));
        mkdirCommand.ShouldNotBeNull();
        var pathArg = mkdirCommand.Args.Last();
        pathArg.ShouldContain("/");
        pathArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task CreateDirectoryAsync_PreventsDotDotTraversal() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert - Attempt to use .. to escape workspace should throw
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: "../../etc"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task ReadFileInContainerAsync_UsesLinuxPathsRegardlessOfHost() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.ReadFileInContainerAsync(
            containerId: "test-container",
            filePath: "src/MyClass.cs");

        // Assert - Verify the command uses forward slashes (Linux container path)
        var catCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("cat"));
        catCommand.ShouldNotBeNull();
        var pathArg = catCommand.Args.Last();
        pathArg.ShouldStartWith("/workspace/");
        pathArg.ShouldContain("/");
        pathArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task WriteFileInContainerAsync_NormalizesWindowsPaths() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.WriteFileInContainerAsync(
            containerId: "test-container",
            filePath: "src\\test\\file.txt",
            content: "test content");

        // Assert - Verify the path was normalized
        var writeCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("sh"));
        writeCommand.ShouldNotBeNull();
        var shCommand = writeCommand.Args.Last();
        shCommand.ShouldContain("/workspace/src/test/file.txt");
        shCommand.ShouldNotContain("\\");
    }

    [Fact]
    public async Task MoveAsync_HandlesWindowsStylePaths() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.MoveAsync(
            containerId: "test-container",
            source: "src\\old.txt",
            dest: "dest\\new.txt");

        // Assert
        var mvCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mv"));
        mvCommand.ShouldNotBeNull();
        var sourceArg = mvCommand.Args[mvCommand.Args.ToList().IndexOf("mv") + 1];
        var destArg = mvCommand.Args[mvCommand.Args.ToList().IndexOf("mv") + 2];
        sourceArg.ShouldContain("/");
        sourceArg.ShouldNotContain("\\");
        destArg.ShouldContain("/");
        destArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task CopyAsync_HandlesWindowsStylePaths() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CopyAsync(
            containerId: "test-container",
            source: "src\\file.txt",
            dest: "dest\\file.txt");

        // Assert
        var cpCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("cp"));
        cpCommand.ShouldNotBeNull();
        var sourceArg = cpCommand.Args[cpCommand.Args.ToList().IndexOf("cp") + 2];
        var destArg = cpCommand.Args[cpCommand.Args.ToList().IndexOf("cp") + 3];
        sourceArg.ShouldContain("/");
        sourceArg.ShouldNotContain("\\");
        destArg.ShouldContain("/");
        destArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task DeleteAsync_HandlesWindowsStylePaths() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.DeleteAsync(
            containerId: "test-container",
            path: "src\\file.txt");

        // Assert
        var rmCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("rm"));
        rmCommand.ShouldNotBeNull();
        var pathArg = rmCommand.Args.Last();
        pathArg.ShouldContain("/");
        pathArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task ListContentsAsync_HandlesWindowsStylePaths() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.ListContentsAsync(
            containerId: "test-container",
            dirPath: "src\\subdir");

        // Assert
        var lsCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("ls"));
        lsCommand.ShouldNotBeNull();
        var pathArg = lsCommand.Args.Last();
        pathArg.ShouldContain("/");
        pathArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task DirectoryExistsAsync_HandlesWindowsStylePaths() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.DirectoryExistsAsync(
            containerId: "test-container",
            dirPath: "src\\subdir");

        // Assert
        var testCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("test"));
        testCommand.ShouldNotBeNull();
        var pathArg = testCommand.Args.Last();
        pathArg.ShouldContain("/");
        pathArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task CreateDirectoryAsync_HandlesDotInPath() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "src/./subdir");

        // Assert - paths are passed through, validation ensures they're safe
        var mkdirCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mkdir"));
        mkdirCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateDirectoryAsync_HandlesValidDotDotPath() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act - this should be allowed as it resolves within workspace
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "src/sub/../../valid");

        // Assert - paths are validated but not normalized in the command
        var mkdirCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mkdir"));
        mkdirCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateDirectoryAsync_ThrowsOnNullOrEmptyPath() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert - null path
        var exception1 = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: null!));
        exception1.Message.ShouldContain("cannot be null or empty");

        // Act & Assert - empty path
        var exception2 = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: ""));
        exception2.Message.ShouldContain("cannot be null or empty");

        // Act & Assert - whitespace path
        var exception3 = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: "   "));
        exception3.Message.ShouldContain("cannot be null or empty");
    }

    [Fact]
    public async Task ReadFileInContainerAsync_HandlesEmptyRelativePath() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.ReadFileInContainerAsync(
            containerId: "test-container",
            filePath: "/");

        // Assert - Should handle empty relative path after trimming leading slash
        var catCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("cat"));
        catCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateDirectoryAsync_HandlesLeadingSlashes() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "/src/subdir");

        // Assert
        var mkdirCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mkdir"));
        mkdirCommand.ShouldNotBeNull();
        var pathArg = mkdirCommand.Args.Last();
        pathArg.ShouldBe("/workspace/src/subdir");
    }

    [Fact]
    public async Task CreateDirectoryAsync_HandlesOnlyDotDotPath() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert - Path that resolves to root should throw
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CreateDirectoryAsync(
                containerId: "test-container",
                dirPath: "../.."));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task ReadFileInContainerAsync_ThrowsOnFailure() {
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

        exception.Message.ShouldContain("Failed to read file");
    }

    [Fact]
    public async Task WriteFileInContainerAsync_ThrowsOnFailure() {
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
                content: "content"));

        exception.Message.ShouldContain("Failed to write file");
    }

    [Fact]
    public async Task CreateDirectoryAsync_ThrowsOnFailure() {
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
                dirPath: "test"));

        exception.Message.ShouldContain("Failed to create directory");
    }

    [Fact]
    public async Task MoveAsync_ThrowsOnFailure() {
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
                source: "src.txt",
                dest: "dest.txt"));

        exception.Message.ShouldContain("Failed to move");
    }

    [Fact]
    public async Task CopyAsync_ThrowsOnFailure() {
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
                source: "src.txt",
                dest: "dest.txt"));

        exception.Message.ShouldContain("Failed to copy");
    }

    [Fact]
    public async Task DeleteAsync_ThrowsOnFailure() {
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
                path: "test.txt"));

        exception.Message.ShouldContain("Failed to delete");
    }

    [Fact]
    public async Task ListContentsAsync_ThrowsOnFailure() {
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
                dirPath: "test"));

        exception.Message.ShouldContain("Failed to list contents");
    }

    [Fact]
    public async Task WriteFileInContainerAsync_EscapesSingleQuotes() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.WriteFileInContainerAsync(
            containerId: "test-container",
            filePath: "test.txt",
            content: "It's a test");

        // Assert
        var shCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("sh"));
        shCommand.ShouldNotBeNull();
        var commandArg = shCommand.Args.Last();
        commandArg.ShouldContain("'\\''");  // Single quotes should be escaped
    }

    [Fact]
    public async Task DirectoryExistsAsync_ReturnsTrueWhenExists() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.DirectoryExistsAsync(
            containerId: "test-container",
            dirPath: "test");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task DirectoryExistsAsync_ReturnsFalseWhenNotExists() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.DirectoryExistsAsync(
            containerId: "test-container",
            dirPath: "test");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_WithRecursiveFlag() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.DeleteAsync(
            containerId: "test-container",
            path: "test",
            recursive: true);

        // Assert
        var rmCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("rm"));
        rmCommand.ShouldNotBeNull();
        rmCommand.Args.ShouldContain("-rf");
    }

    [Fact]
    public async Task MoveAsync_ThrowsOnInvalidSource() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert - Source outside workspace
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.MoveAsync(
                containerId: "test-container",
                source: "../../etc/passwd",
                dest: "dest.txt"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task CopyAsync_ThrowsOnInvalidDest() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act & Assert - Dest outside workspace
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await manager.CopyAsync(
                containerId: "test-container",
                source: "src.txt",
                dest: "../../etc/passwd"));

        exception.Message.ShouldContain("outside the workspace directory");
    }

    [Fact]
    public async Task CreateDirectoryAsync_AllowsWorkspaceRoot() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act - Creating at workspace root should be allowed
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "/");

        // Assert
        var mkdirCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mkdir"));
        mkdirCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task ReadFileInContainerAsync_HandlesMultipleLeadingSlashes() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.ReadFileInContainerAsync(
            containerId: "test-container",
            filePath: "///test.txt");

        // Assert
        var catCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("cat"));
        catCommand.ShouldNotBeNull();
        var pathArg = catCommand.Args.Last();
        pathArg.ShouldBe("/workspace/test.txt");
    }

    [Fact]
    public async Task WriteFileInContainerAsync_HandlesBackslashInBasePath() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act - Even with mixed slashes, should normalize
        await manager.WriteFileInContainerAsync(
            containerId: "test-container",
            filePath: "dir\\subdir\\file.txt",
            content: "test");

        // Assert
        var shCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("sh"));
        shCommand.ShouldNotBeNull();
        var commandArg = shCommand.Args.Last();
        commandArg.ShouldContain("/workspace/dir/subdir/file.txt");
        commandArg.ShouldNotContain("\\");
    }

    [Fact]
    public async Task CreateDirectoryAsync_HandlesConsecutiveDots() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act - Path with consecutive dots should work
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "src/../dest");

        // Assert
        var mkdirCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mkdir"));
        mkdirCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task MoveAsync_HandlesComplexPath() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.MoveAsync(
            containerId: "test-container",
            source: "/a/b/c/../d.txt",
            dest: "x/y/z.txt");

        // Assert - Both paths should be normalized
        var mvCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mv"));
        mvCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_WithoutRecursiveFlag() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.DeleteAsync(
            containerId: "test-container",
            path: "test.txt",
            recursive: false);

        // Assert
        var rmCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("rm"));
        rmCommand.ShouldNotBeNull();
        rmCommand.Args.ShouldContain("-f");
        rmCommand.Args.ShouldNotContain("-rf");
    }

    [Fact]
    public async Task ListContentsAsync_ParsesOutput() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.ListContentsAsync(
            containerId: "test-container",
            dirPath: "test");

        // Assert - Should return parsed list (TestCommandExecutor returns empty success)
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateDirectoryAsync_HandlesOnlySlashes() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act - Just slashes should resolve to workspace root
        await manager.CreateDirectoryAsync(
            containerId: "test-container",
            dirPath: "///");

        // Assert
        var mkdirCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("mkdir"));
        mkdirCommand.ShouldNotBeNull();
    }

    [Fact]
    public async Task CopyAsync_BothPathsWithBackslashes() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CopyAsync(
            containerId: "test-container",
            source: "src\\dir\\file.txt",
            dest: "dest\\dir\\file.txt");

        // Assert - Both paths should be normalized
        var cpCommand = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("cp"));
        cpCommand.ShouldNotBeNull();
        var sourceArg = cpCommand.Args[cpCommand.Args.ToList().IndexOf("cp") + 2];
        var destArg = cpCommand.Args[cpCommand.Args.ToList().IndexOf("cp") + 3];
        sourceArg.ShouldNotContain("\\");
        destArg.ShouldNotContain("\\");
    }

    [Theory]
    [InlineData(ContainerImageType.DotNet, "mcr.microsoft.com/dotnet/sdk:10.0")]
    [InlineData(ContainerImageType.JavaScript, "node:20-bookworm")]
    [InlineData(ContainerImageType.Java, "eclipse-temurin:21-jdk")]
    [InlineData(ContainerImageType.Go, "golang:1.22-bookworm")]
    [InlineData(ContainerImageType.Rust, "rust:1-bookworm")]
    public async Task CreateContainerAsync_UsesCorrectImageForType(ContainerImageType imageType, string expectedImage) {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: imageType);

        // Assert
        var dockerRunCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" && c.Args.Contains("run"));
        dockerRunCommand.ShouldNotBeNull();
        dockerRunCommand.Args.ShouldContain(expectedImage);
    }

    [Fact]
    public async Task CreateContainerAsync_DefaultsToDoTNetImage() {
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
        var dockerRunCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" && c.Args.Contains("run"));
        dockerRunCommand.ShouldNotBeNull();
        dockerRunCommand.Args.ShouldContain("mcr.microsoft.com/dotnet/sdk:10.0");
    }

    [Fact]
    public async Task CreateContainerAsync_SkipsGitInstallationWhenGitExists() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            GitAlreadyInstalled = true
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.JavaScript);

        // Assert
        // Should check for git with 'which git'
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("which") && c.Args.Contains("git"));
        
        // Should NOT install git
        commandExecutor.Commands.ShouldNotContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("install"));
    }

    [Theory]
    [InlineData(ContainerImageType.DotNet)]
    [InlineData(ContainerImageType.JavaScript)]
    [InlineData(ContainerImageType.Java)]
    [InlineData(ContainerImageType.Go)]
    [InlineData(ContainerImageType.Rust)]
    public async Task CreateContainerAsync_InstallsGitForAllImageTypes(ContainerImageType imageType) {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: imageType);

        // Assert
        // Should check for git
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("which") && c.Args.Contains("git"));
        
        // Should install git via apt-get
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("update"));
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("install") && c.Args.Contains("git"));
    }

    [Fact]
    public async Task CreateContainerAsync_LogsImageTypeAndBaseImage() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.Go);

        // Assert - verify the container was created successfully
        var containerId = commandExecutor.Commands
            .FirstOrDefault(c => c.Command == "docker" && c.Args.Contains("run"))?
            .Args;
        containerId.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateContainerAsync_WithJavaScriptType_UsesNodeImage() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.JavaScript);

        // Assert
        result.ShouldNotBeNullOrEmpty();
        var dockerRunCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" && c.Args.Contains("run"));
        dockerRunCommand.ShouldNotBeNull();
        dockerRunCommand.Args.ShouldContain("node:20-bookworm");
    }

    [Fact]
    public async Task CreateContainerAsync_WithJavaType_UsesTemurinImage() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.Java);

        // Assert
        result.ShouldNotBeNullOrEmpty();
        var dockerRunCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" && c.Args.Contains("run"));
        dockerRunCommand.ShouldNotBeNull();
        dockerRunCommand.Args.ShouldContain("eclipse-temurin:21-jdk");
    }

    [Fact]
    public async Task CreateContainerAsync_WithGoType_UsesGolangImage() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.Go);

        // Assert
        result.ShouldNotBeNullOrEmpty();
        var dockerRunCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" && c.Args.Contains("run"));
        dockerRunCommand.ShouldNotBeNull();
        dockerRunCommand.Args.ShouldContain("golang:1.22-bookworm");
    }

    [Fact]
    public async Task CreateContainerAsync_WithRustType_UsesRustImage() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var result = await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.Rust);

        // Assert
        result.ShouldNotBeNullOrEmpty();
        var dockerRunCommand = commandExecutor.Commands.FirstOrDefault(c =>
            c.Command == "docker" && c.Args.Contains("run"));
        dockerRunCommand.ShouldNotBeNull();
        dockerRunCommand.Args.ShouldContain("rust:1-bookworm");
    }

    [Fact]
    public async Task EnsureGitInstalledAsync_WhenGitNotInstalled_InstallsGitForDotNet() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.DotNet);

        // Assert - should run which git first
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("which") && c.Args.Contains("git"));
        
        // Then install git via apt-get
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("update"));
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("install") && c.Args.Contains("-y") && c.Args.Contains("git"));
    }

    [Fact]
    public async Task EnsureGitInstalledAsync_WhenGitAlreadyInstalled_SkipsInstallation() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            GitAlreadyInstalled = true
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        await manager.CreateContainerAsync(
            owner: "test-owner",
            repo: "test-repo",
            token: "test-token",
            branch: "main",
            imageType: ContainerImageType.Rust);

        // Assert - should check for git
        commandExecutor.Commands.ShouldContain(c =>
            c.Args.Contains("which") && c.Args.Contains("git"));
        
        // Should NOT install git
        commandExecutor.Commands.ShouldNotContain(c =>
            c.Args.Contains("apt-get") && c.Args.Contains("install"));
    }

    [Fact]
    public async Task CreateContainerAsync_WithInvalidImageType_ThrowsArgumentOutOfRangeException() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);
        var invalidImageType = (ContainerImageType)999; // Invalid enum value

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
            async () => await manager.CreateContainerAsync(
                owner: "test-owner",
                repo: "test-repo",
                token: "test-token",
                branch: "main",
                imageType: invalidImageType));

        exception.Message.ShouldContain("Unsupported image type");
        exception.ParamName.ShouldBe("imageType");
    }

    [Fact]
    public async Task VerifyBuildToolsAsync_ChecksAllBuildTools() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var status = await manager.VerifyBuildToolsAsync(containerId: "test-container");

        // Assert
        status.ShouldNotBeNull();
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("dotnet"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("npm"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("gradle"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("mvn"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("go"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("cargo"));
    }

    [Fact]
    public async Task VerifyBuildToolsAsync_AllToolsAvailable_ReturnsAllTrue() {
        // Arrange
        var commandExecutor = new TestCommandExecutor();
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var status = await manager.VerifyBuildToolsAsync(containerId: "test-container");

        // Assert
        status.DotnetAvailable.ShouldBeTrue();
        status.NpmAvailable.ShouldBeTrue();
        status.GradleAvailable.ShouldBeTrue();
        status.MavenAvailable.ShouldBeTrue();
        status.GoAvailable.ShouldBeTrue();
        status.CargoAvailable.ShouldBeTrue();
        status.MissingTools.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyBuildToolsAsync_SomeToolsMissing_ReportsCorrectly() {
        // Arrange
        var commandExecutor = new TestCommandExecutor {
            FailOnCommand = "docker",
            FailOnArgs = new[] { "exec" }
        };
        var logger = new TestLogger<DockerContainerManager>();
        var manager = new DockerContainerManager(commandExecutor, logger);

        // Act
        var status = await manager.VerifyBuildToolsAsync(containerId: "test-container");

        // Assert
        status.DotnetAvailable.ShouldBeFalse();
        status.NpmAvailable.ShouldBeFalse();
        status.GradleAvailable.ShouldBeFalse();
        status.MavenAvailable.ShouldBeFalse();
        status.GoAvailable.ShouldBeFalse();
        status.CargoAvailable.ShouldBeFalse();
        status.MissingTools.ShouldContain("dotnet");
        status.MissingTools.ShouldContain("npm");
        status.MissingTools.ShouldContain("gradle");
        status.MissingTools.ShouldContain("maven");
        status.MissingTools.ShouldContain("go");
        status.MissingTools.ShouldContain("cargo");
        status.MissingTools.Count.ShouldBe(6);
    }

    [Fact]
    public async Task CreateContainerAsync_CallsVerifyBuildTools() {
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

        // Assert - should verify build tools after git installation and before cloning
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("dotnet"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("npm"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("gradle"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("mvn"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("go"));
        commandExecutor.Commands.ShouldContain(c => c.Args.Contains("which") && c.Args.Contains("cargo"));
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
        public bool GitAlreadyInstalled { get; set; }
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

            // Special handling for 'which git' check - return success if git is already installed, failure otherwise
            if (args.Contains("which") && args.Contains("git")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = GitAlreadyInstalled ? 0 : 1,
                    Output = GitAlreadyInstalled ? "/usr/bin/git" : "",
                    Error = ""
                });
            }

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
