using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Moq;
using Shouldly;
using RG.OpenCopilot.App.CodeGeneration;

namespace RG.OpenCopilot.Tests;

public class TestGeneratorTests {
    [Fact]
    public async Task GenerateTestsAsync_WithValidCode_ReturnsGeneratedTests() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var codeContent = """
            public class Calculator {
                public int Add(int a, int b) {
                    return a + b;
                }
            }
            """;

        var generatedTests = """
            using Xunit;
            using Shouldly;

            public class CalculatorTests {
                [Fact]
                public void Add_WithPositiveNumbers_ReturnsSum() {
                    // Arrange
                    var calculator = new Calculator();

                    // Act
                    var result = calculator.Add(2, 3);

                    // Assert
                    result.ShouldBe(5);
                }

                [Fact]
                public void Add_WithNegativeNumbers_ReturnsSum() {
                    // Arrange
                    var calculator = new Calculator();

                    // Act
                    var result = calculator.Add(-2, -3);

                    // Assert
                    result.ShouldBe(-5);
                }
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        // Setup file analyzer to return empty test list (no existing tests)
        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: codeContent,
            testFramework: "xUnit");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("class CalculatorTests");
        result.ShouldContain("[Fact]");
        result.ShouldContain("Add_WithPositiveNumbers_ReturnsSum");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithXUnitProject_ReturnsXUnit() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="xunit" Version="2.5.0" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.5.0" />
              </ItemGroup>
            </Project>
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Test.csproj" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "Test.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(csprojContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBe("xUnit");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithNUnitProject_ReturnsNUnit() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="nunit" Version="3.13.0" />
                <PackageReference Include="NUnit3TestAdapter" Version="4.2.0" />
              </ItemGroup>
            </Project>
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Test.csproj" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "Test.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(csprojContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBe("NUnit");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithJestPackageJson_ReturnsJest() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var packageJsonContent = """
            {
              "name": "test-project",
              "devDependencies": {
                "jest": "^29.0.0",
                "@types/jest": "^29.0.0"
              },
              "scripts": {
                "test": "jest"
              }
            }
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "package.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "package.json" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "package.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(packageJsonContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBe("Jest");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithPytestRequirements_ReturnsPytest() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var requirementsContent = """
            pytest==7.4.0
            pytest-cov==4.1.0
            pytest-asyncio==0.21.0
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "package.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "requirements*.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "requirements.txt" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "requirements.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirementsContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBe("pytest");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithNoTestFramework_ReturnsNull() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindExistingTestsAsync_WithXUnitTests_ReturnsTestFiles() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testFileContent = """
            using Xunit;
            using Shouldly;

            public class CalculatorTests {
                [Fact]
                public void Add_WithPositiveNumbers_ReturnsSum() {
                    var calculator = new Calculator();
                    var result = calculator.Add(2, 3);
                    result.ShouldBe(5);
                }
            }
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*Tests.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "CalculatorTests.cs" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.Is<string>(p => p != "*Tests.cs"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "CalculatorTests.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFileContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.FindExistingTestsAsync("test-container");

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(1);
        result[0].Path.ShouldBe("CalculatorTests.cs");
        result[0].Content.ShouldContain("CalculatorTests");
        result[0].Framework.ShouldBe("xUnit");
    }

    [Fact]
    public async Task FindExistingTestsAsync_WithNoTests_ReturnsEmptyList() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.FindExistingTestsAsync("test-container");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WithExistingTests_ReturnsPattern() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "CalculatorTests.cs",
                Content = """
                    using Xunit;
                    using Shouldly;

                    public class CalculatorTests {
                        [Fact]
                        public void Add_WithPositiveNumbers_ReturnsSum() {
                            // Arrange
                            var calculator = new Calculator();

                            // Act
                            var result = calculator.Add(2, 3);

                            // Assert
                            result.ShouldBe(5);
                        }
                    }
                    """,
                Framework = "xUnit"
            }
        };

        var analysisResponse = """
            NamingConvention: MethodName_Scenario_ExpectedOutcome
            AssertionStyle: Shouldly
            UsesArrangeActAssert: yes
            CommonImports: using Xunit;, using Shouldly;
            BaseTestClass: none
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, analysisResponse);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.NamingConvention.ShouldBe("MethodName_Scenario_ExpectedOutcome");
        result.AssertionStyle.ShouldBe("Shouldly");
        result.UsesArrangeActAssert.ShouldBeTrue();
        result.CommonImports.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WithNoTests_ReturnsDefaultPattern() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.NamingConvention.ShouldBe("MethodName_Scenario_ExpectedOutcome");
        result.AssertionStyle.ShouldBe("Shouldly");
        result.UsesArrangeActAssert.ShouldBeTrue();
    }

    [Fact]
    public async Task RunTestsAsync_WithDotNetTests_ReturnsTestResult() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testOutput = """
            Test run for CalculatorTests.dll(.NETCoreApp,Version=v10.0)
            Microsoft (R) Test Execution Command Line Tool Version 17.8.0
            
            Starting test execution, please wait...
            
            Passed CalculatorTests.Add_WithPositiveNumbers_ReturnsSum [10 ms]
            Passed CalculatorTests.Add_WithNegativeNumbers_ReturnsSum [5 ms]
            
            Test Run Successful.
            Total tests: 2
                 Passed: 2
             Total time: 0.5 Seconds
            """;

        mockContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "dotnet",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = testOutput,
                Error = ""
            });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.RunTestsAsync("test-container", "CalculatorTests.cs");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.TotalTests.ShouldBe(2);
        result.PassedTests.ShouldBe(2);
        result.FailedTests.ShouldBe(0);
    }

    [Fact]
    public async Task RunTestsAsync_WithFailingTests_ReturnsFailures() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testOutput = """
            Test run for CalculatorTests.dll(.NETCoreApp,Version=v10.0)
            
            Failed CalculatorTests.Add_WithPositiveNumbers_ReturnsSum [10 ms]
              Error Message:
                Expected result to be 5, but was 6
            
            Passed CalculatorTests.Add_WithNegativeNumbers_ReturnsSum [5 ms]
            
            Test Run Failed.
            Total tests: 2
                 Passed: 1
                 Failed: 1
             Total time: 0.5 Seconds
            """;

        mockContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "dotnet",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = testOutput,
                Error = ""
            });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.RunTestsAsync("test-container", "CalculatorTests.cs");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.TotalTests.ShouldBe(2);
        result.PassedTests.ShouldBe(1);
        result.FailedTests.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateTestsAsync_UsesDetectedFrameworkWhenNotProvided() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="nunit" Version="3.13.0" />
              </ItemGroup>
            </Project>
            """;

        var generatedTests = """
            using NUnit.Framework;

            public class CalculatorTests {
                [Test]
                public void Add_WithPositiveNumbers_ReturnsSum() {
                    var calculator = new Calculator();
                    var result = calculator.Add(2, 3);
                    Assert.AreEqual(5, result);
                }
            }
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Test.csproj" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.Is<string>(s => s != "*.csproj"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "Test.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(csprojContent);

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: "public class Calculator { }",
            testFramework: null); // Not providing framework

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("[Test]");
    }

    [Fact]
    public async Task GenerateTestsAsync_DefaultsToXUnitWhenNoFrameworkDetected() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var generatedTests = """
            using Xunit;

            public class CalculatorTests {
                [Fact]
                public void Add_WithPositiveNumbers_ReturnsSum() {
                    var calculator = new Calculator();
                    var result = calculator.Add(2, 3);
                    Assert.Equal(5, result);
                }
            }
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        ChatHistory? capturedChatHistory = null;
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (ch, _, _, _) => capturedChatHistory = ch)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: "public class Calculator { }",
            testFramework: null);

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var systemMessage = capturedChatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        systemMessage.ShouldNotBeNull();
        systemMessage.Content.ShouldContain("xUnit");
    }

    [Fact]
    public async Task GenerateTestsAsync_WhenLlmFails_ThrowsException() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await generator.GenerateTestsAsync(
                containerId: "test-container",
                codeFilePath: "Calculator.cs",
                codeContent: "public class Calculator { }",
                testFramework: "xUnit"));

        exception.Message.ShouldBe("LLM service unavailable");
    }

    [Fact]
    public async Task RunTestsAsync_WithJavaScriptTests_ParsesJestOutput() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testOutput = """
            PASS  src/calculator.test.js
              Calculator
                ✓ adds two numbers (3 ms)
                ✓ subtracts two numbers (1 ms)
            
            Tests:       2 passed, 2 total
            Time:        1.234 s
            """;

        mockContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "npm",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = testOutput,
                Error = ""
            });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.RunTestsAsync("test-container", "calculator.test.js");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.TotalTests.ShouldBe(2);
        result.PassedTests.ShouldBe(2);
        result.FailedTests.ShouldBe(0);
    }

    [Fact]
    public async Task RunTestsAsync_WithPythonTests_ParsesPytestOutput() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testOutput = """
            ============================= test session starts ==============================
            collected 5 items
            
            tests/test_calculator.py::test_add PASSED                                [ 20%]
            tests/test_calculator.py::test_subtract PASSED                           [ 40%]
            tests/test_calculator.py::test_multiply PASSED                           [ 60%]
            tests/test_calculator.py::test_divide_by_zero FAILED                     [ 80%]
            tests/test_calculator.py::test_divide PASSED                             [100%]
            
            =========================== 4 passed, 1 failed in 0.12s =======================
            """;

        mockContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "python",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = testOutput,
                Error = ""
            });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.RunTestsAsync("test-container", "test_calculator.py");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.TotalTests.ShouldBe(5);
        result.PassedTests.ShouldBe(4);
        result.FailedTests.ShouldBe(1);
    }

    [Fact]
    public async Task RunTestsAsync_WithUnknownFileExtension_ReturnsFailure() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.RunTestsAsync("test-container", "test.unknown");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Output.ShouldContain("Unknown test file extension");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithMarkdownCodeBlockResponse_ExtractsCleanCode() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var codeContent = "public class Calculator { }";

        var generatedTestsWithMarkdown = """
            ```csharp
            using Xunit;
            
            public class CalculatorTests {
                [Fact]
                public void Test() { }
            }
            ```
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTestsWithMarkdown);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: codeContent,
            testFramework: "xUnit");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotContain("```");
        result.ShouldContain("using Xunit;");
        result.ShouldContain("public class CalculatorTests");
    }

    [Fact]
    public async Task FindExistingTestsAsync_WithMultiplePatterns_LimitsToMaxFiles() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        // Setup to return more than MaxTestFilesToAnalyze files
        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*Tests.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Test1.cs", "Test2.cs", "Test3.cs", "Test4.cs", "Test5.cs", "Test6.cs" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.Is<string>(s => s != "*Tests.cs"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("using Xunit; public class Test { }");

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.FindExistingTestsAsync("test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBeLessThanOrEqualTo(5); // Should be limited to MaxTestFilesToAnalyze
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WithFluentAssertionsPattern_DetectsCorrectStyle() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "CalculatorTests.cs",
                Content = """
                    using FluentAssertions;
                    using Xunit;

                    public class CalculatorTests {
                        [Fact]
                        public void Add_WithPositiveNumbers_ReturnsSum() {
                            var calculator = new Calculator();
                            var result = calculator.Add(2, 3);
                            result.Should().Be(5);
                        }
                    }
                    """,
                Framework = "xUnit"
            }
        };

        var analysisResponse = """
            NamingConvention: MethodName_Scenario_ExpectedOutcome
            AssertionStyle: FluentAssertions
            UsesArrangeActAssert: no
            CommonImports: using FluentAssertions;, using Xunit;
            BaseTestClass: none
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, analysisResponse);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.AssertionStyle.ShouldBe("FluentAssertions");
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WhenLlmFails_FallsBackToHeuristics() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "CalculatorTests.cs",
                Content = """
                    using Xunit;
                    using Shouldly;

                    public class CalculatorTests {
                        [Fact]
                        public void Add_WithPositiveNumbers_ReturnsSum() {
                            // Arrange
                            var calculator = new Calculator();

                            // Act
                            var result = calculator.Add(2, 3);

                            // Assert
                            result.ShouldBe(5);
                        }
                    }
                    """,
                Framework = "xUnit"
            }
        };

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.AssertionStyle.ShouldBe("Shouldly");
        result.UsesArrangeActAssert.ShouldBeTrue();
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithMochaPackageJson_ReturnsMocha() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var packageJsonContent = """
            {
              "name": "test-project",
              "devDependencies": {
                "mocha": "^10.0.0",
                "chai": "^4.3.0"
              },
              "scripts": {
                "test": "mocha"
              }
            }
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "package.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "package.json" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "package.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(packageJsonContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBe("Mocha");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithMSTestProject_ReturnsMSTest() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var csprojContent = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="MSTest.TestAdapter" Version="3.0.0" />
                <PackageReference Include="MSTest.TestFramework" Version="3.0.0" />
              </ItemGroup>
            </Project>
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Test.csproj" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "Test.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(csprojContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBe("MSTest");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WithUnittestRequirements_ReturnsUnittest() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var requirementsContent = """
            unittest-xml-reporting==3.2.0
            coverage==7.2.0
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.csproj", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "package.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "requirements*.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "requirements.txt" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "requirements.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync(requirementsContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBe("unittest");
    }

    [Fact]
    public async Task FindExistingTestsAsync_WithJavaScriptTests_ReturnsTestFiles() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testFileContent = """
            describe('Calculator', () => {
                it('adds two numbers', () => {
                    expect(add(2, 3)).toBe(5);
                });
            });
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*Tests.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*Test.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*.test.js", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "calculator.test.js" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.Is<string>(s => s != "*.test.js" && s != "*Tests.cs" && s != "*Test.cs"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "calculator.test.js", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFileContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.FindExistingTestsAsync("test-container");

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(1);
        result[0].Path.ShouldBe("calculator.test.js");
        result[0].Framework.ShouldBe("Jest");
    }

    [Fact]
    public async Task FindExistingTestsAsync_WithPythonTests_ReturnsTestFiles() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testFileContent = """
            import pytest
            
            def test_add():
                assert add(2, 3) == 5
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.Is<string>(s => !s.StartsWith("test_")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "test_*.py", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "test_calculator.py" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "test_calculator.py", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testFileContent);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.FindExistingTestsAsync("test-container");

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(1);
        result[0].Path.ShouldBe("test_calculator.py");
        result[0].Framework.ShouldBe("pytest");
    }

    [Fact]
    public async Task RunTestsAsync_WithTypeScriptTests_ExecutesNpmTest() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var testOutput = """
            PASS  src/calculator.test.ts
              Calculator
                ✓ adds two numbers (2 ms)
            
            Tests:       1 passed, 1 total
            Time:        0.5 s
            """;

        mockContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "npm",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = testOutput,
                Error = ""
            });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.RunTestsAsync("test-container", "calculator.test.ts");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        mockContainerManager.Verify(c => c.ExecuteInContainerAsync(
            It.IsAny<string>(),
            "npm",
            It.Is<string[]>(args => args.Contains("test")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunTestsAsync_WhenExecutionFails_ReturnsFailureWithExceptionMessage() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        mockContainerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Container not found"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.RunTestsAsync("test-container", "test.cs");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Output.ShouldBe("Container not found");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithNUnitFramework_UsesNUnitSpecificPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var generatedTests = """
            using NUnit.Framework;

            public class CalculatorTests {
                [Test]
                public void Add_WithPositiveNumbers_ReturnsSum() {
                    var calculator = new Calculator();
                    var result = calculator.Add(2, 3);
                    Assert.AreEqual(5, result);
                }
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        ChatHistory? capturedChatHistory = null;
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (ch, _, _, _) => capturedChatHistory = ch)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: "public class Calculator { }",
            testFramework: "NUnit");

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var systemMessage = capturedChatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        systemMessage.ShouldNotBeNull();
        systemMessage.Content.ShouldContain("NUnit");
        systemMessage.Content.ShouldContain("[Test]");
        systemMessage.Content.ShouldContain("[SetUp]");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithMSTestFramework_UsesMSTestSpecificPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var generatedTests = """
            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class CalculatorTests {
                [TestMethod]
                public void Add_WithPositiveNumbers_ReturnsSum() {
                    var calculator = new Calculator();
                    var result = calculator.Add(2, 3);
                    Assert.AreEqual(5, result);
                }
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        ChatHistory? capturedChatHistory = null;
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (ch, _, _, _) => capturedChatHistory = ch)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: "public class Calculator { }",
            testFramework: "MSTest");

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var systemMessage = capturedChatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        systemMessage.ShouldNotBeNull();
        systemMessage.Content.ShouldContain("MSTest");
        systemMessage.Content.ShouldContain("[TestMethod]");
        systemMessage.Content.ShouldContain("[TestInitialize]");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithJestFramework_UsesJestSpecificPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var generatedTests = """
            describe('Calculator', () => {
                test('adds two numbers', () => {
                    const calculator = new Calculator();
                    expect(calculator.add(2, 3)).toBe(5);
                });
            });
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        ChatHistory? capturedChatHistory = null;
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (ch, _, _, _) => capturedChatHistory = ch)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "calculator.js",
            codeContent: "class Calculator { }",
            testFramework: "Jest");

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var systemMessage = capturedChatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        systemMessage.ShouldNotBeNull();
        systemMessage.Content.ShouldContain("Jest");
        systemMessage.Content.ShouldContain("describe()");
        systemMessage.Content.ShouldContain("expect()");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithPytestFramework_UsesPytestSpecificPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var generatedTests = """
            import pytest
            
            def test_add():
                calculator = Calculator()
                result = calculator.add(2, 3)
                assert result == 5
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        ChatHistory? capturedChatHistory = null;
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (ch, _, _, _) => capturedChatHistory = ch)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "calculator.py",
            codeContent: "class Calculator: pass",
            testFramework: "pytest");

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var systemMessage = capturedChatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        systemMessage.ShouldNotBeNull();
        systemMessage.Content.ShouldContain("pytest");
        systemMessage.Content.ShouldContain("test_");
        systemMessage.Content.ShouldContain("fixtures");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithPatternHavingBaseTestClass_IncludesInPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "CalculatorTests.cs",
                Content = """
                    using Xunit;
                    
                    public class CalculatorTests : BaseTestClass {
                        [Fact]
                        public void Test() { }
                    }
                    """,
                Framework = "xUnit"
            }
        };

        var analysisResponse = """
            NamingConvention: MethodName_Scenario_ExpectedOutcome
            AssertionStyle: Shouldly
            UsesArrangeActAssert: yes
            CommonImports: using Xunit;, using Shouldly;
            BaseTestClass: BaseTestClass
            """;

        var generatedTests = "public class CalculatorTests : BaseTestClass { }";

        var chatContent1 = new ChatMessageContent(AuthorRole.Assistant, analysisResponse);
        var chatContent2 = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        var callCount = 0;
        ChatHistory? capturedUserPrompt = null;
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (ch, _, _, _) => {
                    callCount++;
                    if (callCount == 2) { // Second call is for test generation
                        capturedUserPrompt = ch;
                    }
                })
            .ReturnsAsync((ChatHistory ch, PromptExecutionSettings _, Kernel _, CancellationToken _) => {
                return callCount == 1 
                    ? new List<ChatMessageContent> { chatContent1 }
                    : new List<ChatMessageContent> { chatContent2 };
            });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*Tests.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "CalculatorTests.cs" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.Is<string>(s => s != "*Tests.cs"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTests[0].Content);

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: "public class Calculator { }",
            testFramework: null);

        // Assert
        result.ShouldNotBeNull();
        capturedUserPrompt.ShouldNotBeNull();
        var userMessage = capturedUserPrompt.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("BaseTestClass");
    }

    [Fact]
    public async Task FindExistingTestsAsync_WhenReadFileFails_ContinuesWithOtherFiles() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), "*Tests.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "Test1.cs", "Test2.cs" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.Is<string>(s => s != "*Tests.cs"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // First file throws exception, second succeeds
        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "Test1.cs", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("File not accessible"));

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), "Test2.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("using Xunit; public class Test2 { }");

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.FindExistingTestsAsync("test-container");

        // Assert
        result.ShouldNotBeEmpty();
        result.Count.ShouldBe(1);
        result[0].Path.ShouldBe("Test2.cs");
    }

    [Fact]
    public async Task DetectTestFrameworkAsync_WhenExceptionOccurs_ReturnsNull() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("File system error"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.DetectTestFrameworkAsync("test-container");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WithAssertStyleTests_DetectsAssertStyle() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "CalculatorTests.cs",
                Content = """
                    using Xunit;

                    public class CalculatorTests {
                        [Fact]
                        public void Add_WithPositiveNumbers_ReturnsSum() {
                            var calculator = new Calculator();
                            var result = calculator.Add(2, 3);
                            Assert.Equal(5, result);
                        }
                    }
                    """,
                Framework = "xUnit"
            }
        };

        // Make LLM fail so it falls back to heuristics
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.AssertionStyle.ShouldBe("Assert");
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WithExpectStyleTests_DetectsExpectStyle() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "calculator.test.js",
                Content = """
                    describe('Calculator', () => {
                        it('adds two numbers', () => {
                            const calculator = new Calculator();
                            expect(calculator.add(2, 3)).toBe(5);
                        });
                    });
                    """,
                Framework = "Jest"
            }
        };

        // Make LLM fail so it falls back to heuristics
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.AssertionStyle.ShouldBe("expect");
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WithNoRecognizedAssertionStyle_ReturnsEmptyStyle() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "SomeTests.cs",
                Content = """
                    public class SomeTests {
                        public void SomeTest() {
                            // No recognizable assertion
                            var result = DoSomething();
                        }
                    }
                    """,
                Framework = "Unknown"
            }
        };

        // Make LLM fail so it falls back to heuristics
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.AssertionStyle.ShouldBe("");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithEmptyLlmResponse_ReturnsEmptyString() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        // Return empty response
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, "   ");

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: "public class Calculator { }",
            testFramework: "xUnit");

        // Assert
        result.ShouldBe("");
    }

    [Fact]
    public async Task GenerateTestsAsync_WithNullLlmResponse_ReturnsEmptyString() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        // Return null content - using string explicitly
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, (string?)null);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "Calculator.cs",
            codeContent: "public class Calculator { }",
            testFramework: "xUnit");

        // Assert
        result.ShouldBe("");
    }

    [Fact]
    public async Task AnalyzeTestPatternAsync_WithPythonArrangeComments_DetectsArrangeActAssert() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var existingTests = new List<TestFile> {
            new TestFile {
                Path = "test_calculator.py",
                Content = """
                    import pytest
                    
                    def test_add():
                        # Arrange
                        calculator = Calculator()
                        
                        # Act
                        result = calculator.add(2, 3)
                        
                        # Assert
                        assert result == 5
                    """,
                Framework = "pytest"
            }
        };

        // Make LLM fail so it falls back to heuristics
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.AnalyzeTestPatternAsync(existingTests);

        // Assert
        result.ShouldNotBeNull();
        result.UsesArrangeActAssert.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateTestsAsync_WithUnknownFramework_UsesDefaultPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<TestGenerator>>();
        var mockContainerManager = new Mock<IContainerManager>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockChatService = new Mock<IChatCompletionService>();

        var generatedTests = "public class Tests { }";
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, generatedTests);

        ChatHistory? capturedChatHistory = null;
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (ch, _, _, _) => capturedChatHistory = ch)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new TestGenerator(
            mockContainerManager.Object,
            mockFileAnalyzer.Object,
            kernel,
            mockLogger.Object);

        // Act
        var result = await generator.GenerateTestsAsync(
            containerId: "test-container",
            codeFilePath: "SomeCode.xyz",
            codeContent: "some code",
            testFramework: "UnknownFramework");

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var systemMessage = capturedChatHistory.FirstOrDefault(m => m.Role == AuthorRole.System);
        systemMessage.ShouldNotBeNull();
        // Should contain the framework name even if unknown
        systemMessage.Content.ShouldContain("UnknownFramework");
    }
}
