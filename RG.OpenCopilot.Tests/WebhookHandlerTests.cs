using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class WebhookHandlerTests {
    [Fact]
    public async Task HandleIssuesEventAsync_IgnoresNonLabeledActions() {
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
            Action = "opened",
            Issue = new GitHubIssue { Number = 1, Title = "Test", Body = "Test body" },
            Repository = new GitHubRepository { Name = "test", Full_Name = "owner/test", Owner = new GitHubOwner { Login = "owner" } },
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleIssuesEventAsync(payload);

        // Assert
        jobDispatcher.JobDispatched.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleIssuesEventAsync_EnqueuesJobForCopilotAssistedLabel() {
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
        var jobId = await handler.HandleIssuesEventAsync(payload);

        // Assert
        jobId.ShouldNotBeNullOrEmpty();
        jobDispatcher.JobDispatched.ShouldBeTrue();
        jobDispatcher.LastDispatchedJob.ShouldNotBeNull();
        jobDispatcher.LastDispatchedJob.Type.ShouldBe("GeneratePlan");
    }

    [Fact]
    public async Task HandleIssuesEventAsync_CreatesTaskWithCorrectDetails() {
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
            Issue = new GitHubIssue { Number = 42, Title = "Test Issue", Body = "Test body" },
            Repository = new GitHubRepository { Name = "test", Full_Name = "owner/test", Owner = new GitHubOwner { Login = "owner" } },
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleIssuesEventAsync(payload);

        // Assert
        var task = await taskStore.GetTaskAsync("owner/test/issues/42");
        task.ShouldNotBeNull();
        task.RepositoryOwner.ShouldBe("owner");
        task.RepositoryName.ShouldBe("test");
        task.IssueNumber.ShouldBe(42);
        task.InstallationId.ShouldBe(123);
        task.Status.ShouldBe(AgentTaskStatus.PendingPlanning);
    }

    [Fact]
    public async Task HandleIssuesEventAsync_SkipsWhenTaskAlreadyExists() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var jobDispatcher = new TestJobDispatcher();
        var jobStatusStore = new TestJobStatusStore();
        var handler = new WebhookHandler(
            taskStore,
            jobDispatcher,
            jobStatusStore,
            new TestLogger<WebhookHandler>());

        // Create an existing task
        var existingTask = new AgentTask {
            Id = "owner/test/issues/99",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "test",
            IssueNumber = 99,
            Status = AgentTaskStatus.Planned
        };
        await taskStore.CreateTaskAsync(task: existingTask);

        var payload = new GitHubIssueEventPayload {
            Action = "labeled",
            Label = new GitHubLabel { Name = "copilot-assisted" },
            Issue = new GitHubIssue { Number = 99, Title = "Updated Issue", Body = "Updated body" },
            Repository = new GitHubRepository { Name = "test", Full_Name = "owner/test", Owner = new GitHubOwner { Login = "owner" } },
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleIssuesEventAsync(payload: payload);

        // Assert
        // Should not dispatch a new job since task already exists
        jobDispatcher.JobDispatched.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleIssuesEventAsync_IgnoresUnlabeledAction() {
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
            Action = "unlabeled",
            Label = new GitHubLabel { Name = "copilot-assisted" },
            Issue = new GitHubIssue { Number = 1, Title = "Test", Body = "Test body" },
            Repository = new GitHubRepository { Name = "test", Full_Name = "owner/test", Owner = new GitHubOwner { Login = "owner" } },
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleIssuesEventAsync(payload: payload);

        // Assert
        jobDispatcher.JobDispatched.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleIssuesEventAsync_IgnoresNonCopilotAssistedLabel() {
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
            Label = new GitHubLabel { Name = "bug" },
            Issue = new GitHubIssue { Number = 1, Title = "Test", Body = "Test body" },
            Repository = new GitHubRepository { Name = "test", Full_Name = "owner/test", Owner = new GitHubOwner { Login = "owner" } },
            Installation = new GitHubInstallation { Id = 123 }
        };

        // Act
        await handler.HandleIssuesEventAsync(payload: payload);

        // Assert
        jobDispatcher.JobDispatched.ShouldBeFalse();
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestGitHubService : IGitHubService {
        public bool BranchCreated { get; private set; }
        public bool PrCreated { get; private set; }
        public bool PrUpdated { get; private set; }

        public Task<string> CreateWorkingBranchAsync(string owner, string repo, int issueNumber, CancellationToken cancellationToken = default) {
            BranchCreated = true;
            return Task.FromResult($"open-copilot/issue-{issueNumber}");
        }

        public Task<int> CreateWipPullRequestAsync(string owner, string repo, string branchName, int issueNumber, string issueTitle, string issueBody, CancellationToken cancellationToken = default) {
            PrCreated = true;
            return Task.FromResult(1);
        }

        public Task UpdatePullRequestDescriptionAsync(string owner, string repo, int prNumber, string title, string body, CancellationToken cancellationToken = default) {
            PrUpdated = true;
            return Task.CompletedTask;
        }

        public Task<int?> GetPullRequestNumberForBranchAsync(string owner, string repo, string branchName, CancellationToken cancellationToken = default) {
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
}
