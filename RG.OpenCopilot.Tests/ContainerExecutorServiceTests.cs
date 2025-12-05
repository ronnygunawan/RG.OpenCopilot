using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class ContainerExecutorServiceTests {
    [Fact]
    public async Task ExecutePlanAsync_ThrowsWhenPlanIsNull() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var containerManager = new TestContainerManager();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ContainerExecutorService(
            tokenProvider,
            containerManager,
            gitHubService,
            taskStore,
            new TestLogger<ContainerExecutorService>());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = null
        };

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.ExecutePlanAsync(task));
    }

    [Fact]
    public async Task ExecutePlanAsync_CreatesAndCleansUpContainer() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var containerManager = new TestContainerManager();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ContainerExecutorService(
            tokenProvider,
            containerManager,
            gitHubService,
            taskStore,
            new TestLogger<ContainerExecutorService>());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan {
                ProblemSummary = "Test",
                Steps = new List<PlanStep>()
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        containerManager.ContainerCreated.ShouldBeTrue();
        containerManager.ContainerCleaned.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_UpdatesTaskStatusToExecuting() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var containerManager = new TestContainerManager();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ContainerExecutorService(
            tokenProvider,
            containerManager,
            gitHubService,
            taskStore,
            new TestLogger<ContainerExecutorService>());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.Planned,
            Plan = new AgentPlan {
                ProblemSummary = "Test",
                Steps = new List<PlanStep>
                {
                    new() { Id = "1", Title = "Step 1", Details = "Do something" }
                }
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Completed);
    }

    [Fact]
    public async Task ExecutePlanAsync_PostsProgressComment() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var containerManager = new TestContainerManager();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ContainerExecutorService(
            tokenProvider,
            containerManager,
            gitHubService,
            taskStore,
            new TestLogger<ContainerExecutorService>());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan {
                ProblemSummary = "Test",
                Steps = new List<PlanStep>
                {
                    new() { Id = "1", Title = "Step 1", Details = "Do something" }
                }
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        gitHubService.CommentPosted.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_CommitsChanges() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var containerManager = new TestContainerManager();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ContainerExecutorService(
            tokenProvider,
            containerManager,
            gitHubService,
            taskStore,
            new TestLogger<ContainerExecutorService>());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan {
                ProblemSummary = "Test",
                Steps = new List<PlanStep>
                {
                    new() { Id = "1", Title = "Step 1", Details = "Do something" }
                }
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        containerManager.CommitAndPushCalled.ShouldBeTrue();
    }

    // Test helper classes
    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestTokenProvider : IGitHubAppTokenProvider {
        public Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-token");
        }
    }

    private class TestContainerManager : IContainerManager {
        public bool ContainerCreated { get; private set; }
        public bool ContainerCleaned { get; private set; }
        public bool CommitAndPushCalled { get; private set; }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            ContainerCreated = true;
            return Task.FromResult("test-container-id");
        }

        public Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default) {
            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "success",
                Error = string.Empty
            });
        }

        public Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
            return Task.FromResult("file content");
        }

        public Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CommitAndPushAsync(string containerId, string commitMessage, string owner, string repo, string branch, string token, CancellationToken cancellationToken = default) {
            CommitAndPushCalled = true;
            return Task.CompletedTask;
        }

        public Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default) {
            ContainerCleaned = true;
            return Task.CompletedTask;
        }
    }

    private class TestGitHubService : IGitHubService {
        public bool CommentPosted { get; private set; }

        public Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default) {
            return Task.FromResult($"open-copilot/issue-{issueNumber}");
        }

        public Task<int> CreateWipPullRequestAsync(string owner, string repo, string branchName, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default) {
            return Task.FromResult(1);
        }

        public Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<int?> GetPullRequestNumberForBranchAsync(string owner, string repo, string branchName, CancellationToken cancellationToken = default) {
            return Task.FromResult<int?>(1);
        }

        public Task PostPullRequestCommentAsync(string owner, string repo, int prNumber, string comment, CancellationToken cancellationToken = default) {
            CommentPosted = true;
            return Task.CompletedTask;
        }
    }
}
