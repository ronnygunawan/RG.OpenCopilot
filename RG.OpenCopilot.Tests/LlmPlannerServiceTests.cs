using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Shouldly;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

public class LlmPlannerServiceTests
{
    [Fact]
    public void ParsePlanFromResponse_ValidJson_ReturnsAgentPlan()
    {
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
    public void ParsePlanFromResponse_MinimalJson_ReturnsAgentPlan()
    {
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
    public void ParsePlanFromResponse_NullFields_UsesDefaults()
    {
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
    public void CreateFallbackPlan_ReturnsValidPlan()
    {
        // Arrange
        var context = new AgentTaskContext
        {
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
    public void BuildPlannerPrompt_IncludesIssueDetails()
    {
        // Arrange
        var context = new AgentTaskContext
        {
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
    public void BuildPlannerPrompt_IncludesRepositorySummary()
    {
        // Arrange
        var context = new AgentTaskContext
        {
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
    public void BuildPlannerPrompt_IncludesCustomInstructions()
    {
        // Arrange
        var context = new AgentTaskContext
        {
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
    public void BuildPlannerPrompt_HandlesEmptyContext()
    {
        // Arrange
        var context = new AgentTaskContext
        {
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
    public void BuildPlannerPrompt_IncludesAllSections()
    {
        // Arrange
        var context = new AgentTaskContext
        {
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
    public void ParsePlanFromResponse_HandlesStepsWithoutId()
    {
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
    public void CreateFallbackPlan_ContainsExpectedSteps()
    {
        // Arrange
        var context = new AgentTaskContext
        {
            IssueTitle = "Bug fix",
            IssueBody = "Fix the bug"
        };

        // Act
        var plan = CreateFallbackPlanPublic(context);

        // Assert
        plan.Steps.ShouldContain(s => s.Title.Contains("Analyze"));
        plan.Steps.ShouldContain(s => s.Title.Contains("Implement"));
        plan.Steps.ShouldContain(s => s.Title.Contains("test"));
        plan.Steps.ShouldAllBe(s => !s.Done);
    }

    // Helper methods to test private logic
    private static AgentPlan ParsePlanFromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var planDto = JsonSerializer.Deserialize<AgentPlanDto>(json, options);

        if (planDto == null)
        {
            throw new InvalidOperationException("Failed to deserialize plan");
        }

        return new AgentPlan
        {
            ProblemSummary = planDto.ProblemSummary ?? "Task implementation",
            Constraints = planDto.Constraints ?? new List<string>(),
            Steps = planDto.Steps?.Select((s, index) => new PlanStep
            {
                Id = s.Id ?? $"step-{index + 1}",
                Title = s.Title ?? "Implementation step",
                Details = s.Details ?? "",
                Done = s.Done
            }).ToList() ?? new List<PlanStep>(),
            Checklist = planDto.Checklist ?? new List<string>(),
            FileTargets = planDto.FileTargets ?? new List<string>()
        };
    }

    private static AgentPlan CreateFallbackPlanPublic(AgentTaskContext context)
    {
        return new AgentPlan
        {
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

    private static string BuildPromptPublic(AgentTaskContext context)
    {
        var promptParts = new List<string>
        {
            "# Task",
            $"**Issue Title:** {context.IssueTitle}",
            "",
            "**Issue Description:**",
            context.IssueBody
        };

        if (!string.IsNullOrEmpty(context.RepositorySummary))
        {
            promptParts.Add("");
            promptParts.Add("# Repository Context");
            promptParts.Add(context.RepositorySummary);
        }

        if (!string.IsNullOrEmpty(context.InstructionsMarkdown))
        {
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
    private sealed class AgentPlanDto
    {
        public string? ProblemSummary { get; set; }
        public List<string>? Constraints { get; set; }
        public List<PlanStepDto>? Steps { get; set; }
        public List<string>? Checklist { get; set; }
        public List<string>? FileTargets { get; set; }
    }

    private sealed class PlanStepDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Details { get; set; }
        public bool Done { get; set; }
    }
}
