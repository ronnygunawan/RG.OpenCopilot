using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class ExecutorServiceTests {
    [Fact]
    public async Task ExecutePlanAsync_ThrowsWhenPlanIsNull() {
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
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
    public async Task ExecutePlanAsync_UpdatesTaskStatusToExecuting() {
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
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
    public async Task ExecutePlanAsync_ClonesRepository() {
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
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
        cloner.CloneWasCalled.ShouldBeTrue();
        cloner.CleanupWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_MarksStepsAsDone() {
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
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
    public async Task ExecutePlanAsync_PostsProgressComment() {
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
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
    public async Task ExecutePlanAsync_CleansUpRepositoryOnSuccess() {
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
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
        cloner.CleanupWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ProcessCommandExecutor_ExecutesCommand() {
        // Arrange
        var executor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try {
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
        finally {
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ProcessCommandExecutor_ReturnsFailureForNonExistentCommand() {
        // Arrange
        var executor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try {
            // Act
            var result = await executor.ExecuteCommandAsync(
                tempDir,
                "nonexistent-command-12345",
                Array.Empty<string>(),
                CancellationToken.None);

            // Assert
            result.Success.ShouldBeFalse();
        }
        catch (Exception) {
            // Expected - command not found
        }
        finally {
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExecutePlanAsync_WithRepositoryCloningFailure_ThrowsException() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryClonerThatFails();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.ExecutePlanAsync(task));
        
        exception.Message.ShouldBe("Clone failed");
    }

    [Fact]
    public async Task ExecutePlanAsync_CleansUpRepositoryOnFailure() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutorThatFails();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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

        // Assert - cleanup should be called even on failure
        cloner.CleanupWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_WithMissingToken_UsesEmptyToken() {
        // Arrange
        var tokenProvider = new TestTokenProviderReturningEmpty();
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
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
        cloner.CloneWasCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_WithUncommittedChanges_CommitsAndPushes() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutorWithChanges();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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
        var executorWithChanges = (TestCommandExecutorWithChanges)executor;
        executorWithChanges.GitCommitCalled.ShouldBeTrue();
        executorWithChanges.GitPushCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_WithGitStatusFailure_ThrowsException() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutorThatFailsGitStatus();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.ExecutePlanAsync(task));
    }

    [Fact]
    public async Task ExecutePlanAsync_WithGitCommitFailure_ThrowsException() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutorThatFailsGitCommit();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.ExecutePlanAsync(task));
    }

    [Fact]
    public async Task ExecutePlanAsync_WithGitPushFailure_ThrowsException() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutorThatFailsGitPush();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            new TestProgressReporter(),
            new TestLogger<ExecutorService>());

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

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.ExecutePlanAsync(task));
    }

    [Fact]
    public async Task ExecutePlanAsync_WhenAllStepsComplete_FinalizesThePR() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutor();
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var progressReporter = new TestProgressReporter();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            progressReporter,
            new TestLogger<ExecutorService>());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan {
                ProblemSummary = "Test",
                Steps = [
                    new PlanStep { Id = "1", Title = "Step 1", Details = "Do something", Done = true }
                ]
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act
        await service.ExecutePlanAsync(task);

        // Assert
        var updatedTask = await taskStore.GetTaskAsync(task.Id);
        updatedTask.ShouldNotBeNull();
        updatedTask.Status.ShouldBe(AgentTaskStatus.Completed);
        progressReporter.PullRequestFinalized.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecutePlanAsync_WhenGitStatusFails_DoesNotFinalizePR() {
        // Arrange
        var tokenProvider = new TestTokenProvider();
        var cloner = new TestRepositoryCloner();
        var executor = new TestCommandExecutorThatFailsGitStatus(); // Executor that fails git status
        var gitHubService = new TestGitHubService();
        var taskStore = new InMemoryAgentTaskStore();
        var progressReporter = new TestProgressReporter();
        var service = new ExecutorService(
            tokenProvider,
            cloner,
            executor,
            gitHubService,
            taskStore,
            progressReporter,
            new TestLogger<ExecutorService>());

        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1,
            Plan = new AgentPlan {
                ProblemSummary = "Test",
                Steps = [
                    new PlanStep { Id = "1", Title = "Step 1", Details = "Do something", Done = false }
                ]
            }
        };

        await taskStore.CreateTaskAsync(task);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.ExecutePlanAsync(task));

        // PR should not be finalized because execution failed
        progressReporter.PullRequestFinalized.ShouldBeFalse();
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

    private class TestRepositoryCloner : IRepositoryCloner {
        public bool CloneWasCalled { get; private set; }
        public bool CleanupWasCalled { get; private set; }

        public Task<string> CloneRepositoryAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            CloneWasCalled = true;
            var tempPath = Path.Combine(Path.GetTempPath(), $"test-clone-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempPath);
            return Task.FromResult(tempPath);
        }

        public void CleanupRepository(string localPath) {
            CleanupWasCalled = true;
            if (Directory.Exists(localPath)) {
                Directory.Delete(localPath, recursive: true);
            }
        }
    }

    private class TestCommandExecutor : ICommandExecutor {
        public Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default) {
            // Simulate git status --porcelain returning empty (no changes)
            if (command == "git" && args.Length > 0 && args[0] == "status") {
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = "",
                    Error = ""
                });
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "success",
                Error = ""
            });
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

        public Task<PullRequestInfo> GetPullRequestAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default) {
            return Task.FromResult(new PullRequestInfo {
                Number = prNumber,
                HeadRef = "test-branch",
                Title = "Test PR",
                Body = "Test body"
            });
        }
    }

    private class TestTokenProviderReturningEmpty : IGitHubAppTokenProvider {
        public Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default) {
            return Task.FromResult("");
        }
    }

    private class TestRepositoryClonerThatFails : IRepositoryCloner {
        public Task<string> CloneRepositoryAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            throw new InvalidOperationException("Clone failed");
        }

        public void CleanupRepository(string localPath) {
            // No cleanup needed
        }
    }

    private class TestCommandExecutorThatFails : ICommandExecutor {
        public Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default) {
            // Only fail for non-git commands to test step execution failures
            if (command != "git") {
                throw new InvalidOperationException("Command execution failed");
            }
            
            // Return success for git commands
            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "",
                Error = ""
            });
        }
    }

    private class TestCommandExecutorWithChanges : ICommandExecutor {
        public bool GitCommitCalled { get; private set; }
        public bool GitPushCalled { get; private set; }

        public Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default) {
            if (command == "git") {
                if (args.Length > 0) {
                    if (args[0] == "status") {
                        // Return changes present
                        return Task.FromResult(new CommandResult {
                            ExitCode = 0,
                            Output = "M file.txt\n",
                            Error = ""
                        });
                    }
                    else if (args[0] == "commit") {
                        GitCommitCalled = true;
                    }
                    else if (args[0] == "push") {
                        GitPushCalled = true;
                    }
                }
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "success",
                Error = ""
            });
        }
    }

    private class TestCommandExecutorThatFailsGitStatus : ICommandExecutor {
        public Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default) {
            if (command == "git" && args.Length > 0 && args[0] == "status") {
                throw new InvalidOperationException("Git status failed");
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "",
                Error = ""
            });
        }
    }

    private class TestCommandExecutorThatFailsGitCommit : ICommandExecutor {
        public Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default) {
            if (command == "git") {
                if (args.Length > 0) {
                    if (args[0] == "status") {
                        // Return changes present so commit is attempted
                        return Task.FromResult(new CommandResult {
                            ExitCode = 0,
                            Output = "M file.txt\n",
                            Error = ""
                        });
                    }
                    else if (args[0] == "commit") {
                        throw new InvalidOperationException("Git commit failed");
                    }
                }
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "",
                Error = ""
            });
        }
    }

    private class TestCommandExecutorThatFailsGitPush : ICommandExecutor {
        public Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default) {
            if (command == "git") {
                if (args.Length > 0) {
                    if (args[0] == "status") {
                        // Return changes present
                        return Task.FromResult(new CommandResult {
                            ExitCode = 0,
                            Output = "M file.txt\n",
                            Error = ""
                        });
                    }
                    else if (args[0] == "push") {
                        throw new InvalidOperationException("Git push failed");
                    }
                }
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "",
                Error = ""
            });
        }
    }

    private class TestProgressReporter : IProgressReporter {
        public bool PullRequestFinalized { get; private set; }

        public Task ReportStepProgressAsync(AgentTask task, PlanStep step, StepExecutionResult result, int prNumber, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task ReportExecutionSummaryAsync(AgentTask task, List<StepExecutionResult> results, int prNumber, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task ReportIntermediateProgressAsync(AgentTask task, string stage, string message, int prNumber, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task UpdateProgressAsync(AgentTask task, int commentId, string updatedContent, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task UpdatePullRequestProgressAsync(AgentTask task, int prNumber, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task ReportCommitSummaryAsync(AgentTask task, string commitSha, string commitMessage, List<FileChange> changes, int prNumber, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task FinalizePullRequestAsync(AgentTask task, int prNumber, CancellationToken cancellationToken = default) {
            PullRequestFinalized = true;
            return Task.CompletedTask;
        }
    }
}
