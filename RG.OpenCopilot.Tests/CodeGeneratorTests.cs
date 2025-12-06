using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Moq;
using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App.CodeGeneration;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class CodeGeneratorTests {
    [Fact]
    public async Task GenerateCodeAsync_WithValidRequest_ReturnsGeneratedCode() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Create a simple calculator class with add and subtract methods",
            Language = "C#",
            FilePath = "Calculator.cs"
        };

        var llmResponse = """
            public class Calculator {
                public int Add(int a, int b) {
                    return a + b;
                }
                
                public int Subtract(int a, int b) {
                    return a - b;
                }
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("class Calculator");
        result.ShouldContain("Add");
        result.ShouldContain("Subtract");
        mockChatService.Verify(s => s.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings>(),
            It.IsAny<Kernel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateCodeAsync_WithMarkdownCodeBlock_ExtractsCleanCode() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Create a greeting function",
            Language = "JavaScript",
            FilePath = "greet.js"
        };

        var llmResponseWithMarkdown = """
            ```javascript
            function greet(name) {
                return `Hello, ${name}!`;
            }
            ```
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponseWithMarkdown);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotContain("```");
        result.ShouldContain("function greet");
        result.Trim().ShouldStartWith("function");
    }

    [Fact]
    public async Task GenerateCodeAsync_WithExistingCode_IncludesExistingCodeInPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Add a multiply method",
            Language = "C#",
            FilePath = "Calculator.cs"
        };

        var existingCode = """
            public class Calculator {
                public int Add(int a, int b) {
                    return a + b;
                }
            }
            """;

        var llmResponse = """
            public class Calculator {
                public int Add(int a, int b) {
                    return a + b;
                }
                
                public int Multiply(int a, int b) {
                    return a * b;
                }
            }
            """;

        ChatHistory? capturedChatHistory = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.GenerateCodeAsync(request, existingCode);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("Multiply");
        capturedChatHistory.ShouldNotBeNull();
        var userMessage = capturedChatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("Existing Code to Modify");
        userMessage.Content.ShouldContain(existingCode);
    }

    [Fact]
    public async Task GenerateClassAsync_CreatesClassGenerationRequest() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var llmResponse = """
            public class User {
                public string Name { get; set; }
                public string Email { get; set; }
            }
            """;

        ChatHistory? capturedChatHistory = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.GenerateClassAsync(
            className: "User",
            description: "A user entity with name and email properties",
            language: "C#");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("class User");
        capturedChatHistory.ShouldNotBeNull();
        var userMessage = capturedChatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("Create a class named 'User'");
    }

    [Fact]
    public async Task GenerateFunctionAsync_CreatesFunctionGenerationRequest() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var llmResponse = """
            def calculate_area(radius):
                import math
                return math.pi * radius ** 2
            """;

        ChatHistory? capturedChatHistory = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.GenerateFunctionAsync(
            functionName: "calculate_area",
            description: "Calculate the area of a circle given its radius",
            language: "Python");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("calculate_area");
        capturedChatHistory.ShouldNotBeNull();
        var userMessage = capturedChatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("Create a function named 'calculate_area'");
    }

    [Fact]
    public async Task ValidateSyntaxAsync_CSharpCodeWithBalancedBraces_ReturnsTrue() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        var validCode = """
            public class Test {
                public void Method() {
                    if (true) {
                        Console.WriteLine("Hello");
                    }
                }
            }
            """;

        // Act
        var result = await generator.ValidateSyntaxAsync(validCode, "C#");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateSyntaxAsync_CSharpCodeWithUnbalancedBraces_ReturnsFalse() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        var invalidCode = """
            public class Test {
                public void Method() {
                    if (true) {
                        Console.WriteLine("Hello");
                    
                }
            """;

        // Act
        var result = await generator.ValidateSyntaxAsync(invalidCode, "C#");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateSyntaxAsync_JavaScriptCodeWithBalancedBraces_ReturnsTrue() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        var validCode = """
            function test() {
                if (true) {
                    console.log("Hello");
                }
            }
            """;

        // Act
        var result = await generator.ValidateSyntaxAsync(validCode, "JavaScript");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateSyntaxAsync_PythonCodeWithBalancedBrackets_ReturnsTrue() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        var validCode = """
            def test():
                items = [1, 2, 3]
                result = items[0]
                return result
            """;

        // Act
        var result = await generator.ValidateSyntaxAsync(validCode, "Python");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateSyntaxAsync_EmptyCode_ReturnsFalse() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.ValidateSyntaxAsync("", "C#");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GenerateCodeAsync_WithDependencies_IncludesDependenciesInPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Create a service class",
            Language = "C#",
            FilePath = "UserService.cs",
            Dependencies = ["Microsoft.EntityFrameworkCore", "System.Linq"]
        };

        var llmResponse = "public class UserService { }";

        ChatHistory? capturedChatHistory = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var userMessage = capturedChatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("Dependencies");
        userMessage.Content.ShouldContain("Microsoft.EntityFrameworkCore");
        userMessage.Content.ShouldContain("System.Linq");
    }

    [Fact]
    public async Task GenerateCodeAsync_WithContext_IncludesContextInPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Create a repository class",
            Language = "C#",
            FilePath = "UserRepository.cs",
            Context = new Dictionary<string, string> {
                { "Database", "PostgreSQL" },
                { "ORM", "Entity Framework Core" }
            }
        };

        var llmResponse = "public class UserRepository { }";

        ChatHistory? capturedChatHistory = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var userMessage = capturedChatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("Additional Context");
        userMessage.Content.ShouldContain("Database");
        userMessage.Content.ShouldContain("PostgreSQL");
        userMessage.Content.ShouldContain("ORM");
        userMessage.Content.ShouldContain("Entity Framework Core");
    }

    [Fact]
    public async Task GenerateCodeAsync_ConfiguresOpenAISettingsCorrectly() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Test",
            Language = "C#"
        };

        var llmResponse = "public class Test { }";

        PromptExecutionSettings? capturedSettings = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (_, settings, _, _) => capturedSettings = settings)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        await generator.GenerateCodeAsync(request);

        // Assert
        capturedSettings.ShouldNotBeNull();
        capturedSettings.ShouldBeOfType<OpenAIPromptExecutionSettings>();
        var openAISettings = (OpenAIPromptExecutionSettings)capturedSettings;
        openAISettings.Temperature.ShouldBe(0.2);
        openAISettings.MaxTokens.ShouldBe(4000);
    }

    [Fact]
    public async Task GenerateCodeAsync_LogsInformationMessages() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Test logging",
            Language = "C#"
        };

        var llmResponse = "public class Test { }";
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        await generator.GenerateCodeAsync(request);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generating code")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Generated")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateCodeAsync_WhenLlmFails_ThrowsException() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Test error handling",
            Language = "C#"
        };

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service error"));

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await generator.GenerateCodeAsync(request));

        exception.Message.ShouldBe("LLM service error");
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error generating code")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateCodeAsync_WithCancellationToken_PropagatesToken() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Test cancellation",
            Language = "C#"
        };

        var cts = new CancellationTokenSource();
        CancellationToken capturedToken = default;

        var llmResponse = "public class Test { }";
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);

        mockChatService
            .Setup(s => s.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .Callback<ChatHistory, PromptExecutionSettings, Kernel, CancellationToken>(
                (_, _, _, token) => capturedToken = token)
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        await generator.GenerateCodeAsync(request, cancellationToken: cts.Token);

        // Assert
        capturedToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task ValidateSyntaxAsync_TypeScriptCode_UsesJavaScriptValidation() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        var validCode = """
            interface User {
                name: string;
                email: string;
            }
            
            function greet(user: User): string {
                return `Hello, ${user.name}!`;
            }
            """;

        // Act
        var result = await generator.ValidateSyntaxAsync(validCode, "TypeScript");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateSyntaxAsync_UnknownLanguage_ReturnsGenericValidation() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        var code = "some code in an unknown language";

        // Act
        var result = await generator.ValidateSyntaxAsync(code, "UnknownLang");

        // Assert
        result.ShouldBeTrue(); // Generic validation just checks if code is not empty
    }

    [Fact]
    public async Task GenerateCodeAsync_BuildsChatHistoryCorrectly() {
        // Arrange
        var mockLogger = new Mock<ILogger<CodeGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var request = new LlmCodeGenerationRequest {
            Description = "Test chat history",
            Language = "C#"
        };

        var llmResponse = "public class Test { }";
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponse);
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

        var generator = new CodeGenerator(kernel, mockLogger.Object);

        // Act
        await generator.GenerateCodeAsync(request);

        // Assert
        capturedChatHistory.ShouldNotBeNull();
        capturedChatHistory.Count.ShouldBe(2); // System message + User message
        capturedChatHistory[0].Role.ShouldBe(AuthorRole.System);
        capturedChatHistory[0].Content.ShouldContain("expert software developer");
        capturedChatHistory[1].Role.ShouldBe(AuthorRole.User);
        capturedChatHistory[1].Content.ShouldContain("Test chat history");
    }
}
