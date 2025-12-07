using Microsoft.Extensions.Logging;
using Moq;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class SmartStepExecutorTests {
    [Fact]
    public async Task ExecuteStepAsync_SuccessfulExecution_ReturnsSuccessResult() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-1",
            Title = "Add user authentication",
            Details = "Implement JWT-based authentication"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            Files = ["Program.cs"],
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/AuthService.cs",
                    Description = "Create authentication service",
                    Request = new CodeGenerationRequest { Content = "public class AuthService { }" }
                }
            ],
            RequiresTests = true,
            MainFile = "/workspace/AuthService.cs",
            TestFile = "/workspace/AuthServiceTests.cs"
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(5)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 5,
            PassedTests = 5,
            FailedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(3)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        SetupSuccessfulExecution(mocks, actionPlan, buildResult, testResult, qualityResult, "/workspace/AuthService.cs", "test content");

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(
            containerId: containerId,
            step: step,
            context: context);

        // Assert
        result.Success.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.Changes.Count.ShouldBeGreaterThan(0);
        result.BuildResult.ShouldNotBeNull();
        result.BuildResult!.Success.ShouldBeTrue();
        result.TestResult.ShouldNotBeNull();
        result.TestResult!.AllPassed.ShouldBeTrue();
        result.Metrics.LLMCalls.ShouldBeGreaterThan(0);
        result.Metrics.FilesCreated.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ExecuteStepAsync_BuildFails_ReturnsFailureResult() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-2",
            Title = "Add feature",
            Details = "Add new feature"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/Feature.cs",
                    Description = "Create feature",
                    Request = new CodeGenerationRequest { Content = "public class Feature { }" }
                }
            ],
            RequiresTests = false
        };

        var buildResult = new BuildResult {
            Success = false,
            Attempts = 3,
            Output = "Compilation error",
            Duration = TimeSpan.FromSeconds(10)
        };

        var mocks = CreateMocks();
        SetupBuildFailure(mocks, actionPlan, buildResult);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(
            containerId: containerId,
            step: step,
            context: context);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Build failed");
        result.BuildResult.ShouldNotBeNull();
        result.BuildResult!.Success.ShouldBeFalse();
        result.Metrics.BuildAttempts.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteStepAsync_TestsFail_ReturnsFailureResult() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-3",
            Title = "Add feature with tests",
            Details = "Add feature and tests"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/Feature.cs",
                    Description = "Create feature",
                    Request = new CodeGenerationRequest { Content = "public class Feature { }" }
                }
            ],
            RequiresTests = true,
            MainFile = "/workspace/Feature.cs",
            TestFile = "/workspace/FeatureTests.cs"
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(5)
        };

        var testResult = new TestValidationResult {
            AllPassed = false,
            TotalTests = 5,
            PassedTests = 3,
            FailedTests = 2,
            Attempts = 2,
            Summary = "2 tests failed",
            Duration = TimeSpan.FromSeconds(10)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        SetupTestFailure(mocks, actionPlan, buildResult, testResult, qualityResult, "/workspace/Feature.cs", "test content");

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(
            containerId: containerId,
            step: step,
            context: context);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Tests failed");
        result.TestResult.ShouldNotBeNull();
        result.TestResult!.AllPassed.ShouldBeFalse();
        result.Metrics.TestAttempts.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteStepAsync_WithMultipleActions_ExecutesAllActions() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-4",
            Title = "Refactor code",
            Details = "Refactor multiple files"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/NewService.cs",
                    Description = "Create new service",
                    Request = new CodeGenerationRequest { Content = "public class NewService { }" }
                },
                new CodeAction {
                    Type = ActionType.ModifyFile,
                    FilePath = "/workspace/ExistingService.cs",
                    Description = "Update existing service",
                    Request = new CodeGenerationRequest { Content = "public class ExistingService { // updated }" }
                },
                new CodeAction {
                    Type = ActionType.DeleteFile,
                    FilePath = "/workspace/OldService.cs",
                    Description = "Delete old service",
                    Request = new CodeGenerationRequest()
                }
            ],
            RequiresTests = false
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(5)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        SetupMultipleActions(mocks, actionPlan, buildResult, testResult, qualityResult);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(
            containerId: containerId,
            step: step,
            context: context);

        // Assert
        result.Success.ShouldBeTrue();
        result.Changes.Count.ShouldBe(3);
        result.Metrics.FilesCreated.ShouldBe(1);
        result.Metrics.FilesModified.ShouldBe(1);
        result.Metrics.FilesDeleted.ShouldBe(1);

        mocks.FileEditor.Verify(
            f => f.CreateFileAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p == "/workspace/NewService.cs"),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mocks.FileEditor.Verify(
            f => f.ModifyFileAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p == "/workspace/ExistingService.cs"),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        mocks.FileEditor.Verify(
            f => f.DeleteFileAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p == "/workspace/OldService.cs"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteStepAsync_AnalyzerThrowsException_ReturnsFailureResult() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-5",
            Title = "Test step",
            Details = "Test details"
        };
        var context = new RepositoryContext {
            Language = "csharp"
        };

        var mocks = CreateMocks();
        mocks.StepAnalyzer
            .Setup(a => a.AnalyzeStepAsync(
                It.IsAny<PlanStep>(),
                It.IsAny<RepositoryContext>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Analysis failed"));

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(
            containerId: containerId,
            step: step,
            context: context);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("Analysis failed");
    }

    [Fact]
    public async Task ExecuteStepWithRetryAsync_FirstAttemptSucceeds_ReturnsSuccessWithoutRetry() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-6",
            Title = "Test step",
            Details = "Test details"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/Test.cs",
                    Description = "Create test file",
                    Request = new CodeGenerationRequest { Content = "public class Test { }" }
                }
            ],
            RequiresTests = false
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(5)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        SetupSuccessfulExecution(mocks, actionPlan, buildResult, testResult, qualityResult, null, null);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepWithRetryAsync(
            containerId: containerId,
            step: step,
            context: context,
            maxRetries: 2);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify StepAnalyzer was called only once (no retries needed)
        mocks.StepAnalyzer.Verify(
            a => a.AnalyzeStepAsync(
                It.IsAny<PlanStep>(),
                It.IsAny<RepositoryContext>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteStepWithRetryAsync_SecondAttemptSucceeds_ReturnsSuccess() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-7",
            Title = "Test step",
            Details = "Test details"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/Test.cs",
                    Description = "Create test file",
                    Request = new CodeGenerationRequest { Content = "public class Test { }" }
                }
            ],
            RequiresTests = false
        };

        var failedBuildResult = new BuildResult {
            Success = false,
            Attempts = 3,
            Output = "Build failed",
            Duration = TimeSpan.FromSeconds(10)
        };

        var successBuildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(5)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        
        // First attempt fails, second succeeds
        var attemptCount = 0;
        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                return attemptCount == 1 ? failedBuildResult : successBuildResult;
            });

        SetupBasicMocks(mocks, actionPlan, testResult, qualityResult);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepWithRetryAsync(
            containerId: containerId,
            step: step,
            context: context,
            maxRetries: 2);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify StepAnalyzer was called twice (first attempt + one retry)
        mocks.StepAnalyzer.Verify(
            a => a.AnalyzeStepAsync(
                It.IsAny<PlanStep>(),
                It.IsAny<RepositoryContext>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RollbackStepAsync_WithFileChanges_ReversesChanges() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() {
                Type = ChangeType.Created,
                Path = "/workspace/NewFile.cs",
                NewContent = "public class NewFile { }"
            },
            new() {
                Type = ChangeType.Modified,
                Path = "/workspace/ModifiedFile.cs",
                OldContent = "old content",
                NewContent = "new content"
            },
            new() {
                Type = ChangeType.Deleted,
                Path = "/workspace/DeletedFile.cs",
                OldContent = "deleted content"
            }
        };

        var failedResult = StepExecutionResult.CreateFailure(
            error: "Test failure",
            changes: changes);

        var mocks = CreateMocks();

        // Setup file existence check for Created file
        mocks.ContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.Is<string>(id => id == containerId),
                It.Is<string>(cmd => cmd == "test"),
                It.Is<string[]>(args => args.Length == 2 && args[0] == "-f" && args[1] == "/workspace/NewFile.cs"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0 });

        // Setup ExecuteInContainerAsync for rm command
        mocks.ContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.Is<string>(id => id == containerId),
                It.Is<string>(cmd => cmd == "rm"),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0 });

        var executor = CreateExecutor(mocks);

        // Act
        await executor.RollbackStepAsync(
            containerId: containerId,
            failedResult: failedResult);

        // Assert - verify rollback operations
        // Verify Created file was deleted
        mocks.ContainerManager.Verify(
            c => c.ExecuteInContainerAsync(
                containerId,
                "rm",
                It.Is<string[]>(args => args.Length == 2 && args[1] == "/workspace/NewFile.cs"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify Modified file was restored
        mocks.ContainerManager.Verify(
            c => c.WriteFileInContainerAsync(
                containerId,
                "/workspace/ModifiedFile.cs",
                "old content",
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify Deleted file was recreated
        mocks.ContainerManager.Verify(
            c => c.WriteFileInContainerAsync(
                containerId,
                "/workspace/DeletedFile.cs",
                "deleted content",
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify changes were cleared
        mocks.FileEditor.Verify(f => f.ClearChanges(), Times.Once);
    }

    [Fact]
    public async Task ExecuteStepAsync_GeneratesTestsWhenRequired_CreatesTestFile() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-8",
            Title = "Add service with tests",
            Details = "Create service and tests"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/Service.cs",
                    Description = "Create service",
                    Request = new CodeGenerationRequest { Content = "public class Service { }" }
                }
            ],
            RequiresTests = true,
            MainFile = "/workspace/Service.cs",
            TestFile = "/workspace/ServiceTests.cs"
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(5)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 3,
            PassedTests = 3,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(3)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        SetupTestGeneration(mocks, actionPlan, buildResult, testResult, qualityResult);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(
            containerId: containerId,
            step: step,
            context: context);

        // Assert
        result.Success.ShouldBeTrue();

        // Verify test generation was called
        mocks.TestGenerator.Verify(
            t => t.GenerateTestsAsync(
                containerId,
                "/workspace/Service.cs",
                It.IsAny<string>(),
                "xUnit",
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify test file was created
        mocks.FileEditor.Verify(
            f => f.CreateFileAsync(
                containerId,
                "/workspace/ServiceTests.cs",
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteStepAsync_CollectsMetrics_ReturnsAccurateMetrics() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-9",
            Title = "Test metrics",
            Details = "Test metrics collection"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/File1.cs",
                    Description = "Create file 1",
                    Request = new CodeGenerationRequest { Content = "class File1 { }" }
                },
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/File2.cs",
                    Description = "Create file 2",
                    Request = new CodeGenerationRequest { Content = "class File2 { }" }
                }
            ],
            RequiresTests = false
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 2,
            FixesApplied = [new CodeFix(), new CodeFix()],
            Duration = TimeSpan.FromSeconds(8)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(2)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        SetupSuccessfulExecution(mocks, actionPlan, buildResult, testResult, qualityResult, null, null);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(
            containerId: containerId,
            step: step,
            context: context);

        // Assert
        result.Success.ShouldBeTrue();
        result.Metrics.ShouldNotBeNull();
        result.Metrics.FilesCreated.ShouldBe(2);
        result.Metrics.FilesModified.ShouldBe(0);
        result.Metrics.FilesDeleted.ShouldBe(0);
        result.Metrics.BuildAttempts.ShouldBe(2);
        result.Metrics.TestAttempts.ShouldBe(1);
        result.Metrics.LLMCalls.ShouldBeGreaterThan(0); // Analysis + actions + build fixes
        result.Metrics.AnalysisDuration.ShouldBeGreaterThan(TimeSpan.Zero);
        result.Metrics.BuildDuration.ShouldBe(TimeSpan.FromSeconds(8));
        result.Metrics.TestDuration.ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ExecuteStepAsync_CreateFileWithEmptyContent_GeneratesContentUsingLLM() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-empty-content",
            Title = "Create file with LLM generation",
            Details = "Create a file where LLM generates content"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/Generated.cs",
                    Description = "Generate a new class",
                    Request = new CodeGenerationRequest { Content = "" } // Empty content
                }
            ],
            RequiresTests = false
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(2)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        mocks.StepAnalyzer
            .Setup(a => a.AnalyzeStepAsync(It.IsAny<PlanStep>(), It.IsAny<RepositoryContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionPlan);

        mocks.CodeGenerator
            .Setup(c => c.GenerateCodeAsync(It.IsAny<LlmCodeGenerationRequest>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Generated { }");

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        mocks.TestValidator
            .Setup(t => t.RunAndValidateTestsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        mocks.QualityChecker
            .Setup(q => q.CheckAndFixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityResult);

        mocks.FileEditor
            .Setup(f => f.GetChanges())
            .Returns([new FileChange { Type = ChangeType.Created, Path = "/workspace/Generated.cs" }]);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(containerId, step, context);

        // Assert
        result.Success.ShouldBeTrue();
        mocks.CodeGenerator.Verify(c => c.GenerateCodeAsync(
            It.Is<LlmCodeGenerationRequest>(r => r.FilePath == "/workspace/Generated.cs"),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteStepAsync_ModifyFileWithEmptyContent_GeneratesModificationsUsingLLM() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-modify-empty",
            Title = "Modify file with LLM generation",
            Details = "Modify a file using LLM"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.ModifyFile,
                    FilePath = "/workspace/Existing.cs",
                    Description = "Update the class",
                    Request = new CodeGenerationRequest { Content = "" } // Empty content
                }
            ],
            RequiresTests = false
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(2)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        mocks.StepAnalyzer
            .Setup(a => a.AnalyzeStepAsync(It.IsAny<PlanStep>(), It.IsAny<RepositoryContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionPlan);

        mocks.ContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "/workspace/Existing.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Existing { }");

        mocks.CodeGenerator
            .Setup(c => c.GenerateCodeAsync(It.IsAny<LlmCodeGenerationRequest>(), "public class Existing { }", It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Existing { /* updated */ }");

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        mocks.TestValidator
            .Setup(t => t.RunAndValidateTestsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        mocks.QualityChecker
            .Setup(q => q.CheckAndFixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityResult);

        mocks.FileEditor
            .Setup(f => f.GetChanges())
            .Returns([new FileChange { Type = ChangeType.Modified, Path = "/workspace/Existing.cs" }]);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(containerId, step, context);

        // Assert
        result.Success.ShouldBeTrue();
        mocks.CodeGenerator.Verify(c => c.GenerateCodeAsync(
            It.Is<LlmCodeGenerationRequest>(r => r.FilePath == "/workspace/Existing.cs"),
            "public class Existing { }",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteStepAsync_QualityCheckFails_ContinuesExecution() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-quality-fail",
            Title = "Test quality check failure",
            Details = "Quality check fails but execution continues"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/File.cs",
                    Description = "Create file",
                    Request = new CodeGenerationRequest { Content = "public class File { }" }
                }
            ],
            RequiresTests = false
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(2)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = false, // Quality check fails
            Issues = [new QualityIssue { Message = "Code quality issue" }]
        };

        var mocks = CreateMocks();
        SetupSuccessfulExecution(mocks, actionPlan, buildResult, testResult, qualityResult, null, null);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(containerId, step, context);

        // Assert
        result.Success.ShouldBeTrue(); // Should still succeed despite quality failure
    }

    [Fact]
    public async Task RollbackStepAsync_WithRollbackError_ContinuesWithOtherRollbacks() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() {
                Type = ChangeType.Created,
                Path = "/workspace/File1.cs",
                NewContent = "file1"
            },
            new() {
                Type = ChangeType.Created,
                Path = "/workspace/File2.cs",
                NewContent = "file2"
            }
        };

        var failedResult = StepExecutionResult.CreateFailure("Test failure", changes: changes);

        var mocks = CreateMocks();

        // First file rollback will fail
        var callCount = 0;
        mocks.ContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "test",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                callCount++;
                if (callCount == 1) {
                    throw new InvalidOperationException("Rollback error");
                }
                return new CommandResult { ExitCode = 0 };
            });

        mocks.ContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "rm",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0 });

        var executor = CreateExecutor(mocks);

        // Act & Assert - should not throw, continues with other rollbacks
        await executor.RollbackStepAsync(containerId, failedResult);

        mocks.FileEditor.Verify(f => f.ClearChanges(), Times.Once);
    }

    [Fact]
    public async Task RollbackStepAsync_OuterExceptionDuringRollback_ThrowsInvalidOperationException() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() {
                Type = ChangeType.Created,
                Path = "/workspace/File.cs"
            }
        };

        var failedResult = StepExecutionResult.CreateFailure("Test failure", changes: changes);

        var mocks = CreateMocks();

        // Mock throws on clearing changes
        mocks.FileEditor
            .Setup(f => f.ClearChanges())
            .Throws(new InvalidOperationException("Clear failed"));

        mocks.ContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0 });

        var executor = CreateExecutor(mocks);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await executor.RollbackStepAsync(containerId, failedResult));

        exception.Message.ShouldContain("Rollback failed");
    }

    [Fact]
    public async Task RollbackStepAsync_ModifiedFileWithNullOldContent_SkipsRestore() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() {
                Type = ChangeType.Modified,
                Path = "/workspace/File.cs",
                OldContent = null,  // null old content
                NewContent = "new content"
            }
        };

        var failedResult = StepExecutionResult.CreateFailure("Test failure", changes: changes);

        var mocks = CreateMocks();
        var executor = CreateExecutor(mocks);

        // Act
        await executor.RollbackStepAsync(containerId, failedResult);

        // Assert - WriteFileInContainerAsync should not be called
        mocks.ContainerManager.Verify(
            c => c.WriteFileInContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RollbackStepAsync_DeletedFileWithNullOldContent_SkipsRecreate() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() {
                Type = ChangeType.Deleted,
                Path = "/workspace/File.cs",
                OldContent = null  // null old content
            }
        };

        var failedResult = StepExecutionResult.CreateFailure("Test failure", changes: changes);

        var mocks = CreateMocks();
        var executor = CreateExecutor(mocks);

        // Act
        await executor.RollbackStepAsync(containerId, failedResult);

        // Assert - WriteFileInContainerAsync should not be called
        mocks.ContainerManager.Verify(
            c => c.WriteFileInContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteStepAsync_TestFilePathIsEmpty_SkipsTestFileCreation() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-no-test-path",
            Title = "Test without test file path",
            Details = "Generate tests but no test file path specified"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/Service.cs",
                    Description = "Create service",
                    Request = new CodeGenerationRequest { Content = "public class Service { }" }
                }
            ],
            RequiresTests = true,
            MainFile = "/workspace/Service.cs",
            TestFile = "" // Empty test file path
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(2)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        mocks.StepAnalyzer
            .Setup(a => a.AnalyzeStepAsync(It.IsAny<PlanStep>(), It.IsAny<RepositoryContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionPlan);

        mocks.ContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "/workspace/Service.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Service { }");

        mocks.TestGenerator
            .Setup(t => t.GenerateTestsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Tests { }");

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        mocks.TestValidator
            .Setup(t => t.RunAndValidateTestsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        mocks.QualityChecker
            .Setup(q => q.CheckAndFixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityResult);

        mocks.FileEditor
            .Setup(f => f.GetChanges())
            .Returns([new FileChange { Type = ChangeType.Created, Path = "/workspace/Service.cs" }]);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(containerId, step, context);

        // Assert
        result.Success.ShouldBeTrue();
        // Verify test file was not created or modified since path is empty
        // TestGenerator should still be called to generate test content
        mocks.TestGenerator.Verify(
            t => t.GenerateTestsAsync(It.IsAny<string>(), "/workspace/Service.cs", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // But the test file should not be written anywhere since TestFile is empty
        mocks.FileEditor.Verify(
            f => f.CreateFileAsync(It.IsAny<string>(), "", It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteStepAsync_RequiresTestsButMainFileEmpty_SkipsTestGeneration() {
        // Arrange
        var containerId = "test-container";
        var step = new PlanStep {
            Id = "step-no-main-file",
            Title = "Test without main file",
            Details = "RequiresTests is true but MainFile is empty"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var actionPlan = new StepActionPlan {
            Actions = [
                new CodeAction {
                    Type = ActionType.CreateFile,
                    FilePath = "/workspace/File.cs",
                    Description = "Create file",
                    Request = new CodeGenerationRequest { Content = "public class File { }" }
                }
            ],
            RequiresTests = true,
            MainFile = "", // Empty main file
            TestFile = "/workspace/Tests.cs"
        };

        var buildResult = new BuildResult {
            Success = true,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(2)
        };

        var testResult = new TestValidationResult {
            AllPassed = true,
            TotalTests = 0,
            PassedTests = 0,
            Attempts = 1,
            Duration = TimeSpan.FromSeconds(1)
        };

        var qualityResult = new QualityResult {
            Success = true,
            Issues = []
        };

        var mocks = CreateMocks();
        SetupSuccessfulExecution(mocks, actionPlan, buildResult, testResult, qualityResult, null, null);

        var executor = CreateExecutor(mocks);

        // Act
        var result = await executor.ExecuteStepAsync(containerId, step, context);

        // Assert
        result.Success.ShouldBeTrue();
        // Test generator should not be called
        mocks.TestGenerator.Verify(
            t => t.GenerateTestsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Helper methods
    private static (
        Mock<IStepAnalyzer> StepAnalyzer,
        Mock<ICodeGenerator> CodeGenerator,
        Mock<ITestGenerator> TestGenerator,
        Mock<IFileEditor> FileEditor,
        Mock<IBuildVerifier> BuildVerifier,
        Mock<ITestValidator> TestValidator,
        Mock<ICodeQualityChecker> QualityChecker,
        Mock<IFileAnalyzer> FileAnalyzer,
        Mock<IContainerManager> ContainerManager,
        TestLogger<SmartStepExecutor> Logger
    ) CreateMocks() {
        return (
            new Mock<IStepAnalyzer>(),
            new Mock<ICodeGenerator>(),
            new Mock<ITestGenerator>(),
            new Mock<IFileEditor>(),
            new Mock<IBuildVerifier>(),
            new Mock<ITestValidator>(),
            new Mock<ICodeQualityChecker>(),
            new Mock<IFileAnalyzer>(),
            new Mock<IContainerManager>(),
            new TestLogger<SmartStepExecutor>()
        );
    }

    private static SmartStepExecutor CreateExecutor(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks) {
        return new SmartStepExecutor(
            stepAnalyzer: mocks.StepAnalyzer.Object,
            codeGenerator: mocks.CodeGenerator.Object,
            testGenerator: mocks.TestGenerator.Object,
            fileEditor: mocks.FileEditor.Object,
            buildVerifier: mocks.BuildVerifier.Object,
            testValidator: mocks.TestValidator.Object,
            qualityChecker: mocks.QualityChecker.Object,
            fileAnalyzer: mocks.FileAnalyzer.Object,
            containerManager: mocks.ContainerManager.Object,
            logger: mocks.Logger);
    }

    private static void SetupSuccessfulExecution(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks,
        StepActionPlan actionPlan,
        BuildResult buildResult,
        TestValidationResult testResult,
        QualityResult qualityResult,
        string? mainFilePath,
        string? mainFileContent) {
        
        SetupBasicMocks(mocks, actionPlan, testResult, qualityResult);

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        if (mainFilePath != null && mainFileContent != null) {
            mocks.ContainerManager
                .Setup(c => c.ReadFileInContainerAsync(
                    It.IsAny<string>(),
                    It.Is<string>(p => p == mainFilePath),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(mainFileContent);

            mocks.TestGenerator
                .Setup(t => t.GenerateTestsAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("public class GeneratedTests { }");

            // Setup test file existence check (doesn't exist)
            mocks.ContainerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    It.Is<string>(cmd => cmd == "test"),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 1 });
        }

        SetupFileEditorChanges(mocks, actionPlan);
    }

    private static void SetupBuildFailure(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks,
        StepActionPlan actionPlan,
        BuildResult buildResult) {
        
        mocks.StepAnalyzer
            .Setup(a => a.AnalyzeStepAsync(
                It.IsAny<PlanStep>(),
                It.IsAny<RepositoryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionPlan);

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        // Mock ContainerManager for reading files (needed for ModifyFile operations)
        mocks.ContainerManager
            .Setup(c => c.ReadFileInContainerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("existing content");

        SetupFileEditorChanges(mocks, actionPlan);
    }

    private static void SetupTestFailure(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks,
        StepActionPlan actionPlan,
        BuildResult buildResult,
        TestValidationResult testResult,
        QualityResult qualityResult,
        string mainFilePath,
        string mainFileContent) {
        
        SetupBasicMocks(mocks, actionPlan, testResult, qualityResult);

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        mocks.ContainerManager
            .Setup(c => c.ReadFileInContainerAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p == mainFilePath),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mainFileContent);

        mocks.TestGenerator
            .Setup(t => t.GenerateTestsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class GeneratedTests { }");

        // Setup test file existence check (doesn't exist)
        mocks.ContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                It.Is<string>(cmd => cmd == "test"),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 1 });

        SetupFileEditorChanges(mocks, actionPlan);
    }

    private static void SetupMultipleActions(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks,
        StepActionPlan actionPlan,
        BuildResult buildResult,
        TestValidationResult testResult,
        QualityResult qualityResult) {
        
        SetupBasicMocks(mocks, actionPlan, testResult, qualityResult);

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        mocks.ContainerManager
            .Setup(c => c.ReadFileInContainerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("existing content");

        var changes = new List<FileChange> {
            new() { Type = ChangeType.Created, Path = "/workspace/NewService.cs", NewContent = "new" },
            new() { Type = ChangeType.Modified, Path = "/workspace/ExistingService.cs", OldContent = "old", NewContent = "modified" },
            new() { Type = ChangeType.Deleted, Path = "/workspace/OldService.cs", OldContent = "deleted" }
        };

        mocks.FileEditor
            .Setup(f => f.GetChanges())
            .Returns(changes);
    }

    private static void SetupTestGeneration(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks,
        StepActionPlan actionPlan,
        BuildResult buildResult,
        TestValidationResult testResult,
        QualityResult qualityResult) {
        
        SetupBasicMocks(mocks, actionPlan, testResult, qualityResult);

        mocks.BuildVerifier
            .Setup(b => b.VerifyBuildAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(buildResult);

        mocks.ContainerManager
            .Setup(c => c.ReadFileInContainerAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p == "/workspace/Service.cs"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Service { }");

        mocks.TestGenerator
            .Setup(t => t.GenerateTestsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class ServiceTests { [Fact] public void Test1() { } }");

        // Setup test file existence check (doesn't exist)
        mocks.ContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                It.Is<string>(cmd => cmd == "test"),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 1 });

        var changes = new List<FileChange> {
            new() { Type = ChangeType.Created, Path = "/workspace/Service.cs", NewContent = "public class Service { }" },
            new() { Type = ChangeType.Created, Path = "/workspace/ServiceTests.cs", NewContent = "tests" }
        };

        mocks.FileEditor
            .Setup(f => f.GetChanges())
            .Returns(changes);
    }

    private static void SetupBasicMocks(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks,
        StepActionPlan actionPlan,
        TestValidationResult testResult,
        QualityResult qualityResult) {
        
        mocks.StepAnalyzer
            .Setup(a => a.AnalyzeStepAsync(
                It.IsAny<PlanStep>(),
                It.IsAny<RepositoryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(actionPlan);

        mocks.TestValidator
            .Setup(t => t.RunAndValidateTestsAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(testResult);

        mocks.QualityChecker
            .Setup(q => q.CheckAndFixAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(qualityResult);

        // Mock ContainerManager for reading files (needed for ModifyFile operations)
        mocks.ContainerManager
            .Setup(c => c.ReadFileInContainerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("existing content");

        SetupFileEditorChanges(mocks, actionPlan);
    }

    private static void SetupFileEditorChanges(
        (Mock<IStepAnalyzer> StepAnalyzer,
         Mock<ICodeGenerator> CodeGenerator,
         Mock<ITestGenerator> TestGenerator,
         Mock<IFileEditor> FileEditor,
         Mock<IBuildVerifier> BuildVerifier,
         Mock<ITestValidator> TestValidator,
         Mock<ICodeQualityChecker> QualityChecker,
         Mock<IFileAnalyzer> FileAnalyzer,
         Mock<IContainerManager> ContainerManager,
         TestLogger<SmartStepExecutor> Logger) mocks,
        StepActionPlan actionPlan) {
        
        var changes = actionPlan.Actions.Select(a => new FileChange {
            Type = a.Type switch {
                ActionType.CreateFile => ChangeType.Created,
                ActionType.ModifyFile => ChangeType.Modified,
                ActionType.DeleteFile => ChangeType.Deleted,
                _ => ChangeType.Created
            },
            Path = a.FilePath,
            NewContent = a.Request.Content
        }).ToList();

        mocks.FileEditor
            .Setup(f => f.GetChanges())
            .Returns(changes);
    }

    // Test helper class
    private class TestLogger<T> : ILogger<T> {
        public List<string> LoggedMessages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) {
            LoggedMessages.Add(formatter(state, exception));
        }
    }
}
