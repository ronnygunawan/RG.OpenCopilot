using Moq;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

public class ProgressReporterTests {
    [Fact]
    public async Task ReportStepProgressAsync_SuccessfulStep_PostsFormattedComment() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var step = new PlanStep {
            Id = "step-1",
            Title = "Implement user authentication",
            Details = "Add authentication logic"
        };

        var result = StepExecutionResult.CreateSuccess(
            changes: [
                new FileChange { Type = ChangeType.Created, Path = "src/Auth/AuthService.cs" },
                new FileChange { Type = ChangeType.Modified, Path = "src/Program.cs" }
            ],
            buildOutput: new BuildResult {
                Success = true,
                Attempts = 1,
                Duration = TimeSpan.FromSeconds(18),
                Errors = [],
                FixesApplied = []
            },
            testResults: new TestValidationResult {
                AllPassed = true,
                TotalTests = 12,
                PassedTests = 12,
                FailedTests = 0,
                SkippedTests = 0,
                Attempts = 1,
                Duration = TimeSpan.FromSeconds(5),
                Summary = "All tests passed"
            },
            actionPlan: new StepActionPlan(),
            duration: TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(42),
            metrics: new ExecutionMetrics {
                LLMCalls = 3,
                FilesCreated = 2,
                FilesModified = 1,
                CodeGenerationDuration = TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(15)
            });

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                "owner",
                "repo",
                456,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

