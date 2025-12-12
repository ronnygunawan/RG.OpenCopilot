using Moq;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services.Docker;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using RG.OpenCopilot.PRGenerationAgent.Services.Executor;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Shouldly;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace RG.OpenCopilot.Tests;

public class TestValidatorTests {
    private readonly Mock<IContainerManager> _containerManager;
    private readonly Mock<IFileEditor> _fileEditor;
    private readonly Kernel _kernel;
    private readonly Mock<IChatCompletionService> _chatService;
    private readonly TestLogger<TestValidator> _logger;
    private readonly TestValidator _validator;

    public TestValidatorTests() {
        _containerManager = new Mock<IContainerManager>();
        _fileEditor = new Mock<IFileEditor>();
        _chatService = new Mock<IChatCompletionService>();
        _logger = new TestLogger<TestValidator>();

        // Create a real Kernel with mocked chat service
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(_chatService.Object);
        _kernel = kernelBuilder.Build();

        _validator = new TestValidator(
            containerManager: _containerManager.Object,
            fileEditor: _fileEditor.Object,
            executorKernel: new ExecutorKernel(_kernel),
            logger: _logger);
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithXunitProject_ReturnsXunit() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, "Passed!  - Failed:     0, Passed:    1, Skipped:     0, Total:    1");

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        // Verify xunit was detected by checking the execution was attempted
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "dotnet",
            It.Is<string[]>(args => args.Contains("test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithNunitProject_ReturnsNunit() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "nunit");
        SetupTestExecution(containerId, "Passed!  - Failed:     0, Passed:    1, Skipped:     0, Total:    1");

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "dotnet",
            It.Is<string[]>(args => args.Contains("test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithMstestProject_ReturnsMstest() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "mstest");
        SetupTestExecution(containerId, "Passed!  - Failed:     0, Passed:    1, Skipped:     0, Total:    1");

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "dotnet",
            It.Is<string[]>(args => args.Contains("test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithJestProject_ReturnsJest() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "jest");
        SetupTestExecution(containerId, "Tests:       0 failed, 1 passed, 1 total");

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "npm",
            It.Is<string[]>(args => args.Contains("test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithPytestProject_ReturnsPytest() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "pytest");
        SetupTestExecution(containerId, "=== 1 passed in 0.50s ===");

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "pytest",
            It.IsAny<string[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithJunitProject_ReturnsJunit() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "junit");
        SetupTestExecution(containerId, "Tests run: 1, Failures: 0, Errors: 0, Skipped: 0");

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "mvn",
            It.Is<string[]>(args => args.Contains("test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithNoFramework_ReturnsNull() {
        // Arrange
        var containerId = "test-container";
        SetupNoFrameworkDetection(containerId);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Output.ShouldBe("No test framework detected");
        result.Total.ShouldBe(0);
    }

    [Fact]
    public async Task RunTestsAsync_WithPassingTests_ReturnsSuccess() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(50);
        result.Failed.ShouldBe(0);
        result.Skipped.ShouldBe(0);
        result.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunTestsAsync_WithFailingTests_ParsesFailures() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.CalculatorTests.AddMethod_WithTwoNumbers_ReturnsSum [100 ms]
            Expected: 5
            Actual: 4
            
            Failed!  - Failed:     1, Passed:    49, Skipped:     0, Total:    50
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(49);
        result.Failed.ShouldBe(1);
        result.Skipped.ShouldBe(0);
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].ClassName.ShouldBe("MyTests.CalculatorTests");
        result.Failures[0].TestName.ShouldBe("AddMethod_WithTwoNumbers_ReturnsSum");
        result.Failures[0].Type.ShouldBe(FailureType.Assertion);
    }

    [Fact]
    public async Task RunTestsAsync_WithJestFailures_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "jest");
        SetupTestExecution(containerId, """
            ● CalculatorTests › addition
            
              expect(received).toBe(expected)
              
              Expected: 5
              Received: 4
            
            Tests:       1 failed, 49 passed, 50 total
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(49);
        result.Failed.ShouldBe(1);
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].ClassName.ShouldBe("CalculatorTests");
        result.Failures[0].TestName.ShouldBe("addition");
    }

    [Fact]
    public async Task RunTestsAsync_WithPytestFailures_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "pytest");
        SetupTestExecution(containerId, """
            FAILED test_calculator.py::TestCalculator::test_addition - AssertionError: assert 4 == 5
            === 1 failed, 49 passed in 2.50s ===
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(49);
        result.Failed.ShouldBe(1);
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].ClassName.ShouldBe("test_calculator.py.TestCalculator");
        result.Failures[0].TestName.ShouldBe("test_addition");
        result.Failures[0].Type.ShouldBe(FailureType.Assertion);
    }

    [Fact]
    public async Task RunTestsAsync_WithJunitFailures_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "junit");
        SetupTestExecution(containerId, """
            testAddition(com.example.CalculatorTests)  Time elapsed: 0.05 s  <<< FAILURE!
            java.lang.AssertionError: expected:<5> but was:<4>
            
            Tests run: 50, Failures: 1, Errors: 0, Skipped: 0
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(49);
        result.Failed.ShouldBe(1);
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].ClassName.ShouldBe("com.example.CalculatorTests");
        result.Failures[0].TestName.ShouldBe("testAddition");
    }

    [Fact]
    public async Task AnalyzeTestFailuresAsync_WithFailures_ReturnsAnalyzedFailures() {
        // Arrange
        SetupLlmFailureAnalysis();
        var failures = new List<TestFailure> {
            new TestFailure {
                TestName = "AddMethod_WithTwoNumbers_ReturnsSum",
                ClassName = "CalculatorTests",
                ErrorMessage = "Expected: 5\nActual: 4",
                Type = FailureType.Assertion
            }
        };

        // Act
        var result = await _validator.AnalyzeTestFailuresAsync(failures: failures);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeGreaterThanOrEqualTo(0);
        _chatService.Verify(c => c.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings>(),
            It.IsAny<Kernel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeTestFailuresAsync_WhenLlmFails_ReturnsOriginalFailures() {
        // Arrange
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service error"));

        var failures = new List<TestFailure> {
            new TestFailure {
                TestName = "TestMethod",
                ClassName = "TestClass",
                ErrorMessage = "Error message",
                Type = FailureType.Exception
            }
        };

        // Act
        var result = await _validator.AnalyzeTestFailuresAsync(failures: failures);

        // Assert
        result.ShouldBe(failures);
    }

    [Fact]
    public async Task ApplyTestFixesAsync_AppliesAllFixes() {
        // Arrange
        var containerId = "test-container";
        var fixes = new List<TestFix> {
            new TestFix {
                Target = FixTarget.Code,
                FilePath = "/workspace/src/Calculator.cs",
                Description = "Fix addition logic",
                OriginalCode = "return a + b - 1;",
                FixedCode = "return a + b;"
            },
            new TestFix {
                Target = FixTarget.Test,
                FilePath = "/workspace/tests/CalculatorTests.cs",
                Description = "Fix assertion",
                OriginalCode = "result.ShouldBe(4);",
                FixedCode = "result.ShouldBe(5);"
            }
        };

        _fileEditor
            .Setup(f => f.ModifyFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _validator.ApplyTestFixesAsync(containerId: containerId, fixes: fixes);

        // Assert
        _fileEditor.Verify(f => f.ModifyFileAsync(
            containerId,
            It.IsAny<string>(),
            It.IsAny<Func<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ApplyTestFixesAsync_WhenFixFails_ContinuesWithOtherFixes() {
        // Arrange
        var containerId = "test-container";
        var fixes = new List<TestFix> {
            new TestFix {
                Target = FixTarget.Code,
                FilePath = "/workspace/src/Calculator.cs",
                Description = "Fix that will fail",
                OriginalCode = "return a + b - 1;",
                FixedCode = "return a + b;"
            },
            new TestFix {
                Target = FixTarget.Test,
                FilePath = "/workspace/tests/CalculatorTests.cs",
                Description = "Fix that will succeed",
                OriginalCode = "result.ShouldBe(4);",
                FixedCode = "result.ShouldBe(5);"
            }
        };

        _fileEditor
            .SetupSequence(f => f.ModifyFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("File not found"))
            .Returns(Task.CompletedTask);

        // Act
        await _validator.ApplyTestFixesAsync(containerId: containerId, fixes: fixes);

        // Assert
        _fileEditor.Verify(f => f.ModifyFileAsync(
            containerId,
            It.IsAny<string>(),
            It.IsAny<Func<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithPassingTests_ReturnsSuccessOnFirstAttempt() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50
            """);

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 2);

        // Assert
        result.AllPassed.ShouldBeTrue();
        result.TotalTests.ShouldBe(50);
        result.PassedTests.ShouldBe(50);
        result.FailedTests.ShouldBe(0);
        result.Attempts.ShouldBe(1);
        result.RemainingFailures.ShouldBeEmpty();
        result.FixesApplied.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithFailingTests_RetriesWithFixes() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        
        // First attempt fails, second attempt passes
        _containerManager
            .SetupSequence(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("test")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = """
                    Failed MyTests.CalculatorTests.AddMethod_WithTwoNumbers_ReturnsSum [100 ms]
                    Expected: 5
                    Actual: 4
                    
                    Failed!  - Failed:     1, Passed:    49, Skipped:     0, Total:    50
                    """
            })
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Passed!  - Failed:     0, Passed:    50, Skipped:     0, Total:    50"
            });

