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
}
