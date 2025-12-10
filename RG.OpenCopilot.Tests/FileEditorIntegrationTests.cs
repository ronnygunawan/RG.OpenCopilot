using Microsoft.Extensions.Logging;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for FileEditor using actual container operations
/// These tests require Docker to be available
/// </summary>
public class FileEditorIntegrationTests {
    [Fact]
    public async Task CreateFileAsync_IntegrationTest_CreatesFileInContainer() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>(), new TestAuditLogger());
        string? containerId = null;

        try {
            // Create a test container
            containerId = await containerManager.CreateContainerAsync(
                owner: "test",
                repo: "test-repo",
                token: "dummy-token",
                branch: "main");

            var testContent = """
                # Test File
                This is a test file created by FileEditor integration test.
                """;

            // Act
            await editor.CreateFileAsync(
                containerId: containerId,
                filePath: "integration-test.md",
                content: testContent);

            // Assert
            var readContent = await containerManager.ReadFileInContainerAsync(containerId, "integration-test.md");
            readContent.ShouldBe(testContent);

            var changes = editor.GetChanges();
            changes.Count.ShouldBe(1);
            changes[0].Type.ShouldBe(ChangeType.Created);
            changes[0].Path.ShouldBe("integration-test.md");
        }
        finally {
            if (containerId != null) {
                await containerManager.CleanupContainerAsync(containerId);
            }
        }
    }

    [Fact]
    public async Task ModifyFileAsync_IntegrationTest_ModifiesExistingFile() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>(), new TestAuditLogger());
        string? containerId = null;

        try {
            containerId = await containerManager.CreateContainerAsync(
                owner: "test",
                repo: "test-repo",
                token: "dummy-token",
                branch: "main");

            // Create initial file
            var initialContent = "Initial content";
            await editor.CreateFileAsync(containerId, "test-modify.txt", initialContent);
            editor.ClearChanges();

            // Act
            await editor.ModifyFileAsync(
                containerId: containerId,
                filePath: "test-modify.txt",
                transform: content => content.Replace("Initial", "Modified"));

            // Assert
            var readContent = await containerManager.ReadFileInContainerAsync(containerId, "test-modify.txt");
            readContent.ShouldBe("Modified content");

            var changes = editor.GetChanges();
            changes.Count.ShouldBe(1);
            changes[0].Type.ShouldBe(ChangeType.Modified);
            changes[0].OldContent.ShouldBe(initialContent);
            changes[0].NewContent.ShouldBe("Modified content");
        }
        finally {
            if (containerId != null) {
                await containerManager.CleanupContainerAsync(containerId);
            }
        }
    }

    [Fact]
    public async Task DeleteFileAsync_IntegrationTest_DeletesFile() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>(), new TestAuditLogger());
        string? containerId = null;

        try {
            containerId = await containerManager.CreateContainerAsync(
                owner: "test",
                repo: "test-repo",
                token: "dummy-token",
                branch: "main");

            // Create a file to delete
            await editor.CreateFileAsync(containerId, "to-delete.txt", "This will be deleted");
            editor.ClearChanges();

            // Act
            await editor.DeleteFileAsync(containerId, "to-delete.txt");

            // Assert
            var result = await containerManager.ExecuteInContainerAsync(
                containerId: containerId,
                command: "test",
                args: new[] { "-f", "to-delete.txt" });

            result.Success.ShouldBeFalse(); // File should not exist

            var changes = editor.GetChanges();
            changes.Count.ShouldBe(1);
            changes[0].Type.ShouldBe(ChangeType.Deleted);
        }
        finally {
            if (containerId != null) {
                await containerManager.CleanupContainerAsync(containerId);
            }
        }
    }

    [Fact]
    public async Task CompleteWorkflow_IntegrationTest_TracksMixedOperations() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>(), new TestAuditLogger());
        string? containerId = null;

        try {
            containerId = await containerManager.CreateContainerAsync(
                owner: "test",
                repo: "test-repo",
                token: "dummy-token",
                branch: "main");

            // Act - Perform multiple operations
            await editor.CreateFileAsync(containerId, "file1.txt", "Content 1");
            await editor.CreateFileAsync(containerId, "file2.txt", "Content 2");
            await editor.ModifyFileAsync(containerId, "file1.txt", c => c + " - Modified");
            await editor.DeleteFileAsync(containerId, "file2.txt");

            // Assert
            var changes = editor.GetChanges();
            changes.Count.ShouldBe(4);
            changes[0].Type.ShouldBe(ChangeType.Created);
            changes[0].Path.ShouldBe("file1.txt");
            changes[1].Type.ShouldBe(ChangeType.Created);
            changes[1].Path.ShouldBe("file2.txt");
            changes[2].Type.ShouldBe(ChangeType.Modified);
            changes[2].Path.ShouldBe("file1.txt");
            changes[3].Type.ShouldBe(ChangeType.Deleted);
            changes[3].Path.ShouldBe("file2.txt");

            // Verify final state
            var file1Content = await containerManager.ReadFileInContainerAsync(containerId, "file1.txt");
            file1Content.ShouldBe("Content 1 - Modified");

            var file2Result = await containerManager.ExecuteInContainerAsync(
                containerId: containerId,
                command: "test",
                args: new[] { "-f", "file2.txt" });
            file2Result.Success.ShouldBeFalse(); // File should be deleted
        }
        finally {
            if (containerId != null) {
                await containerManager.CleanupContainerAsync(containerId);
            }
        }
    }

    [Fact]
    public async Task CreateFileAsync_IntegrationTest_CreatesNestedDirectories() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>(), new TestAuditLogger());
        string? containerId = null;

        try {
            containerId = await containerManager.CreateContainerAsync(
                owner: "test",
                repo: "test-repo",
                token: "dummy-token",
                branch: "main");

            // Act
            await editor.CreateFileAsync(
                containerId: containerId,
                filePath: "deeply/nested/directory/structure/test.txt",
                content: "Nested file content");

            // Assert
            var content = await containerManager.ReadFileInContainerAsync(
                containerId,
                "deeply/nested/directory/structure/test.txt");
            content.ShouldBe("Nested file content");
        }
        finally {
            if (containerId != null) {
                await containerManager.CleanupContainerAsync(containerId);
            }
        }
    }

    private static IContainerManager CreateRealContainerManager() {
        var commandExecutor = new ProcessCommandExecutor(CreateLogger<ProcessCommandExecutor>());
        return new TestDockerContainerManager(commandExecutor, CreateLogger<TestDockerContainerManager>());
    }

    private static ILogger<T> CreateLogger<T>() {
        var loggerFactory = LoggerFactory.Create(builder => {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        return loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Test-specific container manager that creates containers without cloning repositories
    /// </summary>
    private sealed class TestDockerContainerManager : IContainerManager {
        private readonly ICommandExecutor _commandExecutor;
        private readonly ILogger<TestDockerContainerManager> _logger;
        private const string WorkDir = "/workspace";

        public TestDockerContainerManager(ICommandExecutor commandExecutor, ILogger<TestDockerContainerManager> logger) {
            _commandExecutor = commandExecutor;
            _logger = logger;
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            return CreateContainerAsync(owner, repo, token, branch, ContainerImageType.DotNet, cancellationToken);
        }

        public async Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, ContainerImageType imageType, CancellationToken cancellationToken = default) {
            // Create a unique container name
            var containerName = $"opencopilot-test-{Guid.NewGuid():N}".ToLowerInvariant();

            _logger.LogInformation("Creating test container {ContainerName}", containerName);

            // Use a base image for testing - just need a container with basic shell commands
            var result = await _commandExecutor.ExecuteCommandAsync(
                workingDirectory: Directory.GetCurrentDirectory(),
                command: "docker",
                args: new[] {
                    "run",
                    "-d",
                    "--name", containerName,
                    "-w", WorkDir,
                    "mcr.microsoft.com/dotnet/sdk:10.0",
                    "sleep", "infinity"
                },
                cancellationToken: cancellationToken);

            if (!result.Success) {
                throw new InvalidOperationException($"Failed to create container: {result.Error}");
            }

            var containerId = result.Output.Trim();
            _logger.LogInformation("Created test container {ContainerId}", containerId);

            // Create the workspace directory
            await ExecuteInContainerAsync(containerId: containerId, command: "mkdir", args: new[] { "-p", WorkDir }, cancellationToken: cancellationToken);

            return containerId;
        }

        public async Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default) {
            var dockerArgs = new List<string> { "exec", "-w", WorkDir, containerId, command };
            dockerArgs.AddRange(args);

            var result = await _commandExecutor.ExecuteCommandAsync(
                workingDirectory: Directory.GetCurrentDirectory(),
                command: "docker",
                args: dockerArgs.ToArray(),
                cancellationToken: cancellationToken);

            _logger.LogDebug("Executed {Command} in container {ContainerId}: exit code {ExitCode}",
                command, containerId, result.ExitCode);

            return result;
        }

        public async Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
            var fullPath = Path.Join(WorkDir, filePath.TrimStart('/'));

            var result = await _commandExecutor.ExecuteCommandAsync(
                workingDirectory: Directory.GetCurrentDirectory(),
                command: "docker",
                args: new[] { "exec", containerId, "cat", fullPath },
                cancellationToken: cancellationToken);

            if (!result.Success) {
                throw new InvalidOperationException($"Failed to read file {filePath}: {result.Error}");
            }

            // cat command output includes any newlines in the file, but docker exec may add a trailing newline
            // We need to preserve the exact content, so we only remove the final trailing newline if present
            var output = result.Output;
            if (output.EndsWith('\n')) {
                output = output[..^1];
            }
            return output;
        }

        public async Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
            var fullPath = Path.Join(WorkDir, filePath.TrimStart('/'));

            // Create parent directory if needed
            // For container paths, use forward slashes regardless of host OS
            var lastSlash = fullPath.LastIndexOf('/');
            // Check if there's a directory path (lastSlash > 0 excludes root "/" and not-found -1)
            var directory = lastSlash > 0 ? fullPath[..lastSlash] : null;
            if (!string.IsNullOrEmpty(directory) && directory != WorkDir) {
                await _commandExecutor.ExecuteCommandAsync(
                    workingDirectory: Directory.GetCurrentDirectory(),
                    command: "docker",
                    args: new[] { "exec", containerId, "mkdir", "-p", directory },
                    cancellationToken: cancellationToken);
            }

            // Use base64 encoding to safely transfer content without shell injection risks
            // Base64 only produces alphanumeric characters plus '+', '/', and '=', so it's safe to use in shell commands
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var base64Content = Convert.ToBase64String(bytes);

            var result = await _commandExecutor.ExecuteCommandAsync(
                workingDirectory: Directory.GetCurrentDirectory(),
                command: "docker",
                args: new[] {
                    "exec",
                    containerId,
                    "sh",
                    "-c",
                    $"echo '{base64Content}' | base64 -d > {fullPath}"
                },
                cancellationToken: cancellationToken);

            if (!result.Success) {
                throw new InvalidOperationException($"Failed to write file {filePath}: {result.Error}");
            }

            _logger.LogInformation("Wrote file {FilePath} in container {ContainerId}", filePath, containerId);
        }

        public Task CommitAndPushAsync(
            string containerId,
            string commitMessage,
            string owner,
            string repo,
            string branch,
            string token,
            CancellationToken cancellationToken = default) {
            // No-op for tests - we don't push to GitHub in tests
            _logger.LogInformation("Skipping commit and push in test container {ContainerId}", containerId);
            return Task.CompletedTask;
        }

        public async Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default) {
            _logger.LogInformation("Cleaning up test container {ContainerId}", containerId);

            // Stop the container - attempt even if it fails
            var stopResult = await _commandExecutor.ExecuteCommandAsync(
                workingDirectory: Directory.GetCurrentDirectory(),
                command: "docker",
                args: new[] { "stop", containerId },
                cancellationToken: cancellationToken);

            if (!stopResult.Success) {
                _logger.LogWarning("Failed to stop container {ContainerId}: {Error}", containerId, stopResult.Error);
            }

            // Remove the container - attempt even if stop failed
            var removeResult = await _commandExecutor.ExecuteCommandAsync(
                workingDirectory: Directory.GetCurrentDirectory(),
                command: "docker",
                args: new[] { "rm", containerId },
                cancellationToken: cancellationToken);

            if (!removeResult.Success) {
                _logger.LogWarning("Failed to remove container {ContainerId}: {Error}", containerId, removeResult.Error);
            }
            else {
                _logger.LogInformation("Cleaned up test container {ContainerId}", containerId);
            }
        }

        public Task CreateDirectoryAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
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
            return Task.CompletedTask;
        }

        public Task<List<string>> ListContentsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<string>());
        }

        public Task<BuildToolsStatus> VerifyBuildToolsAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.FromResult(new BuildToolsStatus {
                DotnetAvailable = true,
                NpmAvailable = true,
                GradleAvailable = true,
                MavenAvailable = true,
                GoAvailable = true,
                CargoAvailable = true,
                MissingTools = []
            });
        }
    }
}
