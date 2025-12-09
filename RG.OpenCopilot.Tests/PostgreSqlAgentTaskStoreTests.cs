using Microsoft.EntityFrameworkCore;
using RG.OpenCopilot.PRGenerationAgent.Execution.Models;
using RG.OpenCopilot.PRGenerationAgent.Planning.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class PostgreSqlAgentTaskStoreTests : IDisposable {
    private readonly AgentTaskDbContext _context;
    private readonly PostgreSqlAgentTaskStore _store;

    public PostgreSqlAgentTaskStoreTests() {
        // Use in-memory SQLite database for testing
        var options = new DbContextOptionsBuilder<AgentTaskDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new AgentTaskDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
        _store = new PostgreSqlAgentTaskStore(_context);
    }

    public void Dispose() {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task GetTaskAsync_WithExistingTask_ReturnsTask() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        // Act
        var result = await _store.GetTaskAsync("test/repo/issues/1");

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe("test/repo/issues/1");
        result.InstallationId.ShouldBe(123);
        result.RepositoryOwner.ShouldBe("test");
        result.RepositoryName.ShouldBe("repo");
        result.IssueNumber.ShouldBe(1);
        result.Status.ShouldBe(AgentTaskStatus.PendingPlanning);
    }

    [Fact]
    public async Task GetTaskAsync_WithNonExistentTask_ReturnsNull() {
        // Arrange & Act
        var result = await _store.GetTaskAsync("nonexistent");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CreateTaskAsync_WithNewTask_CreatesTask() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };

        // Act
        var result = await _store.CreateTaskAsync(task);

        // Assert
        result.ShouldBe(task);
        var retrieved = await _store.GetTaskAsync("test/repo/issues/1");
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe("test/repo/issues/1");
    }

    [Fact]
    public async Task CreateTaskAsync_WithDuplicateId_ThrowsException() {
        // Arrange
        var task1 = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        var task2 = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 456,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task1);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _store.CreateTaskAsync(task2));
        
        exception.Message.ShouldBe("Task with ID test/repo/issues/1 already exists");
    }

    [Fact]
    public async Task UpdateTaskAsync_WithExistingTask_UpdatesTask() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        // Act
        task.Status = AgentTaskStatus.Planned;
        await _store.UpdateTaskAsync(task);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Status.ShouldBe(AgentTaskStatus.Planned);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNonExistentTask_CreatesTask() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };

        // Act
        await _store.UpdateTaskAsync(task);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Id.ShouldBe("test/repo/issues/1");
        result.Status.ShouldBe(AgentTaskStatus.PendingPlanning);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithPlan_UpdatesPlan() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        var plan = new AgentPlan {
            ProblemSummary = "Test problem",
            Constraints = ["Constraint 1", "Constraint 2"],
            Steps = [
                new PlanStep {
                    Id = "step-1",
                    Title = "Step 1",
                    Details = "Details for step 1",
                    Done = false
                },
                new PlanStep {
                    Id = "step-2",
                    Title = "Step 2",
                    Details = "Details for step 2",
                    Done = false
                }
            ],
            Checklist = ["Item 1", "Item 2"],
            FileTargets = ["file1.cs", "file2.cs"]
        };

        // Act
        task.Plan = plan;
        task.Status = AgentTaskStatus.Planned;
        await _store.UpdateTaskAsync(task);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Plan.ShouldNotBeNull();
        result.Plan.ProblemSummary.ShouldBe("Test problem");
        result.Plan.Constraints.Count.ShouldBe(2);
        result.Plan.Constraints[0].ShouldBe("Constraint 1");
        result.Plan.Steps.Count.ShouldBe(2);
        result.Plan.Steps[0].Id.ShouldBe("step-1");
        result.Plan.Steps[0].Title.ShouldBe("Step 1");
        result.Plan.Checklist.Count.ShouldBe(2);
        result.Plan.FileTargets.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetTasksByInstallationIdAsync_WithMultipleTasks_ReturnsFilteredTasks() {
        // Arrange
        var task1 = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        var task2 = new AgentTask {
            Id = "test/repo/issues/2",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 2,
            Status = AgentTaskStatus.Planned
        };
        var task3 = new AgentTask {
            Id = "other/repo/issues/1",
            InstallationId = 456,
            RepositoryOwner = "other",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };

        await _store.CreateTaskAsync(task1);
        await _store.CreateTaskAsync(task2);
        await _store.CreateTaskAsync(task3);

        // Act
        var results = await _store.GetTasksByInstallationIdAsync(installationId: 123);

        // Assert
        results.Count.ShouldBe(2);
        results.ShouldContain(t => t.Id == "test/repo/issues/1");
        results.ShouldContain(t => t.Id == "test/repo/issues/2");
        results.ShouldNotContain(t => t.Id == "other/repo/issues/1");
    }

    [Fact]
    public async Task GetTasksByInstallationIdAsync_WithNoMatches_ReturnsEmptyList() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        // Act
        var results = await _store.GetTasksByInstallationIdAsync(installationId: 999);

        // Assert
        results.Count.ShouldBe(0);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithTimestamps_UpdatesTimestamps() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        var startedAt = DateTime.UtcNow;
        var completedAt = DateTime.UtcNow.AddMinutes(5);

        // Act
        task.Status = AgentTaskStatus.Executing;
        task.StartedAt = startedAt;
        await _store.UpdateTaskAsync(task);

        task.Status = AgentTaskStatus.Completed;
        task.CompletedAt = completedAt;
        await _store.UpdateTaskAsync(task);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Status.ShouldBe(AgentTaskStatus.Completed);
        result.StartedAt.ShouldNotBeNull();
        result.CompletedAt.ShouldNotBeNull();
        result.StartedAt.Value.ShouldBeInRange(startedAt.AddSeconds(-1), startedAt.AddSeconds(1));
        result.CompletedAt.Value.ShouldBeInRange(completedAt.AddSeconds(-1), completedAt.AddSeconds(1));
    }

    [Fact]
    public async Task UpdateTaskAsync_WithNullPlan_RemovesPlan() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.Planned,
            Plan = new AgentPlan {
                ProblemSummary = "Test problem",
                Constraints = ["Constraint 1"],
                Steps = [
                    new PlanStep {
                        Id = "step-1",
                        Title = "Step 1",
                        Details = "Details",
                        Done = false
                    }
                ],
                Checklist = ["Item 1"],
                FileTargets = ["file1.cs"]
            }
        };
        await _store.CreateTaskAsync(task);

        // Act
        task.Plan = null;
        await _store.UpdateTaskAsync(task);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Plan.ShouldBeNull();
    }

    [Fact]
    public async Task GetTaskAsync_WithCancellationToken_RespectsToken() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _store.GetTaskAsync("test/repo/issues/1", cancellationToken: cts.Token);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateTaskAsync_WithCancellationToken_RespectsToken() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };

        using var cts = new CancellationTokenSource();

        // Act
        var result = await _store.CreateTaskAsync(task, cancellationToken: cts.Token);

        // Assert
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateTaskAsync_WithCancellationToken_RespectsToken() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        using var cts = new CancellationTokenSource();

        // Act
        task.Status = AgentTaskStatus.Planned;
        await _store.UpdateTaskAsync(task, cancellationToken: cts.Token);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Status.ShouldBe(AgentTaskStatus.Planned);
    }

    [Fact]
    public async Task GetTasksByInstallationIdAsync_WithCancellationToken_RespectsToken() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        using var cts = new CancellationTokenSource();

        // Act
        var results = await _store.GetTasksByInstallationIdAsync(installationId: 123, cancellationToken: cts.Token);

        // Assert
        results.Count.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateTaskAsync_WithComplexPlan_PreservesAllData() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        var complexPlan = new AgentPlan {
            ProblemSummary = "Complex problem with special characters: <>&\"'",
            Constraints = [
                "Constraint with unicode: こんにちは",
                "Constraint with emoji: 🚀",
                "Constraint with newlines:\nLine 1\nLine 2"
            ],
            Steps = [
                new PlanStep {
                    Id = "step-1",
                    Title = "Step with special chars: <>&\"'",
                    Details = "Details with unicode and emoji: 测试 🎉",
                    Done = false
                },
                new PlanStep {
                    Id = "step-2",
                    Title = "Step 2",
                    Details = "Multi-line details:\nLine 1\nLine 2\nLine 3",
                    Done = true
                }
            ],
            Checklist = [
                "Item with special chars: <>&\"'",
                "Item with unicode: 日本語",
                "Item with emoji: ✅"
            ],
            FileTargets = [
                "path/to/file.cs",
                "another/path/file.ts",
                "special chars/file name.py"
            ]
        };

        // Act
        task.Plan = complexPlan;
        task.Status = AgentTaskStatus.Planned;
        await _store.UpdateTaskAsync(task);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Plan.ShouldNotBeNull();
        result.Plan.ProblemSummary.ShouldBe("Complex problem with special characters: <>&\"'");
        result.Plan.Constraints.Count.ShouldBe(3);
        result.Plan.Constraints[0].ShouldContain("こんにちは");
        result.Plan.Constraints[1].ShouldContain("🚀");
        result.Plan.Steps.Count.ShouldBe(2);
        result.Plan.Steps[0].Done.ShouldBeFalse();
        result.Plan.Steps[1].Done.ShouldBeTrue();
        result.Plan.Checklist.Count.ShouldBe(3);
        result.Plan.FileTargets.Count.ShouldBe(3);
    }

    [Fact]
    public async Task UpdateTaskAsync_MultipleUpdates_KeepsLatestData() {
        // Arrange
        var task = new AgentTask {
            Id = "test/repo/issues/1",
            InstallationId = 123,
            RepositoryOwner = "test",
            RepositoryName = "repo",
            IssueNumber = 1,
            Status = AgentTaskStatus.PendingPlanning
        };
        await _store.CreateTaskAsync(task);

        // Act - Multiple updates
        task.Status = AgentTaskStatus.Planned;
        await _store.UpdateTaskAsync(task);

        task.Status = AgentTaskStatus.Executing;
        task.StartedAt = DateTime.UtcNow;
        await _store.UpdateTaskAsync(task);

        task.Status = AgentTaskStatus.Completed;
        task.CompletedAt = DateTime.UtcNow;
        await _store.UpdateTaskAsync(task);

        // Assert
        var result = await _store.GetTaskAsync("test/repo/issues/1");
        result.ShouldNotBeNull();
        result.Status.ShouldBe(AgentTaskStatus.Completed);
        result.StartedAt.ShouldNotBeNull();
        result.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetTasksByInstallationIdAsync_WithEmptyDatabase_ReturnsEmptyList() {
        // Act
        var results = await _store.GetTasksByInstallationIdAsync(installationId: 999);

        // Assert
        results.Count.ShouldBe(0);
        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateTaskAsync_WithAllStatuses_CreatesSuccessfully() {
        // Arrange & Act & Assert
        foreach (var status in Enum.GetValues<AgentTaskStatus>()) {
            var task = new AgentTask {
                Id = $"test/repo/issues/{(int)status}",
                InstallationId = 123,
                RepositoryOwner = "test",
                RepositoryName = "repo",
                IssueNumber = (int)status,
                Status = status
            };

            var created = await _store.CreateTaskAsync(task);
            created.Status.ShouldBe(status);

            var retrieved = await _store.GetTaskAsync(task.Id);
            retrieved.ShouldNotBeNull();
            retrieved.Status.ShouldBe(status);
        }
    }
}
