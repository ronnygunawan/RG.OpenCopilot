using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class StepAnalyzerTests {
    [Fact]
    public async Task AnalyzeStepAsync_ValidStep_ReturnsActionPlan() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var step = new PlanStep {
            Id = "step-1",
            Title = "Add user authentication",
            Details = "Implement JWT-based authentication for the API"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            Files = ["Program.cs", "Controllers/UserController.cs"],
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var llmResponse = """
            {
              "actions": [
                {
                  "type": "CreateFile",
                  "filePath": "Services/AuthService.cs",
                  "description": "Create authentication service",
                  "request": {
                    "content": "public class AuthService { }",
                    "parameters": {}
                  }
                }
              ],
              "prerequisites": ["Install JWT package"],
              "requiresTests": true,
              "testFile": "Tests/AuthServiceTests.cs",
              "mainFile": "Services/AuthService.cs"
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);
        SetupMockLlmResponse(mockChatService, chatContent);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        var result = await service.AnalyzeStepAsync(step: step, context: context);

        // Assert
        result.ShouldNotBeNull();
        result.Actions.Count.ShouldBe(1);
        result.Actions[0].Type.ShouldBe(ActionType.CreateFile);
        result.Actions[0].FilePath.ShouldBe("Services/AuthService.cs");
        result.Actions[0].Description.ShouldBe("Create authentication service");
        result.Prerequisites.Count.ShouldBe(1);
        result.Prerequisites[0].ShouldBe("Install JWT package");
        result.RequiresTests.ShouldBeTrue();
        result.TestFile.ShouldBe("Tests/AuthServiceTests.cs");
        result.MainFile.ShouldBe("Services/AuthService.cs");
    }

    [Fact]
    public async Task AnalyzeStepAsync_MultipleActions_ParsesAllActions() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var step = new PlanStep {
            Id = "step-2",
            Title = "Refactor database layer",
            Details = "Move to Entity Framework Core with repository pattern"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            Files = ["Data/DbContext.cs"],
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var llmResponse = """
            {
              "actions": [
                {
                  "type": "CreateFile",
                  "filePath": "Repositories/IUserRepository.cs",
                  "description": "Create user repository interface",
                  "request": {
                    "content": "public interface IUserRepository { }",
                    "parameters": {}
                  }
                },
                {
                  "type": "CreateFile",
                  "filePath": "Repositories/UserRepository.cs",
                  "description": "Implement user repository",
                  "request": {
                    "content": "public class UserRepository : IUserRepository { }",
                    "parameters": {}
                  }
                },
                {
                  "type": "ModifyFile",
                  "filePath": "Data/DbContext.cs",
                  "description": "Update DbContext configuration",
                  "request": {
                    "content": "DbSet<User> Users { get; set; }",
                    "beforeMarker": "public class AppDbContext",
                    "afterMarker": "}",
                    "parameters": {}
                  }
                }
              ],
              "prerequisites": ["Install EF Core packages", "Create migrations"],
              "requiresTests": true,
              "testFile": "Tests/UserRepositoryTests.cs",
              "mainFile": "Repositories/UserRepository.cs"
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);
        SetupMockLlmResponse(mockChatService, chatContent);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        var result = await service.AnalyzeStepAsync(step: step, context: context);

        // Assert
        result.Actions.Count.ShouldBe(3);
        result.Actions[0].Type.ShouldBe(ActionType.CreateFile);
        result.Actions[1].Type.ShouldBe(ActionType.CreateFile);
        result.Actions[2].Type.ShouldBe(ActionType.ModifyFile);
        result.Actions[2].Request.BeforeMarker.ShouldBe("public class AppDbContext");
        result.Actions[2].Request.AfterMarker.ShouldBe("}");
        result.Prerequisites.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnalyzeStepAsync_WithParameters_ParsesParameters() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var step = new PlanStep {
            Id = "step-3",
            Title = "Add caching layer",
            Details = "Implement Redis caching for API responses"
        };
        var context = new RepositoryContext {
            Language = "csharp",
            Files = ["appsettings.json"],
            TestFramework = "xUnit",
            BuildTool = "dotnet"
        };

        var llmResponse = """
            {
              "actions": [
                {
                  "type": "CreateFile",
                  "filePath": "Services/CacheService.cs",
                  "description": "Create cache service",
                  "request": {
                    "content": "public class CacheService { }",
                    "parameters": {
                      "connectionString": "localhost:6379",
                      "defaultTtl": "3600"
                    }
                  }
                }
              ],
              "prerequisites": [],
              "requiresTests": true,
              "testFile": "Tests/CacheServiceTests.cs",
              "mainFile": "Services/CacheService.cs"
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);
        SetupMockLlmResponse(mockChatService, chatContent);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        var result = await service.AnalyzeStepAsync(step: step, context: context);

        // Assert
        result.Actions[0].Request.Parameters.Count.ShouldBe(2);
        result.Actions[0].Request.Parameters["connectionString"].ShouldBe("localhost:6379");
        result.Actions[0].Request.Parameters["defaultTtl"].ShouldBe("3600");
    }

    [Fact]
    public async Task AnalyzeStepAsync_NoTestsRequired_ReturnsFalse() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var step = new PlanStep {
            Id = "step-4",
            Title = "Update README",
            Details = "Add API documentation to README file"
        };
        var context = new RepositoryContext {
            Language = "markdown",
            Files = ["README.md"]
        };

        var llmResponse = """
            {
              "actions": [
                {
                  "type": "ModifyFile",
                  "filePath": "README.md",
                  "description": "Add API documentation section",
                  "request": {
                    "content": "## API Documentation",
                    "parameters": {}
                  }
                }
              ],
              "prerequisites": [],
              "requiresTests": false,
              "testFile": null,
              "mainFile": "README.md"
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);
        SetupMockLlmResponse(mockChatService, chatContent);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        var result = await service.AnalyzeStepAsync(step: step, context: context);

        // Assert
        result.RequiresTests.ShouldBeFalse();
        result.TestFile.ShouldBeNull();
    }

    [Fact]
    public async Task AnalyzeStepAsync_InvalidJson_ThrowsException() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var step = new PlanStep {
            Id = "step-5",
            Title = "Test step",
            Details = "Test details"
        };
        var context = new RepositoryContext {
            Language = "csharp"
        };

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, "invalid json");
        SetupMockLlmResponse(mockChatService, chatContent);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.AnalyzeStepAsync(step: step, context: context));

        exception.Message.ShouldContain("Invalid JSON response from LLM");
    }

    [Fact]
    public async Task AnalyzeStepAsync_LlmThrowsException_ThrowsInvalidOperationException() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var step = new PlanStep {
            Id = "step-6",
            Title = "Test step",
            Details = "Test details"
        };
        var context = new RepositoryContext {
            Language = "csharp"
        };

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM service error"));

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.AnalyzeStepAsync(step: step, context: context));

        exception.Message.ShouldContain("Failed to analyze step");
    }

    [Fact]
    public async Task BuildContextAsync_CSharpProject_DetectsLanguageAndTools() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var containerId = "test-container";

        var fileTree = new FileTree {
            Root = new FileTreeNode { Name = ".", Path = ".", IsDirectory = true },
            AllFiles = [
                "Program.cs",
                "Services/UserService.cs",
                "Tests/UserServiceTests.cs",
                "RG.OpenCopilot.csproj"
            ]
        };

        mockFileAnalyzer
            .Setup(f => f.BuildFileTreeAsync(
                containerId: containerId,
                rootPath: ".",
                cancellationToken: It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTree);

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(
                containerId: containerId,
                filePath: "RG.OpenCopilot.csproj",
                cancellationToken: It.IsAny<CancellationToken>()))
            .ReturnsAsync("<Project><ItemGroup><PackageReference Include=\"xunit\" /></ItemGroup></Project>");

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        var result = await service.BuildContextAsync(containerId: containerId);

        // Assert
        result.Language.ShouldBe("csharp");
        result.Files.Count.ShouldBe(4);
        result.BuildTool.ShouldBe("dotnet");
        result.TestFramework.ShouldBe("xUnit");
    }

    [Fact]
    public async Task BuildContextAsync_JavaScriptProject_DetectsNpmAndJest() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var containerId = "test-container-js";

        var fileTree = new FileTree {
            Root = new FileTreeNode { Name = ".", Path = ".", IsDirectory = true },
            AllFiles = [
                "index.js",
                "src/app.js",
                "tests/app.test.js",
                "package.json"
            ]
        };

        mockFileAnalyzer
            .Setup(f => f.BuildFileTreeAsync(
                containerId: containerId,
                rootPath: ".",
                cancellationToken: It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTree);

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(
                containerId: containerId,
                filePath: "package.json",
                cancellationToken: It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"devDependencies": {"jest": "^29.0.0"}}""");

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        var result = await service.BuildContextAsync(containerId: containerId);

        // Assert
        result.Language.ShouldBe("javascript");
        result.Files.Count.ShouldBe(4);
        result.BuildTool.ShouldBe("npm");
        result.TestFramework.ShouldBe("Jest");
    }

    [Fact]
    public async Task BuildContextAsync_PythonProject_DetectsPipAndPytest() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var containerId = "test-container-py";

        var fileTree = new FileTree {
            Root = new FileTreeNode { Name = ".", Path = ".", IsDirectory = true },
            AllFiles = [
                "main.py",
                "src/app.py",
                "tests/test_app.py",
                "requirements.txt"
            ]
        };

        mockFileAnalyzer
            .Setup(f => f.BuildFileTreeAsync(
                containerId: containerId,
                rootPath: ".",
                cancellationToken: It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTree);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        var result = await service.BuildContextAsync(containerId: containerId);

        // Assert
        result.Language.ShouldBe("python");
        result.Files.Count.ShouldBe(4);
        result.BuildTool.ShouldBe("pip");
        result.TestFramework.ShouldBe("pytest");
    }

    [Fact]
    public async Task BuildContextAsync_FileAnalyzerFails_ThrowsException() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var containerId = "test-container-fail";

        mockFileAnalyzer
            .Setup(f => f.BuildFileTreeAsync(
                containerId: containerId,
                rootPath: ".",
                cancellationToken: It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Container not found"));

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.BuildContextAsync(containerId: containerId));

        exception.Message.ShouldContain("Failed to build repository context");
    }

    [Fact]
    public async Task AnalyzeStepAsync_LogsAnalysisStart() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var step = new PlanStep {
            Id = "step-1",
            Title = "Test step",
            Details = "Test details"
        };
        var context = new RepositoryContext { Language = "csharp" };

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, """
            {
              "actions": [],
              "prerequisites": [],
              "requiresTests": false,
              "testFile": null,
              "mainFile": null
            }
            """);
        SetupMockLlmResponse(mockChatService, chatContent);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        await service.AnalyzeStepAsync(step: step, context: context);

        // Assert
        logger.LoggedMessages.ShouldContain(msg => msg.Contains("Analyzing step: Test step"));
    }

    [Fact]
    public async Task BuildContextAsync_LogsContextBuilt() {
        // Arrange
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockContainerManager = new Mock<IContainerManager>();
        var logger = new TestLogger<StepAnalyzer>();

        var containerId = "test-container";

        var fileTree = new FileTree {
            Root = new FileTreeNode { Name = ".", Path = ".", IsDirectory = true },
            AllFiles = ["Program.cs"]
        };

        mockFileAnalyzer
            .Setup(f => f.BuildFileTreeAsync(
                containerId: containerId,
                rootPath: ".",
                cancellationToken: It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileTree);

        var service = CreateService(
            mockChatService: mockChatService.Object,
            mockFileAnalyzer: mockFileAnalyzer.Object,
            mockContainerManager: mockContainerManager.Object,
            logger: logger);

        // Act
        await service.BuildContextAsync(containerId: containerId);

        // Assert
        logger.LoggedMessages.ShouldContain(msg => 
            msg.Contains("Repository context built") && msg.Contains("Language=csharp"));
    }

    private static StepAnalyzer CreateService(
        IChatCompletionService mockChatService,
        IFileAnalyzer mockFileAnalyzer,
        IContainerManager mockContainerManager,
        ILogger<StepAnalyzer> logger) {
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService);
        var kernel = kernelBuilder.Build();

        return new StepAnalyzer(
            kernel: kernel,
            fileAnalyzer: mockFileAnalyzer,
            containerManager: mockContainerManager,
            logger: logger);
    }

    private static void SetupMockLlmResponse(Mock<IChatCompletionService> mockChatService, ChatMessageContent response) {
        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { response });
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
