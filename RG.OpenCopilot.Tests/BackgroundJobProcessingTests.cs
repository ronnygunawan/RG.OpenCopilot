using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using Microsoft.Extensions.Logging;

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
        var dispatcher = new JobDispatcher(queue, logger);

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
        var dispatcher = new JobDispatcher(queue, logger);

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
        var dispatcher = new JobDispatcher(queue, logger);

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
            Type = "ExecutePlan",
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
            logger);

        var job = new BackgroundJob {
            Type = "ExecutePlan",
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
            Type = "ExecutePlan",
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
            Type = "ExecutePlan",
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
            Type = "ExecutePlan",
            Payload = """{"TaskId":"test/repo/issues/1"}"""
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => {
            await handler.ExecuteAsync(job, cancellationToken: cts.Token);
        });
    }

    [Fact]
    public async Task WebhookHandler_DispatchesExecutePlanJob() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var planner = new SimplePlannerService(new TestLogger<SimplePlannerService>());
        var gitHubService = new TestGitHubService();
        var repositoryAnalyzer = new TestRepositoryAnalyzer();
        var instructionsLoader = new TestInstructionsLoader();
        var jobDispatcher = new TestJobDispatcher();

        var handler = new WebhookHandler(
            taskStore,
            planner,
            gitHubService,
            repositoryAnalyzer,
            instructionsLoader,
            jobDispatcher,
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
        jobDispatcher.LastDispatchedJob.Type.ShouldBe("ExecutePlan");
        jobDispatcher.LastDispatchedJob.Metadata["TaskId"].ShouldBe("owner/test/issues/1");
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

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
