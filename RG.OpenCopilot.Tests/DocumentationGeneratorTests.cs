using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Shouldly;
using RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;

namespace RG.OpenCopilot.Tests;

public class DocumentationGeneratorTests {
    [Fact]
    public async Task GenerateInlineDocsAsync_WithCSharpCode_ReturnsDocumentedCode() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var code = """
            public class Calculator {
                public int Add(int a, int b) {
                    return a + b;
                }
            }
            """;

        var documentedCode = """
            /// <summary>
            /// Provides basic arithmetic operations.
            /// </summary>
            public class Calculator {
                /// <summary>
                /// Calculates the sum of two integers.
                /// </summary>
                /// <param name="a">The first number to add.</param>
                /// <param name="b">The second number to add.</param>
                /// <returns>The sum of the two numbers.</returns>
                public int Add(int a, int b) {
                    return a + b;
                }
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, documentedCode);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        var result = await generator.GenerateInlineDocsAsync(code, "C#");

        // Assert
        result.ShouldNotBeNull();
        result.Language.ShouldBe("C#");
        result.OriginalCode.ShouldBe(code);
        result.DocumentedCodeContent.ShouldContain("/// <summary>");
        result.DocumentedCodeContent.ShouldContain("/// <param name=\"a\">");
        result.DocumentedCodeContent.ShouldContain("/// <returns>");
        result.DocumentationCount.ShouldBeGreaterThan(0);
        mockChatService.Verify(s => s.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings>(),
            It.IsAny<Kernel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateInlineDocsAsync_WithJavaScriptCode_ReturnsDocumentedCode() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var code = """
            function greet(name) {
                return `Hello, ${name}!`;
            }
            """;

        var documentedCode = """
            /**
             * Generates a greeting message for the given name.
             * @param {string} name - The name to greet.
             * @returns {string} The greeting message.
             * @example
             * const message = greet('World'); // returns 'Hello, World!'
             */
            function greet(name) {
                return `Hello, ${name}!`;
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, documentedCode);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        var result = await generator.GenerateInlineDocsAsync(code, "JavaScript");

        // Assert
        result.ShouldNotBeNull();
        result.Language.ShouldBe("JavaScript");
        result.DocumentedCodeContent.ShouldContain("/**");
        result.DocumentedCodeContent.ShouldContain("@param");
        result.DocumentedCodeContent.ShouldContain("@returns");
        result.DocumentationCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateInlineDocsAsync_WithPythonCode_ReturnsDocumentedCode() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var code = """
            def multiply(x, y):
                return x * y
            """;

        var documentedCode = """
            def multiply(x, y):
                \"\"\"Calculates the product of two numbers.
                
                Args:
                    x (float): The first number to multiply.
                    y (float): The second number to multiply.
                
                Returns:
                    float: The product of x and y.
                
                Examples:
                    >>> multiply(3, 4)
                    12
                \"\"\"
                return x * y
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, documentedCode);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        var result = await generator.GenerateInlineDocsAsync(code, "Python");

        // Assert
        result.ShouldNotBeNull();
        result.Language.ShouldBe("Python");
        result.DocumentedCodeContent.ShouldContain("Calculates the product");
        result.DocumentedCodeContent.ShouldContain("Args:");
        result.DocumentedCodeContent.ShouldContain("Returns:");
    }

    [Fact]
    public async Task GenerateInlineDocsAsync_WithNullCode_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.GenerateInlineDocsAsync(null!, "C#"));

        exception.Message.ShouldContain("Code cannot be null or empty");
    }

    [Fact]
    public async Task GenerateInlineDocsAsync_WithEmptyLanguage_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.GenerateInlineDocsAsync("some code", ""));

        exception.Message.ShouldContain("Language cannot be null or empty");
    }

    [Fact]
    public async Task UpdateReadmeAsync_WithExistingReadme_UpdatesContent() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";
        var currentReadme = """
            # My Project
            
            A simple calculator library.
            """;

        var updatedReadme = """
            # My Project
            
            A simple calculator library with advanced features.
            
            ## Features
            - Basic arithmetic operations
            - Advanced mathematical functions
            
            ## Installation
            ```bash
            npm install my-project
            ```
            """;

        var changes = new List<FileChange> {
            new FileChange {
                Type = ChangeType.Created,
                Path = "/workspace/src/calculator.js",
                NewContent = "function add(a, b) { return a + b; }"
            }
        };

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "README.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "/workspace/README.md" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/README.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentReadme);

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, updatedReadme);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        await generator.UpdateReadmeAsync(containerId, changes);

        // Assert
        mockContainerManager.Verify(c => c.WriteFileInContainerAsync(
            containerId,
            "/workspace/README.md",
            It.Is<string>(s => s.Contains("Features") && s.Contains("Installation")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateReadmeAsync_WithoutExistingReadme_CreatesNewReadme() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";
        var newReadme = """
            # New Project
            
