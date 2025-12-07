using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Planner;

public sealed class LlmPlannerService : IPlannerService {
    private readonly Kernel _kernel;
    private readonly ILogger<LlmPlannerService> _logger;
    private readonly IChatCompletionService _chatService;

    public LlmPlannerService(
        Kernel kernel,
        ILogger<LlmPlannerService> logger) {
        _kernel = kernel;
        _logger = logger;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Creating LLM-powered plan for issue: {IssueTitle}", context.IssueTitle);

        try {
            // Build the prompt with all available context
            var prompt = await BuildPlannerPromptAsync(context, cancellationToken);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(GetSystemPrompt());
            chatHistory.AddUserMessage(prompt);

            // Configure OpenAI settings to enforce JSON response
            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.3, // Lower temperature for more deterministic planning
                MaxTokens = 4000,
                ResponseFormat = "json_object" // Enforce JSON response
            };

            // Get completion from LLM
            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            _logger.LogDebug("LLM response: {Response}", response.Content);

            // Parse the JSON response into AgentPlan
            var plan = ParsePlanFromResponse(response.Content ?? "{}");

            _logger.LogInformation("Plan created with {StepCount} steps", plan.Steps.Count);
            return plan;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error creating plan with LLM, falling back to simple plan");

            // Fallback to a simple plan if LLM fails
            return CreateFallbackPlan(context);
        }
    }

    private static string GetSystemPrompt() {
        return """
            You are an expert software development planning assistant. Your role is to analyze coding tasks and create detailed, actionable implementation plans.

            You must respond with a JSON object that strictly follows this schema:

            {
              "problemSummary": "A concise 1-2 sentence summary of what needs to be done",
              "constraints": [
                "List of constraints, best practices, and requirements to follow",
                "E.g., 'Follow existing code style', 'Ensure all tests pass', etc."
              ],
              "steps": [
                {
                  "id": "step-1",
                  "title": "Short descriptive title",
                  "details": "Detailed explanation of what to do in this step",
                  "done": false
                }
              ],
              "checklist": [
                "Verification items that must be true when complete",
                "E.g., 'All tests pass', 'Documentation updated', etc."
              ],
              "fileTargets": [
                "List of files or directories likely to be modified",
                "E.g., 'src/services/UserService.cs', 'tests/', etc."
              ]
            }

            Guidelines:
            - Create 3-8 logical steps that guide the implementation
            - Each step should be specific and actionable
            - Consider the repository context (languages, frameworks, existing patterns)
            - Include testing and documentation steps
            - Identify potential files/directories to modify
            - List constraints like coding standards, test requirements, etc.
            - Make the checklist comprehensive but practical

            Respond ONLY with valid JSON. Do not include any text before or after the JSON object.
            """;
    }

    private async Task<string> BuildPlannerPromptAsync(AgentTaskContext context, CancellationToken cancellationToken) {
        var prompt = new System.Text.StringBuilder();
        prompt.AppendLine("# Task");
        prompt.AppendLine($"**Issue Title:** {context.IssueTitle}");
        prompt.AppendLine();
        prompt.AppendLine("**Issue Description:**");
        prompt.AppendLine(context.IssueBody);

        // Add repository summary if available
        if (!string.IsNullOrEmpty(context.RepositorySummary)) {
            prompt.AppendLine();
            prompt.AppendLine("# Repository Context");
            prompt.AppendLine(context.RepositorySummary);
        }

        // Add custom instructions if provided
        if (!string.IsNullOrEmpty(context.InstructionsMarkdown)) {
            prompt.AppendLine();
            prompt.AppendLine("# Custom Instructions");
            prompt.AppendLine(context.InstructionsMarkdown);
        }

        prompt.AppendLine();
        prompt.AppendLine("# Your Task");
        prompt.AppendLine("Based on the above information, create a detailed implementation plan following the JSON schema provided in the system message.");

        return prompt.ToString();
    }

    private AgentPlan ParsePlanFromResponse(string jsonResponse) {
        try {
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var planDto = JsonSerializer.Deserialize<AgentPlanDto>(jsonResponse, options);

            if (planDto == null) {
                throw new InvalidOperationException("Failed to deserialize plan from LLM response");
            }

            // Convert DTO to domain model
            return new AgentPlan {
                ProblemSummary = planDto.ProblemSummary ?? "Task implementation",
                Constraints = planDto.Constraints ?? [],
                Steps = planDto.Steps?.Select((s, index) => new PlanStep {
                    Id = s.Id ?? $"step-{index + 1}",
                    Title = s.Title ?? "Implementation step",
                    Details = s.Details ?? "",
                    Done = s.Done
                }).ToList() ?? [],
                Checklist = planDto.Checklist ?? [],
                FileTargets = planDto.FileTargets ?? []
            };
        }
        catch (JsonException ex) {
            _logger.LogError(ex, "Failed to parse plan JSON: {Json}", jsonResponse);
            throw new InvalidOperationException("Invalid JSON response from LLM", ex);
        }
    }

    private static AgentPlan CreateFallbackPlan(AgentTaskContext context) {
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
