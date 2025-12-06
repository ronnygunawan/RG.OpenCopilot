using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class LlmPlannerServiceTests {
    [Fact]
    public async Task CreatePlanAsync_WithValidLlmResponse_ReturnsAgentPlan() {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlannerService>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var context = new AgentTaskContext {
            IssueTitle = "Add user authentication",
            IssueBody = "Implement user login and registration"
        };

        var llmResponseJson = """
            {
                "problemSummary": "Implement a comprehensive user authentication system with secure password handling, user registration, and login functionality following industry best practices.",
                "constraints": [
                    "Use secure password hashing with bcrypt or similar algorithm",
                    "Follow OWASP authentication guidelines and security best practices",
                    "Ensure passwords are never stored in plain text",
                    "Implement proper input validation and sanitization",
                    "Add rate limiting to prevent brute force attacks",
                    "Use HTTPS for all authentication endpoints"
                ],
                "steps": [
                    {
                        "id": "step-1",
                        "title": "Create user model and database schema",
                        "details": "Define the User entity with properties for username, hashed password, email, creation date, and last login. Create the corresponding database table with proper indexing on username and email fields for performance. Include fields for password reset tokens and account status.",
                        "done": false
                    },
                    {
                        "id": "step-2",
                        "title": "Implement password hashing service",
                        "details": "Create a secure password hashing service using bcrypt or Argon2. Implement methods for hashing passwords during registration and verifying passwords during login. Use appropriate work factors (cost parameter) to balance security and performance.",
                        "done": false
                    },
                    {
                        "id": "step-3",
                        "title": "Build user registration endpoint",
                        "details": "Create a registration API endpoint that validates input (username format, password strength, email validity), checks for existing users, hashes the password, and creates the new user record. Return appropriate success or error responses with clear messages.",
                        "done": false
                    },
                    {
                        "id": "step-4",
                        "title": "Implement login authentication endpoint",
                        "details": "Create a login API endpoint that accepts username/email and password, retrieves the user from the database, verifies the password hash, and generates a JWT token or session upon successful authentication. Include proper error handling for invalid credentials.",
                        "done": false
                    },
                    {
                        "id": "step-5",
                        "title": "Add authentication middleware",
                        "details": "Implement middleware to protect authenticated routes, validate JWT tokens or sessions, and attach user information to requests. Ensure unauthorized requests receive appropriate 401 responses.",
                        "done": false
                    },
                    {
                        "id": "step-6",
                        "title": "Write comprehensive tests",
                        "details": "Create unit tests for password hashing, registration validation, login authentication, and middleware. Add integration tests for the complete authentication flow including edge cases like duplicate usernames, weak passwords, and invalid tokens.",
                        "done": false
                    }
                ],
                "checklist": [
                    "All tests pass successfully",
                    "Passwords are securely hashed using bcrypt or Argon2",
                    "Input validation prevents SQL injection and XSS attacks",
                    "Rate limiting is implemented on login endpoint",
                    "Authentication endpoints use HTTPS only",
                    "Error messages don't leak sensitive information",
                    "Documentation is updated with authentication flow",
                    "Security review completed by team"
                ],
                "fileTargets": [
                    "Models/User.cs",
                    "Services/PasswordHashingService.cs", 
                    "Services/AuthenticationService.cs",
                    "Controllers/AuthController.cs",
                    "Middleware/AuthenticationMiddleware.cs",
                    "Tests/AuthenticationTests.cs",
                    "Database/Migrations/AddUsersTable.cs"
                ]
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponseJson);

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

        var service = new LlmPlannerService(kernel, mockLogger.Object);

        // Act
        var plan = await service.CreatePlanAsync(context);

        // Assert
        plan.ShouldNotBeNull();
        plan.ProblemSummary.ShouldContain("comprehensive user authentication system");
        plan.Constraints.Count.ShouldBe(6);
        plan.Constraints[0].ShouldBe("Use secure password hashing with bcrypt or similar algorithm");
        plan.Steps.Count.ShouldBe(6);
        plan.Steps[0].Id.ShouldBe("step-1");
        plan.Steps[0].Title.ShouldBe("Create user model and database schema");
        plan.Steps[0].Done.ShouldBeFalse();
        plan.Checklist.Count.ShouldBe(8);
        plan.FileTargets.Count.ShouldBe(7);

        // Verify LLM was called
        mockChatService.Verify(s => s.GetChatMessageContentsAsync(
            It.IsAny<ChatHistory>(),
            It.IsAny<PromptExecutionSettings>(),
            It.IsAny<Kernel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenLlmFails_ReturnsFallbackPlan() {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlannerService>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var context = new AgentTaskContext {
            IssueTitle = "Fix bug in payment processing",
            IssueBody = "Payment fails when amount is zero"
        };

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

        var service = new LlmPlannerService(kernel, mockLogger.Object);

        // Act
        var plan = await service.CreatePlanAsync(context);

        // Assert
        plan.ShouldNotBeNull();
        plan.ProblemSummary.ShouldContain("Fix bug in payment processing");
        plan.Steps.Count.ShouldBeGreaterThan(0);
        plan.Steps[0].Id.ShouldBe("step-1");
        plan.Checklist.Count.ShouldBeGreaterThan(0);
        plan.Constraints.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreatePlanAsync_WhenLlmReturnsInvalidJson_ReturnsFallbackPlan() {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlannerService>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var context = new AgentTaskContext {
            IssueTitle = "Update documentation",
            IssueBody = "Add API documentation"
        };

        var invalidJson = "This is not valid JSON";
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, invalidJson);

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

        var service = new LlmPlannerService(kernel, mockLogger.Object);

        // Act
        var plan = await service.CreatePlanAsync(context);

        // Assert
        plan.ShouldNotBeNull();
        plan.ProblemSummary.ShouldContain("Update documentation");
        plan.Steps.Count.ShouldBeGreaterThan(0);
        plan.Checklist.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CreatePlanAsync_IncludesRepositorySummaryInPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlannerService>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var context = new AgentTaskContext {
            IssueTitle = "Refactor code",
            IssueBody = "Improve code structure",
            RepositorySummary = "C# project using .NET 10"
        };

        var llmResponseJson = """
            {
                "problemSummary": "Refactor codebase",
                "constraints": [],
                "steps": [
                    {
                        "id": "step-1",
                        "title": "Analyze code",
                        "details": "Review current structure",
                        "done": false
                    }
                ],
                "checklist": ["Code compiles"],
                "fileTargets": ["Program.cs"]
            }
            """;

        ChatHistory? capturedChatHistory = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponseJson);

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

        var service = new LlmPlannerService(kernel, mockLogger.Object);

        // Act
        var plan = await service.CreatePlanAsync(context);

        // Assert
        plan.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        capturedChatHistory.Count.ShouldBeGreaterThan(0);
        var userMessage = capturedChatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("C# project using .NET 10");
    }

    [Fact]
    public async Task CreatePlanAsync_IncludesCustomInstructionsInPrompt() {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlannerService>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var context = new AgentTaskContext {
            IssueTitle = "Add feature",
            IssueBody = "Implement new feature",
            InstructionsMarkdown = "# Instructions\nUse async/await"
        };

        var llmResponseJson = """
            {
                "problemSummary": "Add new feature",
                "constraints": [],
                "steps": [
                    {
                        "id": "step-1",
                        "title": "Implement feature",
                        "details": "Add the code",
                        "done": false
                    }
                ],
                "checklist": ["Tests pass"],
                "fileTargets": ["Feature.cs"]
            }
            """;

        ChatHistory? capturedChatHistory = null;
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponseJson);

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

        var service = new LlmPlannerService(kernel, mockLogger.Object);

        // Act
        var plan = await service.CreatePlanAsync(context);

        // Assert
        plan.ShouldNotBeNull();
        capturedChatHistory.ShouldNotBeNull();
        var userMessage = capturedChatHistory.LastOrDefault(m => m.Role == AuthorRole.User);
        userMessage.ShouldNotBeNull();
        userMessage.Content.ShouldContain("Use async/await");
    }

    [Fact]
    public async Task CreatePlanAsync_WithCancellationToken_PropagatesToken() {
        // Arrange
        var mockLogger = new Mock<ILogger<LlmPlannerService>>();
        var mockChatService = new Mock<IChatCompletionService>();

        var context = new AgentTaskContext {
            IssueTitle = "Test task",
            IssueBody = "Test description"
        };

        var cts = new CancellationTokenSource();
        CancellationToken capturedToken = default;

        var llmResponseJson = """
            {
                "problemSummary": "Test",
                "constraints": [],
                "steps": [],
                "checklist": [],
                "fileTargets": []
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, llmResponseJson);

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

        var service = new LlmPlannerService(kernel, mockLogger.Object);

        // Act
        await service.CreatePlanAsync(context, cts.Token);

        // Assert
        capturedToken.ShouldBe(cts.Token);
    }


    [Fact]
    public void ParsePlanFromResponse_ValidJson_ReturnsAgentPlan() {
        // Arrange
        var json = """
            {
                "problemSummary": "Add user authentication",
                "constraints": ["Follow security best practices", "Use existing auth library"],
                "steps": [
                    {
                        "id": "step-1",
                        "title": "Install authentication library",
                        "details": "Add package reference to the project",
                        "done": false
                    },
                    {
                        "id": "step-2",
                        "title": "Configure authentication",
                        "details": "Set up authentication middleware",
                        "done": false
                    }
                ],
                "checklist": ["All tests pass", "Security review completed"],
                "fileTargets": ["src/Program.cs", "src/Services/AuthService.cs"]
            }
            """;

        // Act
        var plan = ParsePlanFromJson(json);

        // Assert
        plan.ShouldNotBeNull();
        plan.ProblemSummary.ShouldBe("Add user authentication");
        plan.Constraints.Count.ShouldBe(2);
        plan.Constraints[0].ShouldBe("Follow security best practices");
        plan.Steps.Count.ShouldBe(2);
        plan.Steps[0].Id.ShouldBe("step-1");
        plan.Steps[0].Title.ShouldBe("Install authentication library");
        plan.Steps[0].Done.ShouldBeFalse();
        plan.Checklist.Count.ShouldBe(2);
        plan.FileTargets.Count.ShouldBe(2);
    }

    [Fact]
    public void ParsePlanFromResponse_MinimalJson_ReturnsAgentPlan() {
        // Arrange
        var json = """
            {
                "problemSummary": "Fix bug",
                "constraints": [],
                "steps": [],
                "checklist": [],
                "fileTargets": []
            }
            """;

        // Act
        var plan = ParsePlanFromJson(json);

        // Assert
        plan.ShouldNotBeNull();
        plan.ProblemSummary.ShouldBe("Fix bug");
        plan.Constraints.ShouldBeEmpty();
        plan.Steps.ShouldBeEmpty();
        plan.Checklist.ShouldBeEmpty();
        plan.FileTargets.ShouldBeEmpty();
    }

    [Fact]
    public void ParsePlanFromResponse_NullFields_UsesDefaults() {
        // Arrange
        var json = """
            {
                "problemSummary": null,
                "constraints": null,
                "steps": null,
                "checklist": null,
                "fileTargets": null
            }
            """;

        // Act
        var plan = ParsePlanFromJson(json);

        // Assert
        plan.ShouldNotBeNull();
        plan.ProblemSummary.ShouldBe("Task implementation");
        plan.Constraints.ShouldNotBeNull();
        plan.Steps.ShouldNotBeNull();
        plan.Checklist.ShouldNotBeNull();
        plan.FileTargets.ShouldNotBeNull();
    }

    [Fact]
    public void CreateFallbackPlan_ReturnsValidPlan() {
        // Arrange
        var context = new AgentTaskContext {
            IssueTitle = "Implement feature X",
            IssueBody = "Add feature X to the application"
        };

        // Act
        var plan = CreateFallbackPlanPublic(context);

        // Assert
        plan.ShouldNotBeNull();
        plan.ProblemSummary.ShouldContain("Implement feature X");
        plan.Steps.Count.ShouldBeGreaterThan(0);
        plan.Steps[0].Id.ShouldBe("step-1");
        plan.Checklist.Count.ShouldBeGreaterThan(0);
        plan.Constraints.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void BuildPlannerPrompt_IncludesIssueDetails() {
        // Arrange
        var context = new AgentTaskContext {
            IssueTitle = "Fix login bug",
            IssueBody = "Users cannot login after password reset"
        };

        // Act
        var prompt = BuildPromptPublic(context);

        // Assert
        prompt.ShouldContain("Fix login bug");
        prompt.ShouldContain("Users cannot login after password reset");
    }

    [Fact]
    public void BuildPlannerPrompt_IncludesRepositorySummary() {
        // Arrange
        var context = new AgentTaskContext {
            IssueTitle = "Update dependencies",
            IssueBody = "Upgrade to latest versions",
            RepositorySummary = "C# project with dotnet and xUnit"
        };

        // Act
        var prompt = BuildPromptPublic(context);

        // Assert
        prompt.ShouldContain("C# project with dotnet and xUnit");
        prompt.ShouldContain("Repository Context");
    }

    [Fact]
    public void BuildPlannerPrompt_IncludesCustomInstructions() {
        // Arrange
        var context = new AgentTaskContext {
            IssueTitle = "Add API endpoint",
            IssueBody = "Create new REST endpoint",
            InstructionsMarkdown = "# Custom Instructions\n\nUse async/await pattern"
        };

        // Act
        var prompt = BuildPromptPublic(context);

        // Assert
        prompt.ShouldContain("Custom Instructions");
        prompt.ShouldContain("Use async/await pattern");
    }

    [Fact]
    public void BuildPlannerPrompt_HandlesEmptyContext() {
        // Arrange
        var context = new AgentTaskContext {
            IssueTitle = "",
            IssueBody = ""
        };

        // Act
        var prompt = BuildPromptPublic(context);

        // Assert
        prompt.ShouldContain("# Task");
        prompt.ShouldContain("# Your Task");
    }

    [Fact]
    public void BuildPlannerPrompt_IncludesAllSections() {
        // Arrange
        var context = new AgentTaskContext {
            IssueTitle = "Complete task",
            IssueBody = "Task description",
            RepositorySummary = "Repo info",
            InstructionsMarkdown = "Instructions here"
        };

        // Act
        var prompt = BuildPromptPublic(context);

        // Assert
        prompt.ShouldContain("# Task");
        prompt.ShouldContain("# Repository Context");
        prompt.ShouldContain("# Custom Instructions");
        prompt.ShouldContain("# Your Task");
    }

    [Fact]
    public void ParsePlanFromResponse_HandlesStepsWithoutId() {
        // Arrange
        var json = """
            {
                "problemSummary": "Test",
                "constraints": [],
                "steps": [
                    {
                        "title": "Step without ID",
                        "details": "Details",
                        "done": false
                    }
                ],
                "checklist": [],
                "fileTargets": []
            }
            """;

        // Act
        var plan = ParsePlanFromJson(json);

        // Assert
        plan.ShouldNotBeNull();
        plan.Steps.Count.ShouldBe(1);
        plan.Steps[0].Id.ShouldBe("step-1"); // Default ID assigned
    }

    [Fact]
    public void CreateFallbackPlan_ContainsExpectedSteps() {
        // Arrange
        var context = new AgentTaskContext {
            IssueTitle = "Bug fix",
            IssueBody = "Fix the bug"
        };

        // Act
        var plan = CreateFallbackPlanPublic(context);

        // Assert
        plan.Steps.ShouldContain(s => s.Title.Contains("Analyze"));
        plan.Steps.ShouldContain(s => s.Title.Contains("Implement"));
        plan.Steps.ShouldContain(s => s.Title.Contains("test", StringComparison.OrdinalIgnoreCase));
        plan.Steps.ShouldAllBe(s => !s.Done);
    }

    // Helper methods to test private logic
    private static AgentPlan ParsePlanFromJson(string json) {
        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var planDto = JsonSerializer.Deserialize<AgentPlanDto>(json, options);

        if (planDto == null) {
            throw new InvalidOperationException("Failed to deserialize plan");
        }

        return new AgentPlan {
            ProblemSummary = planDto.ProblemSummary ?? "Task implementation",
            Constraints = planDto.Constraints ?? new List<string>(),
            Steps = planDto.Steps?.Select((s, index) => new PlanStep {
                Id = s.Id ?? $"step-{index + 1}",
                Title = s.Title ?? "Implementation step",
                Details = s.Details ?? "",
                Done = s.Done
            }).ToList() ?? new List<PlanStep>(),
            Checklist = planDto.Checklist ?? new List<string>(),
            FileTargets = planDto.FileTargets ?? new List<string>()
        };
    }

    private static AgentPlan CreateFallbackPlanPublic(AgentTaskContext context) {
        return new AgentPlan {
            ProblemSummary = $"Implement solution for: {context.IssueTitle}",
            Constraints =
            {
                "Follow existing code style and conventions",
                "Ensure all tests pass",
                "Update documentation if needed"
            },
            Steps =
            {
                new PlanStep
                {
                    Id = "step-1",
                    Title = "Analyze the requirements",
                    Details = "Review the issue description thoroughly and understand what needs to be implemented",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-2",
                    Title = "Identify affected files",
                    Details = "Locate the relevant files and code sections that need modifications",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-3",
                    Title = "Implement the solution",
                    Details = "Make the necessary code changes following best practices",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-4",
                    Title = "Add or update tests",
                    Details = "Ensure test coverage for the new or modified functionality",
                    Done = false
                },
                new PlanStep
                {
                    Id = "step-5",
                    Title = "Verify the implementation",
                    Details = "Run all tests and verify the changes work as expected",
                    Done = false
                }
            },
            Checklist =
            {
                "All code changes are complete",
                "All tests pass",
                "Documentation is updated",
                "Code follows project conventions"
            },
            FileTargets =
            {
                "TBD - determined during implementation"
            }
        };
    }

    private static string BuildPromptPublic(AgentTaskContext context) {
        var promptParts = new List<string>
        {
            "# Task",
            $"**Issue Title:** {context.IssueTitle}",
            "",
            "**Issue Description:**",
            context.IssueBody
        };

        if (!string.IsNullOrEmpty(context.RepositorySummary)) {
            promptParts.Add("");
            promptParts.Add("# Repository Context");
            promptParts.Add(context.RepositorySummary);
        }

        if (!string.IsNullOrEmpty(context.InstructionsMarkdown)) {
            promptParts.Add("");
            promptParts.Add("# Custom Instructions");
            promptParts.Add(context.InstructionsMarkdown);
        }

        promptParts.Add("");
        promptParts.Add("# Your Task");
        promptParts.Add("Based on the above information, create a detailed implementation plan following the JSON schema provided in the system message.");

        return string.Join("\n", promptParts);
    }

    // DTO classes for JSON deserialization
    private sealed class AgentPlanDto {
        public string? ProblemSummary { get; set; }
        public List<string>? Constraints { get; set; }
        public List<PlanStepDto>? Steps { get; set; }
        public List<string>? Checklist { get; set; }
        public List<string>? FileTargets { get; set; }
    }

    private sealed class PlanStepDto {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Details { get; set; }
        public bool Done { get; set; }
    }
}
