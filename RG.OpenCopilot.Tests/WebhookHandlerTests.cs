using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class WebhookHandlerTests {
    [Fact]
    public async Task HandleIssuesEventAsync_IgnoresNonLabeledActions() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var planner = new SimplePlannerService(new TestLogger<SimplePlannerService>());
        var gitHubService = new TestGitHubService();
        var repositoryAnalyzer = new TestRepositoryAnalyzer();
        var instructionsLoader = new TestInstructionsLoader();
        var handler = new WebhookHandler(
            taskStore,
            planner,
            gitHubService,
            repositoryAnalyzer,
            instructionsLoader,
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
        gitHubService.BranchCreated.ShouldBeFalse();
        gitHubService.PrCreated.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleIssuesEventAsync_CreatesBranchAndPrForCopilotAssistedLabel() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var planner = new SimplePlannerService(new TestLogger<SimplePlannerService>());
        var gitHubService = new TestGitHubService();
        var repositoryAnalyzer = new TestRepositoryAnalyzer();
        var instructionsLoader = new TestInstructionsLoader();
        var handler = new WebhookHandler(
            taskStore,
            planner,
            gitHubService,
            repositoryAnalyzer,
            instructionsLoader,
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
        gitHubService.BranchCreated.ShouldBeTrue();
        gitHubService.PrCreated.ShouldBeTrue();
        gitHubService.PrUpdated.ShouldBeTrue();
    }

    [Fact]
    public async Task HandleIssuesEventAsync_CreatesTaskWithCorrectDetails() {
        // Arrange
        var taskStore = new InMemoryAgentTaskStore();
        var planner = new SimplePlannerService(new TestLogger<SimplePlannerService>());
        var gitHubService = new TestGitHubService();
        var repositoryAnalyzer = new TestRepositoryAnalyzer();
        var instructionsLoader = new TestInstructionsLoader();
        var handler = new WebhookHandler(
            taskStore,
            planner,
            gitHubService,
            repositoryAnalyzer,
            instructionsLoader,
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
        task.Status.ShouldBe(AgentTaskStatus.Planned);
        task.Plan.ShouldNotBeNull();
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
}
