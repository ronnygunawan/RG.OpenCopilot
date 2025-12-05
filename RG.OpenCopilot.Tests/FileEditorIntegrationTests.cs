using Microsoft.Extensions.Logging;
using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for FileEditor using actual container operations
/// These tests require Docker to be available
/// </summary>
public class FileEditorIntegrationTests {
    [Fact(Skip = "Requires Docker")]
    public async Task CreateFileAsync_IntegrationTest_CreatesFileInContainer() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>());
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

    [Fact(Skip = "Requires Docker")]
    public async Task ModifyFileAsync_IntegrationTest_ModifiesExistingFile() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>());
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

    [Fact(Skip = "Requires Docker")]
    public async Task DeleteFileAsync_IntegrationTest_DeletesFile() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>());
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

    [Fact(Skip = "Requires Docker")]
    public async Task CompleteWorkflow_IntegrationTest_TracksMixedOperations() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>());
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

    [Fact(Skip = "Requires Docker")]
    public async Task CreateFileAsync_IntegrationTest_CreatesNestedDirectories() {
        // Arrange
        var containerManager = CreateRealContainerManager();
        var editor = new FileEditor(containerManager, CreateLogger<FileEditor>());
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
        return new DockerContainerManager(commandExecutor, CreateLogger<DockerContainerManager>());
    }

    private static ILogger<T> CreateLogger<T>() {
        using var loggerFactory = LoggerFactory.Create(builder => {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        return loggerFactory.CreateLogger<T>();
    }
}