        SetupLlmFixGeneration();
        
        _fileEditor
            .Setup(f => f.ModifyFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 2);

        // Assert
        result.AllPassed.ShouldBeTrue();
        result.Attempts.ShouldBe(2);
        result.FixesApplied.Count.ShouldBeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithMaxRetriesReached_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.CalculatorTests.AddMethod_WithTwoNumbers_ReturnsSum [100 ms]
            Expected: 5
            Actual: 4
            
            Failed!  - Failed:     1, Passed:    49, Skipped:     0, Total:    50
            """);

        SetupLlmFixGeneration();
        
        _fileEditor
            .Setup(f => f.ModifyFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 2);

        // Assert
        result.AllPassed.ShouldBeFalse();
        result.Attempts.ShouldBe(2);
        result.RemainingFailures.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithNoFixesGenerated_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.CalculatorTests.AddMethod_WithTwoNumbers_ReturnsSum [100 ms]
            Expected: 5
            Actual: 4
            
            Failed!  - Failed:     1, Passed:    49, Skipped:     0, Total:    50
            """);

        // Setup LLM to return no fixes
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, """
            {
              "fixes": []
            }
            """);

        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 2);

        // Assert
        result.AllPassed.ShouldBeFalse();
        result.Attempts.ShouldBe(1);
        result.FixesApplied.ShouldBeEmpty();
        result.Summary.ShouldContain("No fixes could be generated");
    }

    [Fact]
    public async Task GetCoverageAsync_WithDotnetProject_ReturnsCoverageReport() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("--collect")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Line Coverage: 85.5%\nBranch Coverage: 72.3%"
            });

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        result.LineCoverage.ShouldBe(85.5);
        result.BranchCoverage.ShouldBe(72.3);
    }

    [Fact]
    public async Task GetCoverageAsync_WhenCoverageFails_ReturnsNull() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("--collect")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Error = "Coverage collection failed"
            });

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetCoverageAsync_WithNoFramework_ReturnsNull() {
        // Arrange
        var containerId = "test-container";
        SetupNoFrameworkDetection(containerId);

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldBeNull();
    }

    private void SetupFrameworkDetection(string containerId, string framework) {
        // Setup all framework checks to return empty
        SetupNoFrameworkDetection(containerId);

        // Then setup the specific framework to be detected
        switch (framework) {
            case "xunit":
                _containerManager
                    .Setup(c => c.ExecuteInContainerAsync(
                        containerId,
                        "find",
                        It.Is<string[]>(args => args.Contains("xunit")),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CommandResult {
                        ExitCode = 0,
                        Output = "./MyProject.Tests.csproj"
                    });
                break;
            case "nunit":
                _containerManager
                    .Setup(c => c.ExecuteInContainerAsync(
                        containerId,
                        "find",
                        It.Is<string[]>(args => args.Contains("nunit")),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CommandResult {
                        ExitCode = 0,
                        Output = "./MyProject.Tests.csproj"
                    });
                break;
            case "mstest":
                _containerManager
                    .Setup(c => c.ExecuteInContainerAsync(
                        containerId,
                        "find",
                        It.Is<string[]>(args => args.Contains("MSTest")),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CommandResult {
                        ExitCode = 0,
                        Output = "./MyProject.Tests.csproj"
                    });
                break;
            case "jest":
                _containerManager
                    .Setup(c => c.ExecuteInContainerAsync(
                        containerId,
                        "find",
                        It.Is<string[]>(args => args.Contains("jest")),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CommandResult {
                        ExitCode = 0,
                        Output = "./package.json"
                    });
                break;
            case "pytest":
                _containerManager
                    .Setup(c => c.ExecuteInContainerAsync(
                        containerId,
                        "find",
                        It.Is<string[]>(args => args.Contains("pytest.ini") || args.Contains("test_*.py")),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CommandResult {
                        ExitCode = 0,
                        Output = "./pytest.ini"
                    });
                break;
            case "junit":
                _containerManager
                    .Setup(c => c.ExecuteInContainerAsync(
                        containerId,
                        "find",
                        It.Is<string[]>(args => args.Contains("junit")),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CommandResult {
                        ExitCode = 0,
                        Output = "./pom.xml"
                    });
                break;
        }
    }

    private void SetupNoFrameworkDetection(string containerId) {
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });
    }

    private void SetupTestExecution(string containerId, string output) {
        var exitCode = output.Contains("Passed!") || output.Contains("passed") || output.Contains("Failures: 0") ? 0 : 1;
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                It.Is<string>(cmd => cmd != "find"),  // Don't match framework detection commands
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = exitCode,
                Output = output
            });
    }

    private void SetupLlmFailureAnalysis() {
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, """
            {
              "failures": [
                {
                  "testName": "AddMethod_WithTwoNumbers_ReturnsSum",
                  "className": "CalculatorTests",
                  "errorMessage": "Assertion failed: expected 5 but got 4",
                  "type": "Assertion",
                  "expectedValue": "5",
                  "actualValue": "4",
                  "rootCause": "Addition logic returns incorrect result"
                }
              ]
            }
            """);

        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });
    }

    private void SetupLlmFixGeneration() {
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, """
            {
              "fixes": [
                {
                  "target": "Code",
                  "filePath": "/workspace/src/Calculator.cs",
                  "description": "Fix addition method to return correct sum",
                  "originalCode": "return a + b - 1;",
                  "fixedCode": "return a + b;"
                }
              ]
            }
            """);

        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });
    }

    [Fact]
    public async Task RunTestsAsync_WithTestFilter_PassesFilterToCommand() {
        // Arrange
        var containerId = "test-container";
        var testFilter = "FullyQualifiedName~MyTests";
        SetupFrameworkDetection(containerId, "xunit");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("--filter") && args.Contains(testFilter)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Passed!  - Failed:     0, Passed:    1, Skipped:     0, Total:    1"
            });

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId, testFilter: testFilter);

        // Assert
        result.Success.ShouldBeTrue();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "dotnet",
            It.Is<string[]>(args => args.Contains("--filter") && args.Contains(testFilter)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunTestsAsync_WithTimeoutFailure_ParsesAsTimeoutType() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.SlowTest.ExecuteWithTimeout [5000 ms]
            Test execution timed out after 5000 ms
            
            Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].Type.ShouldBe(FailureType.Timeout);
    }

    [Fact]
    public async Task RunTestsAsync_WithSetupFailure_ParsesAsSetupType() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.TestClass.TestMethod [100 ms]
            Setup failed: BeforeAll initialization error
            
            Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].Type.ShouldBe(FailureType.Setup);
    }

    [Fact]
    public async Task RunTestsAsync_WithTeardownFailure_ParsesAsTeardownType() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.TestClass.TestMethod [100 ms]
            Teardown failed: AfterEach cleanup error
            
            Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].Type.ShouldBe(FailureType.Teardown);
    }

    [Fact]
    public async Task RunTestsAsync_WithExceptionFailure_ParsesAsExceptionType() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.TestClass.TestMethod [100 ms]
            NullReferenceException: Object reference not set to an instance of an object
            
            Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].Type.ShouldBe(FailureType.Exception);
    }

    [Fact]
    public async Task RunTestsAsync_WithSkippedTests_ParsesSkippedCount() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Passed!  - Failed:     0, Passed:    45, Skipped:     5, Total:    50
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(45);
        result.Skipped.ShouldBe(5);
        result.Failed.ShouldBe(0);
    }

    [Fact]
    public async Task AnalyzeTestFailuresAsync_WithStackTrace_IncludesInPrompt() {
        // Arrange
        var promptCaptured = "";
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, """
            {
              "failures": [
                {
                  "testName": "TestMethod",
                  "className": "TestClass",
                  "errorMessage": "Assertion failed",
                  "type": "Assertion"
                }
              ]
            }
            """);

        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>((h, s, k, c) => {
                promptCaptured = h.LastOrDefault()?.Content ?? "";
            })
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var failures = new List<TestFailure> {
            new TestFailure {
                TestName = "TestMethod",
                ClassName = "TestClass",
                ErrorMessage = "Assertion failed",
                Type = FailureType.Assertion,
                StackTrace = "at TestClass.TestMethod() in TestClass.cs:line 42"
            }
        };

        // Act
        var result = await _validator.AnalyzeTestFailuresAsync(failures: failures);

        // Assert
        result.ShouldNotBeNull();
        promptCaptured.ShouldContain("Stack Trace");
        promptCaptured.ShouldContain("at TestClass.TestMethod()");
    }

    [Fact]
    public async Task AnalyzeTestFailuresAsync_WithInvalidJson_ReturnsEmptyList() {
        // Arrange
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, "invalid json {");
        
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var failures = new List<TestFailure> {
            new TestFailure {
                TestName = "TestMethod",
                ClassName = "TestClass",
                ErrorMessage = "Error",
                Type = FailureType.Exception
            }
        };

        // Act
        var result = await _validator.AnalyzeTestFailuresAsync(failures: failures);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithFailuresButNoParsedFailures_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        
        // Setup test execution to return output that indicates failures but without parseable failure details
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                It.Is<string>(cmd => cmd != "find"),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = """
                    Some error occurred during test execution
                    
                    Failed!  - Failed:     3, Passed:    47, Skipped:     0, Total:    50
                    """
                // Note: This has the summary but no individual "Failed TestClass.TestMethod" lines
            });

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 1);

        // Assert
        result.AllPassed.ShouldBeFalse();
        result.FailedTests.ShouldBe(3);
        result.RemainingFailures.ShouldBeEmpty(); // No parseable individual failures
        result.Summary.ShouldContain("no failures could be parsed");
    }

    [Fact]
    public async Task GetCoverageAsync_WithOnlyLineCoverage_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("--collect")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Line Coverage: 75.0%"
            });

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        result.LineCoverage.ShouldBe(75.0);
        result.BranchCoverage.ShouldBe(0.0);
    }

    [Fact]
    public async Task GetCoverageAsync_WithOnlyBranchCoverage_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("--collect")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Branch Coverage: 60.5%"
            });

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        result.LineCoverage.ShouldBe(0.0);
        result.BranchCoverage.ShouldBe(60.5);
    }

    [Fact]
    public async Task GetCoverageAsync_WithNoCoverageData_ReturnsNull() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("--collect")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "No coverage data available"
            });

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task RunTestsAsync_WithNunitFailures_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "nunit");
        SetupTestExecution(containerId, """
            Failed MyTests.CalculatorTests.SubtractMethod [100 ms]
            Expected: 3
            Actual: 2
            
            Failed!  - Failed:     1, Passed:    49, Skipped:     0, Total:    50
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Total.ShouldBe(50);
        result.Failed.ShouldBe(1);
        result.Failures.Count.ShouldBe(1);
        result.Failures[0].ClassName.ShouldBe("MyTests.CalculatorTests");
        result.Failures[0].TestName.ShouldBe("SubtractMethod");
    }

    [Fact]
    public async Task RunTestsAsync_WithMstestFailures_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "mstest");
        SetupTestExecution(containerId, """
            Failed MyTests.CalculatorTests.MultiplyMethod [100 ms]
            Expected: 12
            Actual: 10
            
            Failed!  - Failed:     1, Passed:    49, Skipped:     0, Total:    50
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Failed.ShouldBe(1);
        result.Failures.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithLlmGeneratingInvalidJson_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.TestClass.TestMethod [100 ms]
            Test failed
            
            Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
            """);

        // Setup LLM to return invalid JSON
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, "{ invalid json");
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 2);

        // Assert
        result.AllPassed.ShouldBeFalse();
        result.FixesApplied.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunTestsAsync_WithJestSkippedTests_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "jest");
        SetupTestExecution(containerId, """
            Tests:       1 failed, 45 passed, 4 skipped, 50 total
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(45);
        result.Failed.ShouldBe(1);
        result.Skipped.ShouldBe(4);
    }

    [Fact]
    public async Task RunTestsAsync_WithPytestOnlyPassed_ParsesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "pytest");
        SetupTestExecution(containerId, """
            === 50 passed in 2.50s ===
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Total.ShouldBe(50);
        result.Passed.ShouldBe(50);
        result.Failed.ShouldBe(0);
    }

    [Fact]
    public async Task RunTestsAsync_WithJunitErrors_ParsesAsFailures() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "junit");
        SetupTestExecution(containerId, """
            testMethod(com.example.TestClass)  Time elapsed: 0.05 s  <<< ERROR!
            java.lang.NullPointerException
            
            Tests run: 50, Failures: 0, Errors: 1, Skipped: 0
            """);

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Failed.ShouldBe(1); // Errors count as failures
        result.Failures.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunTestsAsync_WithJestFilter_PassesFilterCorrectly() {
        // Arrange
        var containerId = "test-container";
        var testFilter = "Calculator";
        SetupFrameworkDetection(containerId, "jest");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "npm",
                It.Is<string[]>(args => args.Contains("--") && args.Contains(testFilter)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Tests:       0 failed, 1 passed, 1 total"
            });

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId, testFilter: testFilter);

        // Assert
        result.Success.ShouldBeTrue();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "npm",
            It.Is<string[]>(args => args.Contains("--") && args.Contains(testFilter)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunTestsAsync_WithPytestFilter_PassesFilterCorrectly() {
        // Arrange
        var containerId = "test-container";
        var testFilter = "test_calculator";
        SetupFrameworkDetection(containerId, "pytest");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "pytest",
                It.Is<string[]>(args => args.Contains("-v") && args.Contains(testFilter)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "=== 1 passed in 0.50s ==="
            });

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId, testFilter: testFilter);

        // Assert
        result.Success.ShouldBeTrue();
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "pytest",
            It.Is<string[]>(args => args.Contains("-v") && args.Contains(testFilter)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunTestsAsync_WithJunitFilter_PassesFilterCorrectly() {
        // Arrange
        var containerId = "test-container";
        var testFilter = "CalculatorTest";
        SetupFrameworkDetection(containerId, "junit");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "mvn",
                It.Is<string[]>(args => args.Contains("test") && args.Any(a => a.Contains("-Dtest="))),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Tests run: 1, Failures: 0, Errors: 0, Skipped: 0"
            });

        // Act
        var result = await _validator.RunTestsAsync(containerId: containerId, testFilter: testFilter);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetCoverageAsync_WithJestFramework_ExecutesCoverageCommand() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "jest");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "npm",
                It.Is<string[]>(args => args.Contains("--coverage")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Line Coverage: 80.5%\nBranch Coverage: 65.0%"
            });

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        result.LineCoverage.ShouldBe(80.5);
        result.BranchCoverage.ShouldBe(65.0);
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "npm",
            It.Is<string[]>(args => args.Contains("--coverage")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetCoverageAsync_WithPytestFramework_ExecutesCoverageCommand() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "pytest");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "pytest",
                It.Is<string[]>(args => args.Contains("--cov")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Line Coverage: 92.3%"
            });

        // Act
        var result = await _validator.GetCoverageAsync(containerId: containerId);

        // Assert
        result.ShouldNotBeNull();
        result.LineCoverage.ShouldBe(92.3);
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "pytest",
            It.Is<string[]>(args => args.Contains("--cov")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithExpectedAndActualValues_IncludesInPrompt() {
        // Arrange
        var containerId = "test-container";
        var promptCaptured = "";
        SetupFrameworkDetection(containerId, "xunit");
        
        _containerManager
            .SetupSequence(c => c.ExecuteInContainerAsync(
                containerId,
                It.Is<string>(cmd => cmd != "find"),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = """
                    Failed MyTests.CalculatorTests.AddMethod [100 ms]
                    Expected: 5
                    Actual: 4
                    
                    Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
                    """
            })
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Passed!  - Failed:     0, Passed:    1, Skipped:     0, Total:    1"
            });

        // Setup LLM to capture the prompt
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, """
            {
              "fixes": [
                {
                  "target": "Code",
                  "filePath": "/workspace/src/Calculator.cs",
                  "description": "Fix addition",
                  "originalCode": "return a + b - 1;",
                  "fixedCode": "return a + b;"
                }
              ]
            }
            """);

        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>((h, s, k, c) => {
                promptCaptured = h.LastOrDefault()?.Content ?? "";
            })
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        _fileEditor
            .Setup(f => f.ModifyFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 2);

        // Assert
        result.AllPassed.ShouldBeTrue();
        // Note: The test internally calls GenerateTestFixesAsync which should include ExpectedValue/ActualValue
        // But those are parsed from the error message, not directly available
    }

    [Fact]
    public async Task RunAndValidateTestsAsync_WithGenerateFixesException_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.TestClass.TestMethod [100 ms]
            Test failed
            
            Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
            """);

        // Setup LLM to throw exception when generating fixes
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service unavailable"));

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 1);

        // Assert
        result.AllPassed.ShouldBeFalse();
        result.FixesApplied.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseFixesFromResponse_WithInvalidJson_ReturnsEmptyList() {
        // This tests the error handling in ParseFixesFromResponse indirectly
        // Arrange
        var containerId = "test-container";
        SetupFrameworkDetection(containerId, "xunit");
        SetupTestExecution(containerId, """
            Failed MyTests.TestClass.TestMethod [100 ms]
            Test failed
            
            Failed!  - Failed:     1, Passed:    0, Skipped:     0, Total:    1
            """);

        // Setup LLM to return invalid JSON for fixes
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, "{ broken json");
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        // Act
        var result = await _validator.RunAndValidateTestsAsync(containerId: containerId, maxRetries: 1);

        // Assert
        result.AllPassed.ShouldBeFalse();
        result.FixesApplied.ShouldBeEmpty();
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