            ## Features
            - Feature 1
            - Feature 2
            """;

        var changes = new List<FileChange> {
            new FileChange {
                Type = ChangeType.Created,
                Path = "/workspace/src/index.js",
                NewContent = "console.log('Hello World');"
            }
        };

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "README.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, newReadme);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        await generator.UpdateReadmeAsync(containerId, changes);

        // Assert
        mockContainerManager.Verify(c => c.WriteFileInContainerAsync(
            containerId,
            "/workspace/README.md",
            It.Is<string>(s => s.Contains("Project")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateReadmeAsync_WithEmptyContainerId_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        var changes = new List<FileChange>();

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.UpdateReadmeAsync("", changes));

        exception.Message.ShouldContain("Container ID cannot be null or empty");
    }

    [Fact]
    public async Task UpdateReadmeAsync_WithNullChanges_ThrowsArgumentNullException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await generator.UpdateReadmeAsync("container", null!));
    }

    [Fact]
    public async Task GenerateApiDocsAsync_WithCodeFiles_ReturnsMarkdownDocumentation() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";
        var apiDoc = """
            # API Documentation
            
            ## Calculator Class
            
            ### Methods
            
            #### Add(int a, int b)
            Calculates the sum of two integers.
            
            **Parameters:**
            - `a` (int): The first number
            - `b` (int): The second number
            
            **Returns:** int - The sum
            
