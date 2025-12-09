using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

public class TimeoutHandlingTests {
    [Fact]
    public async Task GeneratePlanJobHandler_WithTimeout_CancelsAfterTimeout() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var options = new BackgroundJobOptions {
            PlanTimeoutSeconds = 1 // 1 second timeout
        };
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            options,
            logger);

        // Create a task
        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
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

        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepositoryAnalysis { Summary = "Test repo" });

        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync("owner", "repo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Make planner service take too long
        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .Returns(async (AgentTaskContext context, CancellationToken ct) => {
                await Task.Delay(5000, ct); // 5 seconds - exceeds timeout
                return new AgentPlan {
                    ProblemSummary = "Test",
                    Steps = [],
                    Checklist = [],
                    Constraints = []
                };
            });

        var job = new BackgroundJob {
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(new GeneratePlanJobPayload {
                TaskId = task.Id,
                InstallationId = 123,
                RepositoryOwner = "owner",
                RepositoryName = "repo",
                IssueNumber = 1,
                IssueTitle = "Test Issue",
                IssueBody = "Test body",
                WebhookId = "test"
            })
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("timed out");
        result.ErrorMessage.ShouldContain("1 seconds");
    }

    [Fact]
    public async Task ExecutePlanJobHandler_WithTimeout_CancelsAfterTimeout() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var options = new BackgroundJobOptions {
            ExecutionTimeoutSeconds = 1 // 1 second timeout
        };
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

        // Setup executor to take too long
        executorService
            .Setup(e => e.ExecutePlanAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .Returns(async (AgentTask t, CancellationToken ct) => {
                await Task.Delay(5000, ct); // 5 seconds - exceeds timeout
            });

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(new ExecutePlanJobPayload { TaskId = task.Id })
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("timed out");
        result.ErrorMessage.ShouldContain("1 seconds");

        // Check task status was updated
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Failed);
    }

    [Fact]
    public async Task GeneratePlanJobHandler_NoTimeout_CompletesSuccessfully() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var plannerService = new Mock<IPlannerService>();
        var gitHubService = new Mock<IGitHubService>();
        var repositoryAnalyzer = new Mock<IRepositoryAnalyzer>();
        var instructionsLoader = new Mock<IInstructionsLoader>();
        var jobDispatcher = new Mock<IJobDispatcher>();
        var jobStatusStore = new InMemoryJobStatusStore();
        var options = new BackgroundJobOptions {
            PlanTimeoutSeconds = 0 // No timeout
        };
        var logger = new TestLogger<GeneratePlanJobHandler>();

        var handler = new GeneratePlanJobHandler(
            taskStore,
            plannerService.Object,
            gitHubService.Object,
            repositoryAnalyzer.Object,
            instructionsLoader.Object,
            jobDispatcher.Object,
            jobStatusStore,
            options,
            logger);

        // Create a task
        var task = new AgentTask {
            Id = "owner/repo/issues/1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            InstallationId = 123,
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

        gitHubService
            .Setup(g => g.UpdatePullRequestDescriptionAsync("owner", "repo", 42, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        repositoryAnalyzer
            .Setup(r => r.AnalyzeAsync("owner", "repo", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RepositoryAnalysis { Summary = "Test repo" });

        instructionsLoader
            .Setup(i => i.LoadInstructionsAsync("owner", "repo", 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        plannerService
            .Setup(p => p.CreatePlanAsync(It.IsAny<AgentTaskContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentPlan {
                ProblemSummary = "Test",
                Steps = [new PlanStep { Id = "1", Title = "Step 1", Details = "Details" }],
                Checklist = ["Item 1"],
                Constraints = []
            });

        jobDispatcher
            .Setup(j => j.DispatchAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var job = new BackgroundJob {
            Type = GeneratePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(new GeneratePlanJobPayload {
                TaskId = task.Id,
                InstallationId = 123,
                RepositoryOwner = "owner",
                RepositoryName = "repo",
                IssueNumber = 1,
                IssueTitle = "Test Issue",
                IssueBody = "Test body",
                WebhookId = "test"
            })
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNullOrEmpty();
    }

    [Fact]
    public async Task ExecutePlanJobHandler_NoTimeout_CompletesSuccessfully() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var executorService = new Mock<IExecutorService>();
        var options = new BackgroundJobOptions {
            ExecutionTimeoutSeconds = 0 // No timeout
        };
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

        executorService
            .Setup(e => e.ExecutePlanAsync(It.IsAny<AgentTask>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var job = new BackgroundJob {
            Type = ExecutePlanJobHandler.JobTypeName,
            Payload = JsonSerializer.Serialize(new ExecutePlanJobPayload { TaskId = task.Id })
        };

        // Act
        var result = await handler.ExecuteAsync(job);

        // Assert
        result.Success.ShouldBeTrue();
        result.ErrorMessage.ShouldBeNullOrEmpty();
    }

    // Test helper class
    private sealed class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