        // Assert
        mockGitHubService.Verify(s => s.PostPullRequestCommentAsync(
            "owner",
            "repo",
            456,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), times: Times.Once);

        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("Step Progress: Implement user authentication");
        capturedComment.ShouldContain("✅ Status: Completed Successfully");
        capturedComment.ShouldContain("3m 42s");
        capturedComment.ShouldContain("✨ Created: `src/Auth/AuthService.cs`");
        capturedComment.ShouldContain("✏️ Modified: `src/Program.cs`");
        capturedComment.ShouldContain("Build succeeded on attempt 1");
        capturedComment.ShouldContain("All tests passed (12/12)");
        capturedComment.ShouldContain("LLM calls: 3");
    }

    [Fact]
    public async Task ReportStepProgressAsync_FailedStep_PostsErrorDetails() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var step = new PlanStep {
            Id = "step-1",
            Title = "Fix broken tests",
            Details = "Fix failing unit tests"
        };

        var result = StepExecutionResult.CreateFailure(
            error: "Build failed: CS1002: ; expected",
            changes: [new FileChange { Type = ChangeType.Modified, Path = "src/Calculator.cs" }],
            duration: TimeSpan.FromSeconds(45));

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("❌ Status: Failed");
        capturedComment.ShouldContain("⚠️ Error Details");
        capturedComment.ShouldContain("Build failed: CS1002: ; expected");
        capturedComment.ShouldContain("45s");
    }

    [Fact]
    public async Task ReportStepProgressAsync_StepWithNoChanges_PostsCommentWithoutChangesSection() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var step = new PlanStep {
            Id = "step-1",
            Title = "Analysis only",
            Details = "Analyze codebase"
        };

        var result = StepExecutionResult.CreateSuccess(
            changes: [],
            buildOutput: new BuildResult { Success = true, Attempts = 1, Duration = TimeSpan.FromSeconds(5) },
            testResults: new TestValidationResult { AllPassed = true, TotalTests = 0, PassedTests = 0, Duration = TimeSpan.Zero },
            actionPlan: new StepActionPlan(),
            duration: TimeSpan.FromSeconds(30),
            metrics: new ExecutionMetrics());

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldNotContain("### 📂 Changes Made");
    }

    [Fact]
    public async Task ReportExecutionSummaryAsync_MultipleResults_PostsAggregatedSummary() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var results = new List<StepExecutionResult> {
            StepExecutionResult.CreateSuccess(
                changes: [new FileChange { Type = ChangeType.Created, Path = "file1.cs" }],
                buildOutput: new BuildResult { Success = true, Attempts = 1, Duration = TimeSpan.FromSeconds(10) },
                testResults: new TestValidationResult { AllPassed = true, TotalTests = 5, PassedTests = 5, Duration = TimeSpan.FromSeconds(3) },
                actionPlan: new StepActionPlan(),
                duration: TimeSpan.FromMinutes(2),
                metrics: new ExecutionMetrics {
                    FilesCreated = 1,
                    LLMCalls = 2,
                    BuildAttempts = 1,
                    TestAttempts = 1,
                    AnalysisDuration = TimeSpan.FromSeconds(15),
                    CodeGenerationDuration = TimeSpan.FromSeconds(30),
                    BuildDuration = TimeSpan.FromSeconds(10),
                    TestDuration = TimeSpan.FromSeconds(3)
                }),
            StepExecutionResult.CreateSuccess(
                changes: [new FileChange { Type = ChangeType.Modified, Path = "file2.cs" }],
                buildOutput: new BuildResult { Success = true, Attempts = 1, Duration = TimeSpan.FromSeconds(8) },
                testResults: new TestValidationResult { AllPassed = true, TotalTests = 3, PassedTests = 3, Duration = TimeSpan.FromSeconds(2) },
                actionPlan: new StepActionPlan(),
                duration: TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(30),
                metrics: new ExecutionMetrics {
                    FilesModified = 1,
                    LLMCalls = 1,
                    BuildAttempts = 1,
                    TestAttempts = 1,
                    AnalysisDuration = TimeSpan.FromSeconds(10),
                    CodeGenerationDuration = TimeSpan.FromSeconds(20),
                    BuildDuration = TimeSpan.FromSeconds(8),
                    TestDuration = TimeSpan.FromSeconds(2)
                }),
            StepExecutionResult.CreateFailure(
                error: "Test failed",
                duration: TimeSpan.FromSeconds(45))
        };

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportExecutionSummaryAsync(task, results, prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("🎯 Execution Summary");
        capturedComment.ShouldContain("3");  // Total steps
        capturedComment.ShouldContain("2");  // Successful
        capturedComment.ShouldContain("1");  // Failed
        capturedComment.ShouldContain("Total files created: 1");
        capturedComment.ShouldContain("Total files modified: 1");
        capturedComment.ShouldContain("Total LLM calls: 3");
        capturedComment.ShouldContain("Total build attempts: 2");
        capturedComment.ShouldContain("Total test attempts: 2");
    }

    [Fact]
    public async Task ReportIntermediateProgressAsync_PostsIntermediateUpdate() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportIntermediateProgressAsync(
            task,
            stage: "Code Generation",
            message: "Generating authentication logic...",
            prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("🔄 In Progress: Code Generation");
        capturedComment.ShouldContain("Generating authentication logic...");
    }

    [Fact]
    public async Task ReportStepProgressAsync_BuildWithErrors_IncludesErrorCount() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var step = new PlanStep {
            Id = "step-1",
            Title = "Fix compilation errors"
        };

        var result = StepExecutionResult.CreateSuccess(
            changes: [],
            buildOutput: new BuildResult {
                Success = true,
                Attempts = 2,
                Duration = TimeSpan.FromSeconds(20),
                Errors = [
                    new BuildError(),
                    new BuildError()
                ],
                FixesApplied = [
                    new CodeFix(),
                    new CodeFix()
                ]
            },
            testResults: new TestValidationResult { AllPassed = true, TotalTests = 0, PassedTests = 0, Duration = TimeSpan.Zero },
            actionPlan: new StepActionPlan(),
            duration: TimeSpan.FromMinutes(1),
            metrics: new ExecutionMetrics());

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("Build succeeded on attempt 2");
        capturedComment.ShouldContain("Errors: 2");
        capturedComment.ShouldContain("Fixes applied: 2");
    }

    [Fact]
    public async Task ReportStepProgressAsync_TestsWithFailures_IncludesFailureDetails() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var step = new PlanStep {
            Id = "step-1",
            Title = "Fix failing tests"
        };

        var result = StepExecutionResult.CreateSuccess(
            changes: [],
            buildOutput: new BuildResult { Success = true, Attempts = 1, Duration = TimeSpan.FromSeconds(5) },
            testResults: new TestValidationResult {
                AllPassed = false,
                TotalTests = 10,
                PassedTests = 7,
                FailedTests = 2,
                SkippedTests = 1,
                Attempts = 1,
                Duration = TimeSpan.FromSeconds(8),
                FixesApplied = [new TestFix()]
            },
            actionPlan: new StepActionPlan(),
            duration: TimeSpan.FromMinutes(1),
            metrics: new ExecutionMetrics());

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("⚠️ Some tests failed (7/10)");
        capturedComment.ShouldContain("Failed: 2");
        capturedComment.ShouldContain("Skipped: 1");
        capturedComment.ShouldContain("Fixes applied: 1");
    }

    [Fact]
    public async Task UpdateProgressAsync_LogsWarning_WhenCalled() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        // Act
        await reporter.UpdateProgressAsync(task, commentId: 789, updatedContent: "Updated content");

        // Assert
        // Should complete without throwing
        mockGitHubService.Verify(s => s.PostPullRequestCommentAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task ReportStepProgressAsync_WithDeletedFiles_ShowsDeletedIcon() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var step = new PlanStep {
            Id = "step-1",
            Title = "Remove obsolete code"
        };

        var result = StepExecutionResult.CreateSuccess(
            changes: [
                new FileChange { Type = ChangeType.Deleted, Path = "src/OldService.cs" }
            ],
            buildOutput: new BuildResult { Success = true, Attempts = 1, Duration = TimeSpan.FromSeconds(5) },
            testResults: new TestValidationResult { AllPassed = true, TotalTests = 0, PassedTests = 0, Duration = TimeSpan.Zero },
            actionPlan: new StepActionPlan(),
            duration: TimeSpan.FromSeconds(30),
            metrics: new ExecutionMetrics { FilesDeleted = 1 });

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("🗑️ Deleted: `src/OldService.cs`");
    }

    [Fact]
    public async Task ReportStepProgressAsync_DurationFormatting_FormatsCorrectly() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var step = new PlanStep {
            Id = "step-1",
            Title = "Long running task"
        };

        // Test various duration formats
        var testCases = new[] {
            (duration: TimeSpan.FromSeconds(30), expected: "30s"),
            (duration: TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(15), expected: "2m 15s"),
            (duration: TimeSpan.FromHours(1) + TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(10), expected: "1h 5m 10s")
        };

        foreach (var testCase in testCases) {
            var result = StepExecutionResult.CreateSuccess(
                changes: [],
                buildOutput: new BuildResult { Success = true, Attempts = 1, Duration = TimeSpan.FromSeconds(5) },
                testResults: new TestValidationResult { AllPassed = true, TotalTests = 0, PassedTests = 0, Duration = TimeSpan.Zero },
                actionPlan: new StepActionPlan(),
                duration: testCase.duration,
                metrics: new ExecutionMetrics());

            string? capturedComment = null;
            mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                    capturedComment = comment;
                })
                .Returns(Task.CompletedTask);

            // Act
            await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

            // Assert
            capturedComment.ShouldNotBeNull();
            capturedComment.ShouldContain(testCase.expected);
        }
    }

    [Fact]
    public async Task UpdatePullRequestProgressAsync_UpdatesCheckedOffSteps() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123,
            Plan = new AgentPlan {
                Steps = [
                    new PlanStep { Id = "1", Title = "Step 1", Details = "First step", Done = true },
                    new PlanStep { Id = "2", Title = "Step 2", Details = "Second step", Done = false }
                ]
            }
        };

        var existingPrBody = """
            ## Plan
            
            **Problem Summary:** Test problem
            
            ### Steps
            
            - [ ] **Step 1** - First step
            - [ ] **Step 2** - Second step
            
            ### Constraints
            
            - Keep it simple
            
            ---
            
            _This PR was automatically created by RG.OpenCopilot._
            """;

        mockGitHubService.Setup(s => s.GetPullRequestAsync("owner", "repo", 456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestInfo {
                Number = 456,
                HeadRef = "test-branch",
                Title = "Test PR",
                Body = existingPrBody
            });

        string? capturedBody = null;
        mockGitHubService.Setup(s => s.UpdatePullRequestDescriptionAsync(
                "owner",
                "repo",
                456,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, string, CancellationToken>((_, _, _, _, body, _) => {
                capturedBody = body;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.UpdatePullRequestProgressAsync(task, prNumber: 456);

        // Assert
        mockGitHubService.Verify(s => s.UpdatePullRequestDescriptionAsync(
            "owner",
            "repo",
            456,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), times: Times.Once);

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("- [x] **Step 1**");
        capturedBody.ShouldContain("- [ ] **Step 2**");
    }

    [Fact]
    public async Task UpdatePullRequestProgressAsync_WithNoPlan_LogsWarning() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123,
            Plan = null
        };

        // Act
        await reporter.UpdatePullRequestProgressAsync(task, prNumber: 456);

        // Assert
        mockGitHubService.Verify(s => s.GetPullRequestAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task ReportCommitSummaryAsync_PostsFormattedComment() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        var changes = new List<FileChange> {
            new FileChange { Type = ChangeType.Created, Path = "src/NewFile.cs" },
            new FileChange { Type = ChangeType.Modified, Path = "src/ExistingFile.cs" },
            new FileChange { Type = ChangeType.Deleted, Path = "src/OldFile.cs" }
        };

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                "owner",
                "repo",
                456,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportCommitSummaryAsync(
            task,
            commitSha: "abc123def456",
            commitMessage: "Add authentication feature",
            changes: changes,
            prNumber: 456);

        // Assert
        mockGitHubService.Verify(s => s.PostPullRequestCommentAsync(
            "owner",
            "repo",
            456,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), times: Times.Once);

        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("📦 Commit Summary");
        capturedComment.ShouldContain("abc123d");
        capturedComment.ShouldContain("Add authentication feature");
        capturedComment.ShouldContain("✨ `src/NewFile.cs`");
        capturedComment.ShouldContain("✏️ `src/ExistingFile.cs`");
        capturedComment.ShouldContain("🗑️ `src/OldFile.cs`");
        capturedComment.ShouldContain("Total:** 3 file(s) changed");
    }

    [Fact]
    public async Task ReportCommitSummaryAsync_WithNoChanges_ShowsNoChangesMessage() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123
        };

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportCommitSummaryAsync(
            task,
            commitSha: "abc123def456",
            commitMessage: "Empty commit",
            changes: [],
            prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("No file changes in this commit");
    }

    [Fact]
    public async Task ReportStepProgressAsync_WithTimingInfo_IncludesElapsedAndEstimatedTime() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123,
            StartedAt = timeProvider.GetUtcNow().DateTime.AddMinutes(-10),
            Plan = new AgentPlan {
                Steps = [
                    new PlanStep { Id = "1", Title = "Step 1", Done = true },
                    new PlanStep { Id = "2", Title = "Step 2", Done = true },
                    new PlanStep { Id = "3", Title = "Step 3", Done = false },
                    new PlanStep { Id = "4", Title = "Step 4", Done = false }
                ]
            }
        };

        var step = new PlanStep {
            Id = "step-2",
            Title = "Step 2"
        };

        var result = StepExecutionResult.CreateSuccess(
            changes: [],
            buildOutput: new BuildResult { Success = true, Attempts = 1, Duration = TimeSpan.FromSeconds(5) },
            testResults: new TestValidationResult { AllPassed = true, TotalTests = 0, PassedTests = 0, Duration = TimeSpan.Zero },
            actionPlan: new StepActionPlan(),
            duration: TimeSpan.FromMinutes(2),
            metrics: new ExecutionMetrics());

        string? capturedComment = null;
        mockGitHubService.Setup(s => s.PostPullRequestCommentAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, CancellationToken>((_, _, _, comment, _) => {
                capturedComment = comment;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.ReportStepProgressAsync(task, step, result, prNumber: 456);

        // Assert
        capturedComment.ShouldNotBeNull();
        capturedComment.ShouldContain("Elapsed Time:");
        capturedComment.ShouldContain("Estimated Completion:");
        capturedComment.ShouldContain("Progress:** 2/4 steps completed");
    }

    [Fact]
    public async Task FinalizePullRequestAsync_RemovesWipPrefix_AndArchivesDetails() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123,
            Plan = new AgentPlan {
                Steps = [
                    new PlanStep { Id = "1", Title = "Implement authentication", Details = "Add auth logic", Done = true },
                    new PlanStep { Id = "2", Title = "Add tests", Details = "Write unit tests", Done = true }
                ]
            }
        };

        var wipBody = """
            ## Plan
            
            **Problem Summary:** Add authentication
            
            ### Steps
            
            - [x] **Implement authentication** - Add auth logic
            - [x] **Add tests** - Write unit tests
            
            ---
            
            _This PR was automatically created by RG.OpenCopilot._
            """;

        mockGitHubService.Setup(s => s.GetPullRequestAsync("owner", "repo", 456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestInfo {
                Number = 456,
                HeadRef = "test-branch",
                Title = "[WIP] Add authentication",
                Body = wipBody
            });

        string? capturedTitle = null;
        string? capturedBody = null;
        mockGitHubService.Setup(s => s.UpdatePullRequestDescriptionAsync(
                "owner",
                "repo",
                456,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, string, CancellationToken>((_, _, _, title, body, _) => {
                capturedTitle = title;
                capturedBody = body;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.FinalizePullRequestAsync(task, prNumber: 456);

        // Assert
        mockGitHubService.Verify(s => s.UpdatePullRequestDescriptionAsync(
            "owner",
            "repo",
            456,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), times: Times.Once);

        capturedTitle.ShouldNotBeNull();
        capturedTitle.ShouldBe("Add authentication");
        capturedTitle.ShouldNotContain("[WIP]");

        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("## Summary");
        capturedBody.ShouldContain("issue #123");
        capturedBody.ShouldContain("✅ Implement authentication");
        capturedBody.ShouldContain("✅ Add tests");
        capturedBody.ShouldContain("<details>");
        capturedBody.ShouldContain("📋 WIP Progress Details");
        capturedBody.ShouldContain(wipBody);
        capturedBody.ShouldContain("</details>");
    }

    [Fact]
    public async Task FinalizePullRequestAsync_WithNoPlan_LogsWarning() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123,
            Plan = null
        };

        // Act
        await reporter.FinalizePullRequestAsync(task, prNumber: 456);

        // Assert
        mockGitHubService.Verify(s => s.GetPullRequestAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), times: Times.Never);
    }

    [Fact]
    public async Task FinalizePullRequestAsync_IncludesTestingSection() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 456,
            Plan = new AgentPlan {
                Steps = [
                    new PlanStep { Id = "1", Title = "Fix bug", Details = "Fix critical bug", Done = true }
                ]
            }
        };

        mockGitHubService.Setup(s => s.GetPullRequestAsync("owner", "repo", 789, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestInfo {
                Number = 789,
                HeadRef = "test-branch",
                Title = "[WIP] Fix bug",
                Body = "Original body"
            });

        string? capturedBody = null;
        mockGitHubService.Setup(s => s.UpdatePullRequestDescriptionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, string, CancellationToken>((_, _, _, _, body, _) => {
                capturedBody = body;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.FinalizePullRequestAsync(task, prNumber: 789);

        // Assert
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("### Testing");
        capturedBody.ShouldContain("All automated tests have been run and verified to pass");
    }

    [Fact]
    public async Task FinalizePullRequestAsync_OnlyIncludesCompletedSteps() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123,
            Plan = new AgentPlan {
                Steps = [
                    new PlanStep { Id = "1", Title = "Completed step", Details = "Done", Done = true },
                    new PlanStep { Id = "2", Title = "Incomplete step", Details = "Not done", Done = false }
                ]
            }
        };

        mockGitHubService.Setup(s => s.GetPullRequestAsync("owner", "repo", 456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestInfo {
                Number = 456,
                HeadRef = "test-branch",
                Title = "[WIP] Test PR",
                Body = "WIP body"
            });

        string? capturedBody = null;
        mockGitHubService.Setup(s => s.UpdatePullRequestDescriptionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, string, CancellationToken>((_, _, _, _, body, _) => {
                capturedBody = body;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.FinalizePullRequestAsync(task, prNumber: 456);

        // Assert
        capturedBody.ShouldNotBeNull();
        capturedBody.ShouldContain("✅ Completed step");
        capturedBody.ShouldNotContain("Incomplete step");
    }

    [Fact]
    public async Task FinalizePullRequestAsync_TitleWithoutWipPrefix_RemainsUnchanged() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockGitHubService = new Mock<IGitHubService>();
        var logger = new TestLogger<ProgressReporter>();
        var reporter = new ProgressReporter(mockGitHubService.Object, timeProvider, logger);

        var task = new AgentTask {
            Id = "task-1",
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 123,
            Plan = new AgentPlan {
                Steps = [
                    new PlanStep { Id = "1", Title = "Step 1", Done = true }
                ]
            }
        };

        mockGitHubService.Setup(s => s.GetPullRequestAsync("owner", "repo", 456, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestInfo {
                Number = 456,
                HeadRef = "test-branch",
                Title = "Already final title",
                Body = "WIP body"
            });

        string? capturedTitle = null;
        mockGitHubService.Setup(s => s.UpdatePullRequestDescriptionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, int, string, string, CancellationToken>((_, _, _, title, _, _) => {
                capturedTitle = title;
            })
            .Returns(Task.CompletedTask);

        // Act
        await reporter.FinalizePullRequestAsync(task, prNumber: 456);

        // Assert
        capturedTitle.ShouldNotBeNull();
        capturedTitle.ShouldBe("Already final title");
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
