using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for CodeGenerator that use real LLM models
/// These tests require LLM:ApiKey to be configured in appsettings.json or environment variables
/// </summary>
public class CodeGeneratorIntegrationTests {
    private readonly IConfiguration _configuration;
    private readonly bool _hasApiKey;

    public CodeGeneratorIntegrationTests() {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        _hasApiKey = !string.IsNullOrEmpty(_configuration["LLM:ApiKey"]);
    }

    [Fact]
    public async Task GenerateCodeAsync_CSharpClass_GeneratesValidCSharpCode() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var request = new LlmCodeGenerationRequest {
            Description = """
                Create a simple User class with the following properties:
                - Id (int)
                - Name (string)
                - Email (string)
                - CreatedAt (DateTime)
                
                Include a constructor that initializes all properties.
                """,
            Language = "C#",
            FilePath = "Models/User.cs"
        };

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("class User");
        result.ShouldContain("Id");
        result.ShouldContain("Name");
        result.ShouldContain("Email");
        result.ShouldContain("CreatedAt");

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "C#");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateCodeAsync_JavaScriptFunction_GeneratesValidJavaScript() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var request = new LlmCodeGenerationRequest {
            Description = """
                Create a function that validates an email address.
                The function should:
                - Accept an email string as parameter
                - Return true if valid, false otherwise
                - Use a regex pattern for validation
                """,
            Language = "JavaScript",
            FilePath = "validators/emailValidator.js"
        };

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("function");
        result.ShouldContain("email");

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "JavaScript");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateCodeAsync_PythonClass_GeneratesValidPythonCode() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var request = new LlmCodeGenerationRequest {
            Description = """
                Create a Calculator class with methods for:
                - add(a, b)
                - subtract(a, b)
                - multiply(a, b)
                - divide(a, b)
                
                Include error handling for division by zero.
                """,
            Language = "Python",
            FilePath = "calculator.py"
        };

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("class");
        result.ShouldContain("def add");
        result.ShouldContain("def subtract");
        result.ShouldContain("def multiply");
        result.ShouldContain("def divide");

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "Python");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateCodeAsync_ModifyExistingCode_PreservesStyle() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var existingCode = """
            public class Calculator {
                public int Add(int a, int b) {
                    return a + b;
                }
                
                public int Subtract(int a, int b) {
                    return a - b;
                }
            }
            """;

        var request = new LlmCodeGenerationRequest {
            Description = "Add Multiply and Divide methods to the Calculator class",
            Language = "C#",
            FilePath = "Calculator.cs"
        };

        // Act
        var result = await generator.GenerateCodeAsync(request, existingCode);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("class Calculator");
        result.ShouldContain("Add"); // Existing method should be preserved
        result.ShouldContain("Subtract"); // Existing method should be preserved
        result.ShouldContain("Multiply"); // New method
        result.ShouldContain("Divide"); // New method

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "C#");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateClassAsync_CreatesCompleteClass() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        // Act
        var result = await generator.GenerateClassAsync(
            className: "Product",
            description: "A product entity with Id, Name, Price, and Description properties. Include data validation.",
            language: "C#");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("class Product");
        result.ShouldContain("Id");
        result.ShouldContain("Name");
        result.ShouldContain("Price");

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "C#");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateFunctionAsync_CreatesCompleteFunction() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        // Act
        var result = await generator.GenerateFunctionAsync(
            functionName: "fibonacci",
            description: "Calculate the nth Fibonacci number using recursion with memoization",
            language: "JavaScript");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("fibonacci");
        result.ShouldContain("function");

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "JavaScript");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateCodeAsync_WithDependencies_UsesSpecifiedLibraries() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var request = new LlmCodeGenerationRequest {
            Description = "Create a service class that uses Entity Framework Core to query users from database",
            Language = "C#",
            FilePath = "Services/UserService.cs",
            Dependencies = [
                "Microsoft.EntityFrameworkCore",
                "System.Linq"
            ]
        };

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("class");
        // The code should reference the dependencies in some way
        // (either through usings or in the implementation)

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "C#");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateCodeAsync_WithContext_IncorporatesContextInfo() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var request = new LlmCodeGenerationRequest {
            Description = "Create a database context class",
            Language = "C#",
            FilePath = "Data/AppDbContext.cs",
            Context = new Dictionary<string, string> {
                { "Database Provider", "PostgreSQL" },
                { "Entity Framework Version", "8.0" },
                { "Entities", "User, Product, Order" }
            }
        };

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("DbContext");

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "C#");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateCodeAsync_TypeScript_GeneratesValidTypeScriptCode() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var request = new LlmCodeGenerationRequest {
            Description = """
                Create a TypeScript interface for a User with:
                - id: number
                - name: string
                - email: string
                - role: 'admin' | 'user' | 'guest'
                
                Also create a function that validates a user object against this interface.
                """,
            Language = "TypeScript",
            FilePath = "types/user.ts"
        };

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("interface");
        result.ShouldContain("User");

        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "TypeScript");
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task GenerateCodeAsync_ComplexScenario_GeneratesHighQualityCode() {
        // Skip if no API key configured
        if (!_hasApiKey) {
            return; // Skip test when no LLM configured
        }

        // Arrange
        var kernel = CreateKernel();
        var logger = new TestLogger<CodeGenerator>();
        var gitHubClient = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("RG-OpenCopilot-Test"));
        var generator = new CodeGenerator(new ExecutorKernel(kernel), logger, gitHubClient);

        var request = new LlmCodeGenerationRequest {
            Description = """
                Create a service class for managing todo items with the following:
                
                1. A TodoItem class with properties: Id, Title, Description, IsCompleted, DueDate
                2. A TodoService class with methods:
                   - GetAllAsync(): returns all todos
                   - GetByIdAsync(id): returns a specific todo
                   - CreateAsync(todo): creates a new todo
                   - UpdateAsync(todo): updates an existing todo
                   - DeleteAsync(id): deletes a todo
                   - MarkAsCompleteAsync(id): marks a todo as complete
                
                Include:
                - Proper error handling
                - XML documentation comments
                - Async/await patterns
                - Validation
                """,
            Language = "C#",
            FilePath = "Services/TodoService.cs",
            Dependencies = [
                "System.Collections.Generic",
                "System.Threading.Tasks"
            ]
        };

        // Act
        var result = await generator.GenerateCodeAsync(request);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        
        // Check for class definitions
        result.ShouldContain("class TodoItem");
        result.ShouldContain("class TodoService");
        
        // Check for required properties
        result.ShouldContain("Title");
        result.ShouldContain("Description");
        result.ShouldContain("IsCompleted");
        
        // Check for async methods
        result.ShouldContain("async");
        result.ShouldContain("Task");
        
        // Validate syntax
        var isValid = await generator.ValidateSyntaxAsync(result, "C#");
        isValid.ShouldBeTrue();
    }

    private Kernel CreateKernel() {
        var kernelBuilder = Kernel.CreateBuilder();

        var llmProvider = _configuration["LLM:Provider"] ?? "OpenAI";
        var apiKey = _configuration["LLM:ApiKey"] ?? "";
        var modelId = _configuration["LLM:ModelId"] ?? "gpt-4o";

        switch (llmProvider.ToLowerInvariant()) {
            case "openai":
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: modelId,
                    apiKey: apiKey);
                break;

            case "azureopenai":
                var azureEndpoint = _configuration["LLM:AzureEndpoint"] ?? "";
                var azureDeployment = _configuration["LLM:AzureDeployment"] ?? "";
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: azureDeployment,
                    endpoint: azureEndpoint,
                    apiKey: apiKey);
                break;

            default:
                throw new InvalidOperationException($"Unsupported LLM provider: {llmProvider}");
        }

        return kernelBuilder.Build();
    }

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
