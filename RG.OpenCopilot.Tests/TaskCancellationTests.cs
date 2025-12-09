using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

public class TaskCancellationTests {
    [Fact]
    public void CancelJob_NonExistentJob_ReturnsFalse() {
        // Arrange
        var options = new BackgroundJobOptions { MaxConcurrency = 1 };
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var logger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, logger);

        // Act
        var cancelled = dispatcher.CancelJob("non-existent-job-id");

        // Assert
        cancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecutePlanJobHandler_CancelledJob_UpdatesTaskStatus() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var options = new BackgroundJobOptions();
        var logger = new TestLogger<ExecutePlanJobHandler>();

        var handler = new ExecutePlanJobHandler(
            taskStore,
            executorService.Object,
            options,
            logger);

        // Create a task
        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
            Status = AgentTaskStatus.Planned,
            Plan = new AgentPlan {
                ProblemSummary = "Test",
                Steps = new List<PlanStep> {
                    new() { Id = "1", Title = "Step 1", Details = "Details" }
                }
            }
        };
        await taskStore.CreateTaskAsync(task);

        // Setup executor to throw cancellation
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately
        
        executorService
            .Setup(e => e.ExecutePlanAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(new ExecutePlanJobPayload { TaskId = task.Id })
        };

        // Act
        var exception = await Should.ThrowAsync<OperationCanceledException>(async () => 
            await handler.ExecuteAsync(job, cts.Token));

        // Assert
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Cancelled);
        updatedTask.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task AgentTaskStatus_IncludesCancelledStatus() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
            Status = AgentTaskStatus.Cancelled
        };

        // Act
        await taskStore.CreateTaskAsync(task);
        var retrieved = await taskStore.GetTaskAsync(task.Id);

        // Assert
        retrieved.ShouldNotBeNull();
        retrieved.Status.ShouldBe(AgentTaskStatus.Cancelled);
    }

    [Fact]
    public async Task GetTasksByInstallationIdAsync_ReturnsTasksForInstallation() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        
        var task1 = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
            Status = AgentTaskStatus.Executing
        };
        
        var task2 = new AgentTask {
            Id = "test/repo/issues/2",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 2,
            InstallationId = 123,
            Status = AgentTaskStatus.Planned
        };
        
        var task3 = new AgentTask {
            Id = "other/repo/issues/1",
            RepositoryOwner = "other",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 456,
            Status = AgentTaskStatus.Executing
        };

        await taskStore.CreateTaskAsync(task1);
        await taskStore.CreateTaskAsync(task2);
        await taskStore.CreateTaskAsync(task3);

        // Act
        var tasks = await taskStore.GetTasksByInstallationIdAsync(123);

        // Assert
        tasks.Count.ShouldBe(2);
        tasks.ShouldAllBe(t => t.InstallationId == 123);
        tasks.ShouldContain(t => t.Id == "test/repo/issues/1");
        tasks.ShouldContain(t => t.Id == "test/repo/issues/2");
    }

    [Fact]
    public async Task GetTasksByInstallationIdAsync_NoTasks_ReturnsEmptyList() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();

        // Act
        var tasks = await taskStore.GetTasksByInstallationIdAsync(999);

        // Assert
        tasks.ShouldBeEmpty();
    }

    // Test helper class
    private sealed class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
