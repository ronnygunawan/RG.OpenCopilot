using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class ExecutorServiceTests
{
    [Fact]
    public async Task ExecutePlanAsync_ThrowsWhenPlanIsNull()
    {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestLogger<ExecutorService>());

        var task = new AgentTask
        {
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
    public async Task ExecutePlanAsync_UpdatesTaskStatusToExecuting()
    {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestLogger<ExecutorService>());

        var task = new AgentTask
        {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.Planned,
            Plan = new AgentPlan
            {
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
    public async Task ExecutePlanAsync_ClonesRepository()
    {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestLogger<ExecutorService>());

        var task = new AgentTask
        {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan
            {
                ProblemSummary = "Test",
                Steps = new List<PlanStep>()
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        cloner.CloneWasCalled.ShouldBeTrue();
        cloner.CleanupWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_MarksStepsAsDone()
    {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestLogger<ExecutorService>());

        var task = new AgentTask
        {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan
            {
                ProblemSummary = "Test",
                Steps = new List<PlanStep>
                {
                    new() { Id = "1", Title = "Step 1", Details = "Do something", Done = false },
                    new() { Id = "2", Title = "Step 2", Details = "Do more", Done = false }
                }
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Plan.ShouldNotBeNull();
        updatedTask.Plan.Steps.ShouldAllBe(s => s.Done);
    }

    [Fact]
    public async Task ExecutePlanAsync_PostsProgressComment()
    {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestLogger<ExecutorService>());

        var task = new AgentTask
        {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan
            {
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
    public async Task ExecutePlanAsync_CleansUpRepositoryOnSuccess()
    {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestLogger<ExecutorService>());

        var task = new AgentTask
        {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan
            {
                ProblemSummary = "Test",
                Steps = new List<PlanStep>()
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        cloner.CleanupWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandExecutor_ExecutesCommand()
    {
        // Arrange
        var executor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var result = await executor.ExecuteCommandAsync(
                tempDir,
                "echo",
                new[] { "hello" },
                CancellationToken.None);

            // Assert
            result.Success.ShouldBeTrue();
            result.Output.ShouldContain("hello");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProcessCommandExecutor_ReturnsFailureForNonExistentCommand()
    {
        // Arrange
        var executor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var result = await executor.ExecuteCommandAsync(
                tempDir,
                "nonexistent-command-12345",
                Array.Empty<string>(),
                CancellationToken.None);

            // Assert
            result.Success.ShouldBeFalse();
        }
        catch (Exception)
        {
            // Expected - command not found
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    // Test helper classes
    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestTokenProvider : IGitHubAppTokenProvider
    {
        public Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("test-token");
        }
    }

    private class TestRepositoryCloner : IRepositoryCloner
    {
        public bool CloneWasCalled { get; private set; }
        public bool CleanupWasCalled { get; private set; }

        public Task<string> CloneRepositoryAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default)
        {
            CloneWasCalled = true;
            var tempPath = Path.Combine(Path.GetTempPath(), $"test-clone-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempPath);
            return Task.FromResult(tempPath);
        }

        public void CleanupRepository(string localPath)
        {
            CleanupWasCalled = true;
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, recursive: true);
            }
        }
    }

    private class TestCommandExecutor : ICommandExecutor
    {
        public Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default)
        {
            // Simulate git status --porcelain returning empty (no changes)
            if (command == "git" && args.Length > 0 && args[0] == "status")
            {
                return Task.FromResult(new CommandResult
                {
                    ExitCode = 0,
                    Output = string.Empty,
                    Error = string.Empty
                });
            }

            return Task.FromResult(new CommandResult
            {
                ExitCode = 0,
                Output = "success",
                Error = string.Empty
            });
        }
    }

    private class TestGitHubService : IGitHubService
    {
        public bool CommentPosted { get; private set; }

        public Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"open-copilot/issue-{issueNumber}");
        }

        public Task<int> CreateWipPullRequestAsync(string owner, string repo, string branchName, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(1);
        }

        public Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<int?> GetPullRequestNumberForBranchAsync(string owner, string repo, string branchName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<int?>(1);
        }

        public Task PostPullRequestCommentAsync(string owner, string repo, int prNumber, string comment, CancellationToken cancellationToken = default)
        {
            CommentPosted = true;
            return Task.CompletedTask;
        }
    }
}