            **Example:**
            ```csharp
            var calc = new Calculator();
            var result = calc.Add(2, 3); // returns 5
            ```
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "*.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "/workspace/Calculator.cs" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "*.js", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "*.ts", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "*.py", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "*.java", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "*.go", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/Calculator.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Calculator { public int Add(int a, int b) { return a + b; } }");

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, apiDoc);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        var result = await generator.GenerateApiDocsAsync(containerId, ApiDocFormat.Markdown);

        // Assert
        result.ShouldNotBeNull();
        result.Format.ShouldBe(ApiDocFormat.Markdown);
        result.Content.ShouldContain("API Documentation");
        result.Content.ShouldContain("Calculator");
        result.FilePath.ShouldBe("/workspace/API.md");
    }

    [Fact]
    public async Task GenerateApiDocsAsync_WithNoCodeFiles_ReturnsEmptyDocumentation() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";

        // Setup all file patterns to return empty lists
        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        var result = await generator.GenerateApiDocsAsync(containerId);

        // Assert
        result.ShouldNotBeNull();
        result.Content.ShouldContain("No API documentation available");
    }

    [Fact]
    public async Task GenerateApiDocsAsync_WithHtmlFormat_ReturnsHtmlDocumentation() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";
        var apiDoc = """
            <!DOCTYPE html>
            <html>
            <head><title>API Documentation</title></head>
            <body>
            <h1>Calculator API</h1>
            <p>Documentation for Calculator class.</p>
            </body>
            </html>
            """;

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "*.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "/workspace/Calculator.cs" });

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, It.Is<string>(s => s != "*.cs"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/Calculator.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("public class Calculator { }");

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, apiDoc);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        var result = await generator.GenerateApiDocsAsync(containerId, ApiDocFormat.Html);

        // Assert
        result.ShouldNotBeNull();
        result.Format.ShouldBe(ApiDocFormat.Html);
        result.Content.ShouldContain("<!DOCTYPE html>");
        result.FilePath.ShouldBe("/workspace/API.html");
    }

    [Fact]
    public async Task GenerateApiDocsAsync_WithEmptyContainerId_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.GenerateApiDocsAsync(""));

        exception.Message.ShouldContain("Container ID cannot be null or empty");
    }

    [Fact]
    public async Task UpdateChangelogAsync_WithExistingChangelog_AddsNewVersion() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";
        var version = "1.1.0";
        var currentChangelog = """
            # Changelog
            
            ## [1.0.0] - 2024-01-01
            ### Added
            - Initial release
            """;

        var updatedChangelog = """
            # Changelog
            
            ## [1.1.0] - 2024-02-01
            ### Added
            - New calculator feature
            ### Fixed
            - Bug in addition method
            
            ## [1.0.0] - 2024-01-01
            ### Added
            - Initial release
            """;

        var changes = new List<ChangelogEntry> {
            new ChangelogEntry {
                Version = version,
                Type = "Added",
                Description = "New calculator feature",
                Date = DateTimeOffset.Parse("2024-02-01")
            },
            new ChangelogEntry {
                Version = version,
                Type = "Fixed",
                Description = "Bug in addition method",
                Date = DateTimeOffset.Parse("2024-02-01")
            }
        };

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "CHANGELOG.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "/workspace/CHANGELOG.md" });

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/CHANGELOG.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentChangelog);

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, updatedChangelog);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        await generator.UpdateChangelogAsync(containerId, version, changes);

        // Assert
        mockContainerManager.Verify(c => c.WriteFileInContainerAsync(
            containerId,
            "/workspace/CHANGELOG.md",
            It.Is<string>(s => s.Contains("1.1.0") && s.Contains("1.0.0")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateChangelogAsync_WithoutExistingChangelog_CreatesNewChangelog() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";
        var version = "1.0.0";
        var newChangelog = """
            # Changelog
            
            All notable changes to this project will be documented in this file.
            
            ## [1.0.0] - 2024-01-01
            ### Added
            - Initial release
            """;

        var changes = new List<ChangelogEntry> {
            new ChangelogEntry {
                Version = version,
                Type = "Added",
                Description = "Initial release",
                Date = DateTimeOffset.Parse("2024-01-01")
            }
        };

        mockFileAnalyzer
            .Setup(f => f.ListFilesAsync(containerId, "CHANGELOG.md", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, newChangelog);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        await generator.UpdateChangelogAsync(containerId, version, changes);

        // Assert
        mockContainerManager.Verify(c => c.WriteFileInContainerAsync(
            containerId,
            "/workspace/CHANGELOG.md",
            It.Is<string>(s => s.Contains("Changelog") && s.Contains("1.0.0")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateChangelogAsync_WithEmptyContainerId_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        var changes = new List<ChangelogEntry>();

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.UpdateChangelogAsync("", "1.0.0", changes));

        exception.Message.ShouldContain("Container ID cannot be null or empty");
    }

    [Fact]
    public async Task UpdateChangelogAsync_WithEmptyVersion_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        var changes = new List<ChangelogEntry>();

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.UpdateChangelogAsync("container", "", changes));

        exception.Message.ShouldContain("Version cannot be null or empty");
    }

    [Fact]
    public async Task UpdateChangelogAsync_WithNullChanges_ThrowsArgumentNullException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await generator.UpdateChangelogAsync("container", "1.0.0", null!));
    }

    [Fact]
    public async Task GenerateUsageExamplesAsync_WithValidApiFile_ReturnsExamples() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var containerId = "test-container";
        var apiFilePath = "/workspace/Calculator.cs";
        var apiCode = """
            public class Calculator {
                public int Add(int a, int b) {
                    return a + b;
                }
            }
            """;

        var examples = """
            # Usage Examples
            
            ## Basic Addition
            ```csharp
            var calculator = new Calculator();
            var result = calculator.Add(5, 3);
            Console.WriteLine($"5 + 3 = {result}"); // Output: 5 + 3 = 8
            ```
            
            ## Negative Numbers
            ```csharp
            var calculator = new Calculator();
            var result = calculator.Add(-5, 3);
            Console.WriteLine($"-5 + 3 = {result}"); // Output: -5 + 3 = -2
            ```
            """;

        mockContainerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, apiFilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiCode);

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, examples);

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

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act
        var result = await generator.GenerateUsageExamplesAsync(containerId, apiFilePath);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldContain("Usage Examples");
        result.ShouldContain("```csharp");
        result.ShouldContain("calculator.Add");
    }

    [Fact]
    public async Task GenerateUsageExamplesAsync_WithEmptyContainerId_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.GenerateUsageExamplesAsync("", "/path/to/file.cs"));

        exception.Message.ShouldContain("Container ID cannot be null or empty");
    }

    [Fact]
    public async Task GenerateUsageExamplesAsync_WithEmptyApiFilePath_ThrowsArgumentException() {
        // Arrange
        var mockLogger = new Mock<ILogger<DocumentationGenerator>>();
        var mockChatService = new Mock<IChatCompletionService>();
        var mockFileAnalyzer = new Mock<IFileAnalyzer>();
        var mockFileEditor = new Mock<IFileEditor>();
        var mockContainerManager = new Mock<IContainerManager>();

        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(mockChatService.Object);
        var kernel = kernelBuilder.Build();

        var generator = new DocumentationGenerator(
            kernel,
            mockLogger.Object,
            mockFileAnalyzer.Object,
            mockFileEditor.Object,
            mockContainerManager.Object);

        // Act & Assert
        var exception = await Should.ThrowAsync<ArgumentException>(
            async () => await generator.GenerateUsageExamplesAsync("container", ""));

        exception.Message.ShouldContain("API file path cannot be null or empty");
    }
}
