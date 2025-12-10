using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class GitHubAppUninstallationTests {
    [Fact]
    public async Task HandleInstallationEvent_DeletedAction_CancelsActiveTasks() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<WebhookHandler>();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher.Object,
            jobStatusStore,
            timeProvider,
            logger,
            new TestAuditLogger(), new TestCorrelationIdProvider());

        // Create tasks for the installation
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
            Id = "test/repo/issues/3",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 3,
            InstallationId = 123,
            Status = AgentTaskStatus.Completed // Already completed - should not be cancelled
        };

        var task4 = new AgentTask {
            Id = "other/repo/issues/1",
            RepositoryOwner = "other",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 456, // Different installation
            Status = AgentTaskStatus.Executing
        };

        await taskStore.CreateTaskAsync(task1);
        await taskStore.CreateTaskAsync(task2);
        await taskStore.CreateTaskAsync(task3);
        await taskStore.CreateTaskAsync(task4);

        var payload = new GitHubInstallationEventPayload {
            Action = "deleted",
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleInstallationEventAsync(payload);

        // Assert
        var updatedTask1 = await taskStore.GetTaskAsync(task1.Id);
        updatedTask1.ShouldNotBeNull();
        updatedTask1.Status.ShouldBe(AgentTaskStatus.Cancelled);
        updatedTask1.CompletedAt.ShouldNotBeNull();

        var updatedTask2 = await taskStore.GetTaskAsync(task2.Id);
        updatedTask2.ShouldNotBeNull();
        updatedTask2.Status.ShouldBe(AgentTaskStatus.Cancelled);
        updatedTask2.CompletedAt.ShouldNotBeNull();

        var updatedTask3 = await taskStore.GetTaskAsync(task3.Id);
        updatedTask3.ShouldNotBeNull();
        updatedTask3.Status.ShouldBe(AgentTaskStatus.Completed); // Should not be changed

        var updatedTask4 = await taskStore.GetTaskAsync(task4.Id);
        updatedTask4.ShouldNotBeNull();
        updatedTask4.Status.ShouldBe(AgentTaskStatus.Executing); // Should not be changed
    }

    [Fact]
    public async Task HandleInstallationEvent_DeletedAction_CancelsActiveJobs() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<WebhookHandler>();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher.Object,
            jobStatusStore,
            timeProvider,
            logger,
            new TestAuditLogger(), new TestCorrelationIdProvider());

        // Create job statuses for the installation
        var job1 = new BackgroundJobStatusInfo {
            JobId = "job-1",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Processing,
            CreatedAt = timeProvider.GetUtcNow().DateTime,
            Metadata = new Dictionary<string, string> {
                ["InstallationId"] = "123",
                ["TaskId"] = "test/repo/issues/1"
            }
        };

        var job2 = new BackgroundJobStatusInfo {
            JobId = "job-2",
            JobType = "ExecutePlan",
            Status = BackgroundJobStatus.Queued,
            CreatedAt = timeProvider.GetUtcNow().DateTime,
            Metadata = new Dictionary<string, string> {
                ["InstallationId"] = "123",
                ["TaskId"] = "test/repo/issues/2"
            }
        };

        var job3 = new BackgroundJobStatusInfo {
            JobId = "job-3",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = timeProvider.GetUtcNow().DateTime,
            Metadata = new Dictionary<string, string> {
                ["InstallationId"] = "123",
                ["TaskId"] = "test/repo/issues/3"
            }
        };

        var job4 = new BackgroundJobStatusInfo {
            JobId = "job-4",
            JobType = "GeneratePlan",
            Status = BackgroundJobStatus.Processing,
            CreatedAt = timeProvider.GetUtcNow().DateTime,
            Metadata = new Dictionary<string, string> {
                ["InstallationId"] = "456", // Different installation
                ["TaskId"] = "other/repo/issues/1"
            }
        };

        await jobStatusStore.SetStatusAsync(job1);
        await jobStatusStore.SetStatusAsync(job2);
        await jobStatusStore.SetStatusAsync(job3);
        await jobStatusStore.SetStatusAsync(job4);

        var payload = new GitHubInstallationEventPayload {
            Action = "deleted",
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleInstallationEventAsync(payload);

        // Assert
        jobDispatcher.Verify(j => j.CancelJob("job-1"), Times.Once);
        jobDispatcher.Verify(j => j.CancelJob("job-2"), Times.Once);
        jobDispatcher.Verify(j => j.CancelJob("job-3"), Times.Never); // Completed job
        jobDispatcher.Verify(j => j.CancelJob("job-4"), Times.Never); // Different installation
    }

    [Fact]
    public async Task HandleInstallationEvent_NonDeletedAction_IgnoresEvent() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<WebhookHandler>();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher.Object,
            jobStatusStore,
            timeProvider,
            logger,
            new TestAuditLogger(), new TestCorrelationIdProvider());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
            Status = AgentTaskStatus.Executing
        };
        await taskStore.CreateTaskAsync(task);

        var payload = new GitHubInstallationEventPayload {
            Action = "created", // Not deleted
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleInstallationEventAsync(payload);

        // Assert
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Executing); // Should not be changed

        jobDispatcher.Verify(j => j.CancelJob(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleInstallationEvent_NullInstallation_IgnoresEvent() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<WebhookHandler>();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher.Object,
            jobStatusStore,
            timeProvider,
            logger,
            new TestAuditLogger(), new TestCorrelationIdProvider());

        var payload = new GitHubInstallationEventPayload {
            Action = "deleted",
            Installation = null
        };

        // Act
        await handler.HandleInstallationEventAsync(payload);

        // Assert - should not throw and should not cancel anything
        jobDispatcher.Verify(j => j.CancelJob(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleInstallationEvent_NoTasksForInstallation_CompletesWithoutError() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<WebhookHandler>();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher.Object,
            jobStatusStore,
            timeProvider,
            logger,
            new TestAuditLogger(), new TestCorrelationIdProvider());

        var payload = new GitHubInstallationEventPayload {
            Action = "deleted",
            Installation = new GitHubInstallation { Id = 999 } // No tasks for this installation
        };

        // Act & Assert - should not throw
        await handler.HandleInstallationEventAsync(payload);
    }

    [Fact]
    public async Task HandleInstallationEvent_PendingPlanningTasks_GetsCancelled() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<WebhookHandler>();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher.Object,
            jobStatusStore,
            timeProvider,
            logger,
            new TestAuditLogger(), new TestCorrelationIdProvider());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        var payload = new GitHubInstallationEventPayload {
            Action = "deleted",
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleInstallationEventAsync(payload);

        // Assert
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Cancelled);
    }

    [Fact]
    public async Task HandleInstallationEvent_FailedTasks_NotCancelled() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<WebhookHandler>();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher.Object,
            jobStatusStore,
            timeProvider,
            logger,
            new TestAuditLogger(), new TestCorrelationIdProvider());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
            Status = AgentTaskStatus.Failed
        };
        await taskStore.CreateTaskAsync(task);

        var payload = new GitHubInstallationEventPayload {
            Action = "deleted",
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleInstallationEventAsync(payload);

        // Assert
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Failed); // Should not be changed
    }

    // Test helper class
    private sealed class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

}
