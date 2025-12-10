using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

public class BackgroundJobProcessingTests {
    [Fact]
    public async Task JobQueue_EnqueueAndDequeue_WorksCorrectly() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);

        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test payload"
        };

        // Act
        var enqueued = await queue.EnqueueAsync(job);
        var dequeued = await queue.DequeueAsync();

        // Assert
        enqueued.ShouldBeTrue();
        dequeued.ShouldNotBeNull();
        dequeued.Type.ShouldBe("TestJob");
        dequeued.Payload.ShouldBe("test payload");
    }

    [Fact]
    public async Task JobQueue_DequeueFromEmptyQueue_ReturnsNullWhenCancelled() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var result = await queue.DequeueAsync(cancellationToken: cts.Token);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task JobQueue_WithPrioritization_DequeuesHighestPriorityFirst() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = true
        };
        var queue = new ChannelJobQueue(options);

        var lowPriorityJob = new BackgroundJob {
            Type = "LowPriority",
            Payload = "low",
            Priority = 1
        };

        var highPriorityJob = new BackgroundJob {
            Type = "HighPriority",
            Payload = "high",
            Priority = 10
        };

        var mediumPriorityJob = new BackgroundJob {
            Type = "MediumPriority",
            Payload = "medium",
            Priority = 5
        };

        // Act
        await queue.EnqueueAsync(lowPriorityJob);
        await queue.EnqueueAsync(highPriorityJob);
        await queue.EnqueueAsync(mediumPriorityJob);

        // Give the queue a moment to stabilize
        await Task.Delay(100);

        var first = await queue.DequeueAsync();
        var second = await queue.DequeueAsync();
        var third = await queue.DequeueAsync();

        // Assert
        first.ShouldNotBeNull();
        first.Type.ShouldBe("HighPriority");

        second.ShouldNotBeNull();
        second.Type.ShouldBe("MediumPriority");

        third.ShouldNotBeNull();
        third.Type.ShouldBe("LowPriority");
    }

    [Fact]
    public async Task JobDispatcher_DispatchesJobSuccessfully() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var logger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), logger);

        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        dispatcher.RegisterHandler(handler.Object);

        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test"
        };

        // Act
        var result = await dispatcher.DispatchAsync(job);

        // Assert
        result.ShouldBeTrue();
        queue.Count.ShouldBe(1);
    }

    [Fact]
    public async Task JobDispatcher_FailsToDispatchUnregisteredJobType() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var logger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), logger);

        var job = new BackgroundJob {
            Type = "UnregisteredJob",
            Payload = "test"
        };

        // Act
        var result = await dispatcher.DispatchAsync(job);

        // Assert
        result.ShouldBeFalse();
        queue.Count.ShouldBe(0);
    }

    [Fact]
    public async Task JobDispatcher_CancelNonExistentJob_ReturnsFalse() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var logger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), logger);

        // Act
        var cancelled = dispatcher.CancelJob("non-existent-job");

        // Assert
        cancelled.ShouldBeFalse();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task JobResult_CreateSuccess_SetsPropertiesCorrectly() {
        // Arrange & Act
        var result = JobResult.CreateSuccess(data: "test data");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldBe("test data");
        result.ErrorMessage.ShouldBeNull();
        result.Exception.ShouldBeNull();
        result.ShouldRetry.ShouldBeFalse();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task JobResult_CreateFailure_SetsPropertiesCorrectly() {
        // Arrange
        var exception = new Exception("Test exception");

        // Act
        var result = JobResult.CreateFailure(
            errorMessage: "Test error",
            exception: exception,
            shouldRetry: true);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Test error");
        result.Exception.ShouldBe(exception);
        result.ShouldRetry.ShouldBeTrue();

        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExecutePlanJobHandler_ExecutesSuccessfully() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var logger = new TestLogger<ExecutePlanJobHandler>();

        var handler = new ExecutePlanJobHandler(
            taskStore,
            executorService.Object,
            new BackgroundJobOptions(),
            new FakeTimeProvider(),
            logger);

        // Create a task with a plan
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.Planned,
            Plan = new AgentPlan {
                ProblemSummary = "Test problem",
                Steps = new List<PlanStep>()
            }
        };
        await taskStore.CreateTaskAsync(task);

        executorService
            .Setup(e => e.ExecutePlanAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = """{"TaskId":"test/repo/issues/1"}"""
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();
        executorService.Verify(e => e.ExecutePlanAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()), Times.Once);

        var updatedTask = await taskStore.GetTaskAsync("test/repo/issues/1");
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Completed);
    }

    [Fact]
    public async Task ExecutePlanJobHandler_FailsWhenTaskNotFound() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var logger = new TestLogger<ExecutePlanJobHandler>();

        var handler = new ExecutePlanJobHandler(
            taskStore,
            executorService.Object,
            new BackgroundJobOptions(),
            new FakeTimeProvider(),
            logger);

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = """{"TaskId":"nonexistent/task"}"""
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("not found");
        result.ShouldRetry.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecutePlanJobHandler_FailsWhenTaskHasNoPlan() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var logger = new TestLogger<ExecutePlanJobHandler>();

        var handler = new ExecutePlanJobHandler(
            taskStore,
            executorService.Object,
            new BackgroundJobOptions(),
            new FakeTimeProvider(),
            logger);

        // Create a task without a plan
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = """{"TaskId":"test/repo/issues/1"}"""
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("no plan");
        result.ShouldRetry.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecutePlanJobHandler_HandlesExecutionException() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var logger = new TestLogger<ExecutePlanJobHandler>();

        var handler = new ExecutePlanJobHandler(
            taskStore,
            executorService.Object,
            new BackgroundJobOptions(),
            new FakeTimeProvider(),
            logger);

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.Planned,
            Plan = new AgentPlan {
                ProblemSummary = "Test problem",
                Steps = new List<PlanStep>()
            }
        };
        await taskStore.CreateTaskAsync(task);

        executorService
            .Setup(e => e.ExecutePlanAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Execution failed"));

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = """{"TaskId":"test/repo/issues/1"}"""
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("Execution failed");
        result.ShouldRetry.ShouldBeTrue();
        result.Exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecutePlanJobHandler_HandlesCancellation() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var logger = new TestLogger<ExecutePlanJobHandler>();

        var handler = new ExecutePlanJobHandler(
            taskStore,
            executorService.Object,
            new BackgroundJobOptions(),
            new FakeTimeProvider(),
            logger);

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.Planned,
            Plan = new AgentPlan {
                ProblemSummary = "Test problem",
                Steps = new List<PlanStep>()
            }
        };
        await taskStore.CreateTaskAsync(task);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        executorService
            .Setup(e => e.ExecutePlanAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = """{"TaskId":"test/repo/issues/1"}"""
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => {
            await handler.ExecuteAsync(job, cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task WebhookHandler_DispatchesGeneratePlanJob() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new TestJobDispatcher();
        var jobStatusStore = new TestJobStatusStore();

        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher,
            jobStatusStore,
            new TestLogger<WebhookHandler>());

        var payload = new GitHubIssueEventPayload {
            Action = "labeled",
            Label = new GitHubLabel { Name = "copilot-assisted" },
            Issue = new GitHubIssue { Number = 1, Title = "Test Issue", Body = "Test body" },
            Repository = new GitHubRepository { Name = "test", Full_Name = "owner/test", Owner = new GitHubOwner { Login = "owner" } },
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleIssuesEventAsync(payload);

        // Assert
        jobDispatcher.JobDispatched.ShouldBeTrue();
        jobDispatcher.LastDispatchedJob.ShouldNotBeNull();
        jobDispatcher.LastDispatchedJob.Type.ShouldBe(GeneratePlanJobHandler.JobTypeName);
        jobDispatcher.LastDispatchedJob.Metadata["TaskId"].ShouldBe("owner/test/issues/1");
    }

    [Fact]
    public async Task ChannelJobQueue_Complete_ClosesChannel() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);

        // Act
        queue.Complete();

        // Assert
        // Attempting to enqueue after Complete should return false
        var job = new BackgroundJob { Type = "Test", Payload = "test" };
        var result = await queue.EnqueueAsync(job);
        result.ShouldBeFalse();
    }

    [Fact]
    public void ChannelJobQueue_Dispose_DisposesResources() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);

        // Act & Assert - should not throw
        queue.Dispose();
    }

    [Fact]
    public async Task ChannelJobQueue_EnqueueAsync_HandlesChannelClosedException() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);
        queue.Complete();

        var job = new BackgroundJob { Type = "Test", Payload = "test" };

        // Act
        var result = await queue.EnqueueAsync(job);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ChannelJobQueue_DequeueAsync_WithPrioritizationDisabled_ReturnsFirstJob() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);

        var firstJob = new BackgroundJob { Type = "First", Payload = "first", Priority = 1 };
        var secondJob = new BackgroundJob { Type = "Second", Payload = "second", Priority = 10 };

        await queue.EnqueueAsync(firstJob);
        await queue.EnqueueAsync(secondJob);

        // Act
        var dequeuedFirst = await queue.DequeueAsync();
        var dequeuedSecond = await queue.DequeueAsync();

        // Assert - should return in FIFO order when prioritization is disabled
        dequeuedFirst.ShouldNotBeNull();
        dequeuedFirst.Type.ShouldBe("First");

        dequeuedSecond.ShouldNotBeNull();
        dequeuedSecond.Type.ShouldBe("Second");
    }

    [Fact]
    public void JobDispatcher_RegisterHandler_DuplicateRegistration_LogsWarning() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var logger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), logger);

        var handler1 = new Mock<IJobHandler>();
        handler1.Setup(h => h.JobType).Returns("TestJob");

        var handler2 = new Mock<IJobHandler>();
        handler2.Setup(h => h.JobType).Returns("TestJob");

        // Act
        dispatcher.RegisterHandler(handler1.Object);
        dispatcher.RegisterHandler(handler2.Object); // Duplicate

        // Assert - both calls should complete without exception
        // The second registration should be ignored with a warning
        var retrievedHandler = dispatcher.GetHandler("TestJob");
        retrievedHandler.ShouldBe(handler1.Object); // First handler should remain
    }

    [Fact]
    public void JobDispatcher_GetHandler_NonExistentType_ReturnsNull() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var logger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), logger);

        // Act
        var handler = dispatcher.GetHandler("NonExistent");

        // Assert
        handler.ShouldBeNull();
    }

    [Fact]
    public async Task JobDispatcher_RemoveActiveJob_RemovesJobFromTracking() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var logger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), logger);

        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        dispatcher.RegisterHandler(handler.Object);

        var job = new BackgroundJob {
            Id = "test-job",
            Type = "TestJob",
            Payload = "test"
        };

        // Act
        await dispatcher.DispatchAsync(job);
        dispatcher.RemoveActiveJob("test-job");

        // Assert - job should no longer be in active jobs
        // Attempting to cancel should return false
        var cancelled = dispatcher.CancelJob("test-job");
        cancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecutePlanJobHandler_FailsWhenPayloadIsInvalid() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var logger = new TestLogger<ExecutePlanJobHandler>();

        var handler = new ExecutePlanJobHandler(
            taskStore,
            executorService.Object,
            new BackgroundJobOptions(),
            new FakeTimeProvider(),
            logger);

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = "invalid json{{"
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
        // JSON deserialization exceptions are caught and marked for retry
        result.ShouldRetry.ShouldBeTrue();
    }

    [Fact]
    public async Task BackgroundJob_CreateRetryJob_IncrementsRetryCount() {
        // Arrange
        var originalJob = new BackgroundJob {
            Id = "test-id",
            Type = "TestJob",
            Payload = "test",
            Priority = 5,
            MaxRetries = 3,
            RetryCount = 1,
            Metadata = new Dictionary<string, string> { ["Key"] = "Value" }
        };

        // Act
        var retryJob = originalJob.CreateRetryJob();

        // Assert
        retryJob.Id.ShouldBe(originalJob.Id);
        retryJob.Type.ShouldBe(originalJob.Type);
        retryJob.Payload.ShouldBe(originalJob.Payload);
        retryJob.Priority.ShouldBe(originalJob.Priority);
        retryJob.MaxRetries.ShouldBe(originalJob.MaxRetries);
        retryJob.RetryCount.ShouldBe(2); // Incremented
        retryJob.Metadata["Key"].ShouldBe("Value");

        await Task.CompletedTask;
    }

    [Fact]
    public async Task JobQueue_Count_ReturnsCorrectCount() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);

        // Act
        queue.Count.ShouldBe(0);

        await queue.EnqueueAsync(new BackgroundJob { Type = "Test1", Payload = "test" });
        await queue.EnqueueAsync(new BackgroundJob { Type = "Test2", Payload = "test" });

        // Assert
        queue.Count.ShouldBe(2);

        await queue.DequeueAsync();
        queue.Count.ShouldBe(1);
    }

    [Fact]
    public async Task JobDispatcher_DispatchAsync_FailsWhenEnqueueFails() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 1,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);
        queue.Complete(); // Close the queue

        var logger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), logger);

        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        dispatcher.RegisterHandler(handler.Object);

        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test"
        };

        // Act
        var result = await dispatcher.DispatchAsync(job);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task BackgroundJobProcessor_ProcessesJobsSuccessfully() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            EnableRetry = false
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var handlerExecuted = false;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                handlerExecuted = true;
                return JobResult.CreateSuccess();
            });
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test"
        };
        await dispatcher.DispatchAsync(job);
        
        // Give processor time to process the job
        await Task.Delay(500);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert
        handlerExecuted.ShouldBeTrue();
        handler.Verify(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BackgroundJobProcessor_HandlesJobWithNoHandler() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        // Enqueue a job without registering a handler
        var job = new BackgroundJob {
            Type = "UnknownJob",
            Payload = "test"
        };
        await queue.EnqueueAsync(job);
        
        // Give processor time to try processing
        await Task.Delay(500);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should not throw, just log error
        // Test passes if no exception is thrown
    }

    [Fact]
    public async Task BackgroundJobProcessor_RetriesFailedJobs() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            EnableRetry = true,
            RetryDelayMilliseconds = 100
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var executionCount = 0;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                executionCount++;
                return JobResult.CreateFailure("Test failure", shouldRetry: true);
            });
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test",
            MaxRetries = 2
        };
        await dispatcher.DispatchAsync(job);
        
        // Give processor time to process and retry
        await Task.Delay(1500);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should have executed 3 times (initial + 2 retries)
        executionCount.ShouldBe(3);
    }

    [Fact]
    public async Task BackgroundJobProcessor_DoesNotRetryWhenRetryDisabled() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            EnableRetry = false
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var executionCount = 0;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                executionCount++;
                return JobResult.CreateFailure("Test failure", shouldRetry: true);
            });
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test",
            MaxRetries = 3
        };
        await dispatcher.DispatchAsync(job);
        
        // Give processor time to process
        await Task.Delay(500);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should only execute once (no retries)
        executionCount.ShouldBe(1);
    }

    [Fact]
    public async Task BackgroundJobProcessor_DoesNotRetryWhenShouldRetryIsFalse() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            EnableRetry = true,
            RetryDelayMilliseconds = 100
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var executionCount = 0;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                executionCount++;
                return JobResult.CreateFailure("Test failure", shouldRetry: false);
            });
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test",
            MaxRetries = 3
        };
        await dispatcher.DispatchAsync(job);
        
        // Give processor time to process
        await Task.Delay(500);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should only execute once (no retries when shouldRetry is false)
        executionCount.ShouldBe(1);
    }

    [Fact]
    public async Task BackgroundJobProcessor_HandlesExceptionInHandler() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            EnableRetry = true,
            RetryDelayMilliseconds = 100
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var executionCount = 0;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionCount++)
            .ThrowsAsync(new InvalidOperationException("Test exception"));
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test",
            MaxRetries = 1
        };
        await dispatcher.DispatchAsync(job);
        
        // Give processor time to process and retry
        await Task.Delay(800);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should have executed twice (initial + 1 retry) due to exception
        executionCount.ShouldBe(2);
    }

    [Fact]
    public async Task BackgroundJobProcessor_ProcessesMultipleJobsConcurrently() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 2,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            EnableRetry = false
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var executionCount = 0;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .Returns(async () => {
                Interlocked.Increment(ref executionCount);
                await Task.Delay(200);
                return JobResult.CreateSuccess();
            });
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        // Enqueue multiple jobs
        for (int i = 0; i < 4; i++) {
            var job = new BackgroundJob {
                Type = "TestJob",
                Payload = $"test{i}"
            };
            await dispatcher.DispatchAsync(job);
        }
        
        // Give processor time to process jobs
        await Task.Delay(1500);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - all jobs should be executed
        executionCount.ShouldBe(4);
    }

    [Fact]
    public async Task BackgroundJobProcessor_HandlesOperationCanceledExceptionInMainLoop() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        // Cancel immediately
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should complete without throwing
        processorTask.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task BackgroundJobProcessor_WaitsForJobsToCompleteOnShutdown() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            ShutdownTimeoutSeconds = 2
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var jobCompleted = false;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .Returns(async () => {
                await Task.Delay(500);
                jobCompleted = true;
                return JobResult.CreateSuccess();
            });
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test"
        };
        await dispatcher.DispatchAsync(job);
        
        // Give job time to start
        await Task.Delay(100);
        
        // Stop processor
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - job should have completed before shutdown
        jobCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task BackgroundJobProcessor_Dispose_DisposesResources() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act & Assert - should not throw
        processor.Dispose();
        
        await Task.CompletedTask;
    }

    [Fact]
    public async Task BackgroundJobProcessor_HandlesJobCancellationDuringExecution() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            MaxQueueSize = 10,
            EnablePrioritization = false,
            EnableRetry = false
        };
        var queue = new ChannelJobQueue(options);
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var statusStore = new TestJobStatusStore();
        var dispatcher = new JobDispatcher(queue, statusStore, new TestJobDeduplicationService(), dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, new TestRetryPolicyCalculator(), new TestJobDeduplicationService(), options, new FakeTimeProvider(), processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            Payload = "test"
        };
        await dispatcher.DispatchAsync(job);
        
        // Give processor time to process
        await Task.Delay(300);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should complete without throwing
        handler.Verify(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Test helper classes
    private class TestGitHubService : IGitHubService {
        public bool BranchCreated { get; private set; }
        public bool PrCreated { get; private set; }

        public Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default) {
            BranchCreated = true;
            return Task.FromResult($"copilot/issue-{issueNumber}");
        }

        public Task<int> CreateWipPullRequestAsync(string owner, string repo, string headBranch, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default) {
            PrCreated = true;
            return Task.FromResult(1);
        }

        public Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<int?> CreatePullRequestAsync(string owner, string repo, string headBranch, string baseBranch, string title, string body, CancellationToken cancellationToken = default) {
            return Task.FromResult<int?>(1);
        }

        public Task PostPullRequestCommentAsync(string owner, string repo, int prNumber, string comment, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<PullRequestInfo> GetPullRequestAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default) {
            return Task.FromResult(new PullRequestInfo {
                Number = prNumber,
                HeadRef = "test-branch",
                Title = "Test PR",
                Body = "Test body"
            });
        }

        public Task<int?> GetPullRequestNumberForBranchAsync(string owner, string repo, string branchName, CancellationToken cancellationToken = default) {
            return Task.FromResult<int?>(1);
        }
    }

    private class TestRepositoryAnalyzer : IRepositoryAnalyzer {
        public Task<RepositoryAnalysis> AnalyzeAsync(string owner, string repo, CancellationToken cancellationToken = default) {
            return Task.FromResult(new RepositoryAnalysis {
                Languages = new Dictionary<string, long> { { "C#", 1000 } },
                KeyFiles = new List<string> { "README.md" },
                DetectedTestFramework = "xUnit",
                DetectedBuildTool = "dotnet",
                Summary = "C# project with dotnet and xUnit"
            });
        }
    }

    private class TestInstructionsLoader : IInstructionsLoader {
        public Task<string?> LoadInstructionsAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default) {
            return Task.FromResult<string?>(null);
        }
    }

    private class TestJobDispatcher : IJobDispatcher {
        public bool JobDispatched { get; private set; }
        public BackgroundJob? LastDispatchedJob { get; private set; }

        public Task<bool> DispatchAsync(BackgroundJob job, CancellationToken cancellationToken = default) {
            JobDispatched = true;
            LastDispatchedJob = job;
            return Task.FromResult(true);
        }

        public bool CancelJob(string jobId) {
            return true;
        }

        public void RegisterHandler(IJobHandler handler) {
        }
    }

    private class TestRetryPolicyCalculator : IRetryPolicyCalculator {
        public int CalculateRetryDelay(RetryPolicy policy, int retryCount) {
            return policy.BaseDelayMilliseconds;
        }

        public bool ShouldRetry(RetryPolicy policy, int retryCount, int maxRetries, bool shouldRetry) {
            return policy.Enabled && shouldRetry && retryCount < maxRetries;
        }
    }

    private class TestJobDeduplicationService : IJobDeduplicationService {
        public Task<string?> GetInFlightJobAsync(string idempotencyKey, CancellationToken cancellationToken = default) {
            return Task.FromResult<string?>(null);
        }

        public Task RegisterJobAsync(string jobId, string idempotencyKey, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task UnregisterJobAsync(string jobId, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task ClearAllAsync(CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }
    }

    private class TestJobStatusStore : IJobStatusStore {
        public Task SetStatusAsync(BackgroundJobStatusInfo statusInfo, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<BackgroundJobStatusInfo?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default) {
            return Task.FromResult<BackgroundJobStatusInfo?>(null);
        }

        public Task DeleteStatusAsync(string jobId, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsByStatusAsync(
            BackgroundJobStatus status,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsByTypeAsync(
            string jobType,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsBySourceAsync(
            string source,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<List<BackgroundJobStatusInfo>> GetJobsAsync(
            BackgroundJobStatus? status = null,
            string? jobType = null,
            string? source = null,
            int skip = 0,
            int take = 100,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<BackgroundJobStatusInfo>());
        }

        public Task<JobMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) {
            return Task.FromResult(new JobMetrics());
        }
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
