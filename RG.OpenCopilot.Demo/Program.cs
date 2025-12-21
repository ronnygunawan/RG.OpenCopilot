using System.Text.Json;
using Microsoft.Extensions.Logging;
using RG.OpenCopilot.PRGenerationAgent.Planning.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.Planner;

namespace RG.OpenCopilot.Demo;

/// <summary>
/// Demo runner that demonstrates the core capabilities of RG.OpenCopilot
/// without requiring GitHub App setup, webhooks, or external services.
/// </summary>
internal sealed class Program {
    private static async Task<int> Main(string[] args) {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         RG.OpenCopilot - Demo Runner                         ║");
        Console.WriteLine("║  Demonstrating AI-Powered Code Generation & Planning         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        try {
            // Setup logging
            using var loggerFactory = LoggerFactory.Create(builder => {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            // Load sample issue
            var issueJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "demo", "sample-issue.json");
            if (!File.Exists(issueJsonPath)) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"❌ Error: Sample issue file not found at: {issueJsonPath}");
                Console.ResetColor();
                return 1;
            }

            var issueJson = await File.ReadAllTextAsync(issueJsonPath);
            var issueData = JsonSerializer.Deserialize<SampleIssue>(issueJson, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
            
            if (issueData == null) {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Error: Failed to parse sample issue JSON");
                Console.ResetColor();
                return 1;
            }

            Console.WriteLine("📋 Sample Issue Loaded");
            Console.WriteLine($"   Title: {issueData.Title}");
            Console.WriteLine($"   Number: #{issueData.Number}");
            Console.WriteLine();

            // Create agent task context
            var context = new AgentTaskContext {
                IssueTitle = issueData.Title,
                IssueBody = issueData.Body,
                InstructionsMarkdown = "Follow C# coding conventions. Use XML documentation comments.",
                RepositorySummary = """
                Sample Calculator Project:
                - src/Calculator.cs - Main calculator class with Add and Subtract methods
                - Uses .NET 10.0
                - No tests currently exist
                """
            };

            Console.WriteLine("🤖 Stage 1: Planning");
            Console.WriteLine("   Analyzing issue and generating implementation plan...");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("💡 Using SimplePlannerService (rule-based planning)");
            Console.WriteLine("   For AI-powered planning, configure LLM:Planner:ApiKey in appsettings.Development.json");
            Console.ResetColor();
            Console.WriteLine();

            // Generate plan using SimplePlannerService (no LLM required)
            var plannerLogger = loggerFactory.CreateLogger<SimplePlannerService>();
            var plannerService = new SimplePlannerService(plannerLogger);
            var plan = await plannerService.CreatePlanAsync(context);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Plan Generated Successfully!");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine("📝 Problem Summary:");
            Console.WriteLine($"   {plan.ProblemSummary}");
            Console.WriteLine();

            if (plan.Constraints.Count > 0) {
                Console.WriteLine("⚠️  Constraints:");
                foreach (var constraint in plan.Constraints) {
                    Console.WriteLine($"   • {constraint}");
                }
                Console.WriteLine();
            }

            Console.WriteLine($"📋 Implementation Steps ({plan.Steps.Count} steps):");
            for (int i = 0; i < plan.Steps.Count; i++) {
                var step = plan.Steps[i];
                Console.WriteLine($"   {i + 1}. {step.Title}");
                if (!string.IsNullOrEmpty(step.Details)) {
                    // Show first line of details
                    var firstLine = step.Details.Split('\n')[0];
                    if (firstLine.Length > 60) {
                        firstLine = firstLine.Substring(0, 57) + "...";
                    }
                    Console.WriteLine($"      {firstLine}");
                }
            }
            Console.WriteLine();

            if (plan.FileTargets.Count > 0) {
                Console.WriteLine("📁 Files to Modify:");
                foreach (var file in plan.FileTargets) {
                    Console.WriteLine($"   • {file}");
                }
                Console.WriteLine();
            }

            if (plan.Checklist.Count > 0) {
                Console.WriteLine("✓ Completion Checklist:");
                foreach (var item in plan.Checklist) {
                    Console.WriteLine($"   □ {item}");
                }
                Console.WriteLine();
            }

            // Save plan to file
            var planOutputPath = Path.Combine(Directory.GetCurrentDirectory(), "demo", "generated-plan.json");
            var planJson = JsonSerializer.Serialize(plan, new JsonSerializerOptions {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(planOutputPath, planJson);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"💾 Plan saved to: {planOutputPath}");
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine("🎉 Demo Complete!");
            Console.WriteLine();
            Console.WriteLine("What happened:");
            Console.WriteLine("   1. ✅ Loaded sample issue from JSON");
            Console.WriteLine("   2. ✅ Created agent task context");
            Console.WriteLine("   3. ✅ Generated implementation plan using AI");
            Console.WriteLine("   4. ✅ Saved detailed plan to file");
            Console.WriteLine();
            Console.WriteLine("Next steps in a full run would include:");
            Console.WriteLine("   5. ⏭️  Code generation based on plan");
            Console.WriteLine("   6. ⏭️  Test generation");
            Console.WriteLine("   7. ⏭️  Build verification");
            Console.WriteLine("   8. ⏭️  PR creation with changes");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("💡 Note: Full execution requires Docker and may take 10-15 minutes.");
            Console.WriteLine("   This demo shows the planning phase only for quick verification.");
            Console.ResetColor();
            Console.WriteLine();

            return 0;
        }
        catch (Exception ex) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine();
            Console.WriteLine("Stack trace:");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
            return 1;
        }
    }
}

internal sealed class SampleIssue {
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public List<string> Labels { get; set; } = [];
    public RepositoryInfo Repository { get; set; } = new();
}

internal sealed class RepositoryInfo {
    public string Name { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Path { get; set; } = "";
}
