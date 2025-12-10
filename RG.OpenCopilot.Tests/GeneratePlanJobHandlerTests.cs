using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

public class GeneratePlanJobHandlerTests {
    [Fact]
    public async Task ExecuteAsync_HappyPath_ExecutesSuccessfully() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        // Create a task
        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        // Setup mocks
        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync("owner", "repo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("open-copilot/issue-1");

        gitHubService
            .Setup(g => g.CreateWipPullRequestAsync("owner", "repo", "open-copilot/issue-1", 1, "Test Issue", "Test body", It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var repoAnalysis = new RepositoryAnalysis {
            Summary = "Test repository",
            Languages = new Dictionary<string, long> { { "C#", 1000 } },
            KeyFiles = new List<string> { "Program.cs" },
            DetectedBuildTool = "dotnet",
            DetectedTestFramework = "xUnit"
        };
        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoAnalysis);

        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync("owner", "repo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("Custom instructions");

        var plan = new AgentPlan {
            ProblemSummary = "Test problem",
            Steps = new List<PlanStep> {
                new() { Title = "Step 1", Details = "Details 1" }
            },
            Checklist = new List<string> { "Task 1" },
            Constraints = new List<string> { "Constraint 1" }
        };
        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        gitHubService
            .Setup(g => g.UpdatePullRequestDescriptionAsync("owner", "repo", 42, "[WIP] Test Issue", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        jobDispatcher
            .Setup(j => j.DispatchAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var payload = new GeneratePlanJobPayload {
            TaskId = "owner/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test Issue",
            IssueBody = "Test body",
            WebhookId = "webhook-123"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string> {
                ["TaskId"] = "owner/repo/issues/1"
            }
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify task was updated with plan
        var updatedTask = await taskStore.GetTaskAsync("owner/repo/issues/1");
        updatedTask.ShouldNotBeNull();
        updatedTask.Plan.ShouldNotBeNull();
        updatedTask.Plan.ProblemSummary.ShouldBe("Test problem");
        updatedTask.Status.ShouldBe(AgentTaskStatus.Planned);

        // Verify branch was created
        gitHubService.Verify(g => g.CreateWorkingBranchAsync("owner", "repo", 1, It.IsAny<CancellationToken>()), Times.Once);

        // Verify PR was created
        gitHubService.Verify(g => g.CreateWipPullRequestAsync("owner", "repo", "open-copilot/issue-1", 1, "Test Issue", "Test body", It.IsAny<CancellationToken>()), Times.Once);

        // Verify PR was updated with plan
        gitHubService.Verify(g => g.UpdatePullRequestDescriptionAsync("owner", "repo", 42, "[WIP] Test Issue", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify execution job was dispatched
        jobDispatcher.Verify(j => j.DispatchAsync(It.Is<BackgroundJob>(b => b.Type == ExecutePlanJobHandler.JobTypeName), It.IsAny<CancellationToken>()), Times.Once);

        // Verify job status was updated
        var jobStatus = await jobStatusStore.GetStatusAsync("job-123");
        jobStatus.ShouldNotBeNull();
        jobStatus.Status.ShouldBe(BackgroundJobStatus.Completed);
        jobStatus.ResultData.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidPayload_ReturnsFailure() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = "invalid json",
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNullOrEmpty();
        // The error message will be from JsonSerializer exception
        result.ShouldRetry.ShouldBeTrue(); // Caught by outer catch, so retry is true

        // Verify job status was updated to Failed
        var jobStatus = await jobStatusStore.GetStatusAsync("job-123");
        jobStatus.ShouldNotBeNull();
        jobStatus.Status.ShouldBe(BackgroundJobStatus.Failed);
        jobStatus.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_TaskNotFound_ReturnsFailure() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        // Setup mocks for early steps
        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync("owner", "repo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync("open-copilot/issue-1");

        gitHubService
            .Setup(g => g.CreateWipPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var repoAnalysis = new RepositoryAnalysis {
            Summary = "Test repository"
        };
        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoAnalysis);

        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var plan = new AgentPlan {
            ProblemSummary = "Test",
            Steps = new List<PlanStep>(),
            Checklist = new List<string>(),
            Constraints = new List<string>()
        };
        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        var payload = new GeneratePlanJobPayload {
            TaskId = "nonexistent/task",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test",
            IssueBody = "Test"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("not found");
        result.ShouldRetry.ShouldBeFalse();

        // Verify job status was updated to Failed
        var jobStatus = await jobStatusStore.GetStatusAsync("job-123");
        jobStatus.ShouldNotBeNull();
        jobStatus.Status.ShouldBe(BackgroundJobStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCancelledException() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        // Setup mock to throw cancellation
        var cts = new CancellationTokenSource();
        cts.Cancel();

        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var payload = new GeneratePlanJobPayload {
            TaskId = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test",
            IssueBody = "Test"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string>()
        };

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(async () => await handler.ExecuteAsync(job, cts.Token));

        // Verify job status was updated to Cancelled
        var jobStatus = await jobStatusStore.GetStatusAsync("job-123");
        jobStatus.ShouldNotBeNull();
        jobStatus.Status.ShouldBe(BackgroundJobStatus.Cancelled);
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionDuringExecution_ReturnsFailureWithRetry() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        // Setup mock to throw exception
        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub API error"));

        var payload = new GeneratePlanJobPayload {
            TaskId = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test",
            IssueBody = "Test"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldBe("GitHub API error");
        result.ShouldRetry.ShouldBeTrue();
        result.Exception.ShouldNotBeNull();

        // Verify job status was updated to Failed
        var jobStatus = await jobStatusStore.GetStatusAsync("job-123");
        jobStatus.ShouldNotBeNull();
        jobStatus.Status.ShouldBe(BackgroundJobStatus.Failed);
        jobStatus.ErrorMessage.ShouldBe("GitHub API error");
    }

    [Fact]
    public async Task ExecuteAsync_RepositoryAnalyzerFails_ContinuesWithoutAnalysis() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("branch");

        gitHubService
            .Setup(g => g.CreateWipPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Repository analyzer throws exception
        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Analysis failed"));

        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var plan = new AgentPlan {
            ProblemSummary = "Test",
            Steps = new List<PlanStep>(),
            Checklist = new List<string>(),
            Constraints = new List<string>()
        };
        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        gitHubService
            .Setup(g => g.UpdatePullRequestDescriptionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        jobDispatcher
            .Setup(j => j.DispatchAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var payload = new GeneratePlanJobPayload {
            TaskId = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test",
            IssueBody = "Test"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify planner was called with null repository summary
        plannerService.Verify(p => p.CreatePlanAsync(
            It.Is<AgentTaskContext>(ctx => ctx.RepositorySummary == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_InstructionsLoaderFails_ContinuesWithoutInstructions() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("branch");

        gitHubService
            .Setup(g => g.CreateWipPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var repoAnalysis = new RepositoryAnalysis {
            Summary = "Test repository"
        };
        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(repoAnalysis);

        // Instructions loader throws exception
        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Instructions load failed"));

        var plan = new AgentPlan {
            ProblemSummary = "Test",
            Steps = new List<PlanStep>(),
            Checklist = new List<string>(),
            Constraints = new List<string>()
        };
        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        gitHubService
            .Setup(g => g.UpdatePullRequestDescriptionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        jobDispatcher
            .Setup(j => j.DispatchAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var payload = new GeneratePlanJobPayload {
            TaskId = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test",
            IssueBody = "Test"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify planner was called with null instructions
        plannerService.Verify(p => p.CreatePlanAsync(
            It.Is<AgentTaskContext>(ctx => ctx.InstructionsMarkdown == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutionJobDispatchFails_StillReturnsSuccess() {
        var timeProvider = new FakeTimeProvider();
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("branch");

        gitHubService
            .Setup(g => g.CreateWipPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepositoryAnalysis { Summary = "Test" });

        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var plan = new AgentPlan {
            ProblemSummary = "Test",
            Steps = new List<PlanStep>(),
            Checklist = new List<string>(),
            Constraints = new List<string>()
        };
        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        gitHubService
            .Setup(g => g.UpdatePullRequestDescriptionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Dispatcher fails to dispatch execution job
        jobDispatcher
            .Setup(j => j.DispatchAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var payload = new GeneratePlanJobPayload {
            TaskId = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test",
            IssueBody = "Test"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify job status is still Completed (dispatch failure is logged but doesn't fail the job)
        var jobStatus = await jobStatusStore.GetStatusAsync("job-123");
        jobStatus.ShouldNotBeNull();
        jobStatus.Status.ShouldBe(BackgroundJobStatus.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesJobStatusThroughAllStages() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            new BackgroundJobOptions(),
            timeProvider,
            logger);

        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await taskStore.CreateTaskAsync(task);

        // Setup mocks
        gitHubService
            .Setup(g => g.CreateWorkingBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("branch");

        gitHubService
            .Setup(g => g.CreateWipPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepositoryAnalysis { Summary = "Test" });

        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var plan = new AgentPlan {
            ProblemSummary = "Test",
            Steps = new List<PlanStep>(),
            Checklist = new List<string>(),
            Constraints = new List<string>()
        };
        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        gitHubService
            .Setup(g => g.UpdatePullRequestDescriptionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        jobDispatcher
            .Setup(j => j.DispatchAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Set initial status to Queued
        await jobStatusStore.SetStatusAsync(new BackgroundJobStatusInfo {
            JobId = "job-123",
            JobType = GeneratePlanJobHandler.JobTypeName,
            Status = BackgroundJobStatus.Queued,
            CreatedAt = timeProvider.GetUtcNow().DateTime,
            Metadata = new Dictionary<string, string>()
        });

        var payload = new GeneratePlanJobPayload {
            TaskId = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            IssueTitle = "Test",
            IssueBody = "Test"
        };

        var job = new BackgroundJob {
            Id = "job-123",
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(payload),
            Metadata = new Dictionary<string, string>()
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();

        var finalStatus = await jobStatusStore.GetStatusAsync("job-123");
        finalStatus.ShouldNotBeNull();
        finalStatus.Status.ShouldBe(BackgroundJobStatus.Completed);
        finalStatus.CreatedAt.ShouldNotBe(default);
        finalStatus.StartedAt.ShouldNotBeNull();
        finalStatus.CompletedAt.ShouldNotBeNull();
        finalStatus.ResultData.ShouldNotBeNullOrEmpty();
        finalStatus.ErrorMessage.ShouldBeNull();
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
