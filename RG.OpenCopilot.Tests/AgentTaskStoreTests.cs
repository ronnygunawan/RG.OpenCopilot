using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class AgentTaskStoreTests {
    [Fact]
    public async Task GetTaskAsync_WithExistingTask_ReturnsTask() {
        // Arrange
        var store = new InMemoryAgentTaskStore();
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1
        };
        await store.CreateTaskAsync(task);

        // Act
        var result = await store.GetTaskAsync("test/repo/issues/1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("test/repo/issues/1");
    }

    [Fact]
    public async Task GetTaskAsync_WithNonExistentTask_ReturnsNull() {
        // Arrange
        var store = new InMemoryAgentTaskStore();

        // Act
        var result = await store.GetTaskAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateTaskAsync_WithNewTask_CreatesTask() {
        // Arrange
        var store = new InMemoryAgentTaskStore();
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1
        };

        // Act
        var result = await store.CreateTaskAsync(task);

        // Assert
        result.ShouldBe(task);
        var retrieved = await store.GetTaskAsync("test/repo/issues/1");
        retrieved.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateTaskAsync_WithDuplicateId_ThrowsException() {
        // Arrange
        var store = new InMemoryAgentTaskStore();
        var task1 = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1
        };
        var task2 = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 456,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1
        };
        await store.CreateTaskAsync(task1);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.CreateTaskAsync(task2));
        
        exception.Message.ShouldBe("Task with ID test/repo/issues/1 already exists");
    }

    [Fact]
    public async Task UpdateTaskAsync_WithExistingTask_UpdatesTask() {
        // Arrange
        var store = new InMemoryAgentTaskStore();
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await store.CreateTaskAsync(task);

        // Act
        task.Status = AgentTaskStatus.Planned;
        await store.UpdateTaskAsync(task);

        // Assert
        var result = await store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Status.ShouldBe(AgentTaskStatus.Planned);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNonExistentTask_CreatesTask() {
        // Arrange
        var store = new InMemoryAgentTaskStore();
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1
        };

        // Act
        await store.UpdateTaskAsync(task);

        // Assert
        var result = await store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Id.ShouldBe("test/repo/issues/1");
    }

    [Fact]
    public async Task ConcurrentOperations_HandleCorrectly() {
        // Arrange
        var store = new InMemoryAgentTaskStore();
        var tasks = Enumerable.Range(1, 100).Select(i => new AgentTask {
            Id = $"test/repo/issues/{i}",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = i
        }).ToList();

        // Act - Create tasks concurrently
        var createTasks = tasks.Select(task => store.CreateTaskAsync(task));
        await Task.WhenAll(createTasks);

        // Update tasks concurrently
        foreach (var task in tasks) {
            task.Status = AgentTaskStatus.Planned;
        }
        var updateTasks = tasks.Select(task => store.UpdateTaskAsync(task));
        await Task.WhenAll(updateTasks);

        // Assert - Retrieve tasks concurrently
        var getTasks = tasks.Select(task => store.GetTaskAsync(task.Id));
        var results = await Task.WhenAll(getTasks);
        
        results.Length.ShouldBe(100);
        results.All(r => r != null).ShouldBeTrue();
        results.All(r => r!.Status == AgentTaskStatus.Planned).ShouldBeTrue();
    }
}
