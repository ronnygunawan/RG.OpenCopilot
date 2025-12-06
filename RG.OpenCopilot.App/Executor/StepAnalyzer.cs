using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using RG.OpenCopilot.Agent;

namespace RG.OpenCopilot.App.Executor;

/// <summary>
/// Analyzes plan steps using LLM to generate detailed action plans
/// </summary>
public sealed class StepAnalyzer : IStepAnalyzer {
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly IFileAnalyzer _fileAnalyzer;
    private readonly IContainerManager _containerManager;
    private readonly ILogger<StepAnalyzer> _logger;

    public StepAnalyzer(
        Kernel kernel,
        IFileAnalyzer fileAnalyzer,
        IContainerManager containerManager,
        ILogger<StepAnalyzer> logger) {
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _fileAnalyzer = fileAnalyzer;
        _containerManager = containerManager;
        _logger = logger;
    }

    public async Task<StepActionPlan> AnalyzeStepAsync(
        PlanStep step,
        RepositoryContext context,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Analyzing step: {StepTitle}", step.Title);

        try {
            var prompt = BuildAnalysisPrompt(step: step, context: context);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(GetSystemPrompt());
            chatHistory.AddUserMessage(prompt);

            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.2,
                MaxTokens = 2048,
                ResponseFormat = "json_object"
            };

            var responses = await _chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            var response = responses.FirstOrDefault();
            if (response == null) {
                throw new InvalidOperationException("LLM service returned no response");
            }

            _logger.LogDebug("LLM response: {Response}", response.Content);

            var actionPlan = ParseActionPlan(response.Content ?? "{}");

            _logger.LogInformation("Action plan created with {ActionCount} actions", actionPlan.Actions.Count);
            return actionPlan;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error analyzing step with LLM");
            throw new InvalidOperationException($"Failed to analyze step: {ex.Message}", ex);
        }
    }

    public async Task<RepositoryContext> BuildContextAsync(
        string containerId,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Building repository context for container {ContainerId}", containerId);

        try {
            var context = new RepositoryContext {
                Metadata = []
            };

            // Detect primary language by examining file extensions
            var fileTree = await _fileAnalyzer.BuildFileTreeAsync(
                containerId: containerId,
                rootPath: ".",
                cancellationToken: cancellationToken);

            var allFiles = fileTree.AllFiles;
            context.Files = allFiles;

            // Detect primary language
            var languageCounts = new Dictionary<string, int>();
            foreach (var file in allFiles) {
                var language = DetectLanguageFromExtension(file);
                if (language != "unknown") {
                    languageCounts[language] = languageCounts.GetValueOrDefault(language) + 1;
                }
            }

            if (languageCounts.Any()) {
                context.Language = languageCounts.OrderByDescending(kvp => kvp.Value).First().Key;
            }

            // Detect test framework
            context.TestFramework = await DetectTestFrameworkAsync(
                containerId: containerId,
                files: allFiles,
                language: context.Language,
                cancellationToken: cancellationToken);

            // Detect build tool
            context.BuildTool = DetectBuildTool(allFiles);

            _logger.LogInformation(
                "Repository context built: Language={Language}, BuildTool={BuildTool}, TestFramework={TestFramework}",
                context.Language,
                context.BuildTool,
                context.TestFramework);

            return context;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error building repository context");
            throw new InvalidOperationException($"Failed to build repository context: {ex.Message}", ex);
        }
    }

    private static string GetSystemPrompt() {
        return """
            You are an expert code analysis assistant. Your role is to analyze plan steps and generate detailed, actionable code change plans.

            You must respond with a JSON object that strictly follows this schema:

            {
              "actions": [
                {
                  "type": "CreateFile" | "ModifyFile" | "DeleteFile",
                  "filePath": "path/to/file.ext",
                  "description": "What this action does",
                  "request": {
                    "content": "Code content or modification details",
                    "beforeMarker": "optional context marker before change",
                    "afterMarker": "optional context marker after change",
                    "parameters": {
                      "key": "value"
                    }
                  }
                }
              ],
              "prerequisites": [
                "Things that need to be done first",
                "Dependencies that must exist"
              ],
              "requiresTests": true | false,
              "testFile": "path/to/test/file.ext or null",
              "mainFile": "path/to/main/implementation/file.ext or null"
            }

            Guidelines:
            - Analyze the step title and details to understand what code changes are needed
            - Consider the repository context (language, existing files, test framework)
            - Generate specific file paths based on the repository structure
            - Be precise about what code to create or modify
            - Include test file paths if tests are needed
            - List prerequisites that must be completed first
            - Make actions atomic and specific

            Respond ONLY with valid JSON. Do not include any text before or after the JSON object.
            """;
    }

    private string BuildAnalysisPrompt(PlanStep step, RepositoryContext context) {
        var prompt = new StringBuilder();

        prompt.AppendLine("# Plan Step to Analyze");
        prompt.AppendLine($"**Title:** {step.Title}");
        prompt.AppendLine($"**Details:** {step.Details}");
        prompt.AppendLine();

        prompt.AppendLine("# Repository Context");
        prompt.AppendLine($"**Language:** {context.Language}");
        
        if (!string.IsNullOrEmpty(context.BuildTool)) {
            prompt.AppendLine($"**Build Tool:** {context.BuildTool}");
        }
        
        if (!string.IsNullOrEmpty(context.TestFramework)) {
            prompt.AppendLine($"**Test Framework:** {context.TestFramework}");
        }

        prompt.AppendLine();
        prompt.AppendLine("**Existing Files:**");
        var relevantFiles = context.Files
            .Where(f => !f.Contains("bin/") && !f.Contains("obj/") && !f.Contains("node_modules/"))
            .Take(50)
            .ToList();

        foreach (var file in relevantFiles) {
            prompt.AppendLine($"- {file}");
        }

        if (context.Files.Count > 50) {
            prompt.AppendLine($"... and {context.Files.Count - 50} more files");
        }

        prompt.AppendLine();
        prompt.AppendLine("# Your Task");
        prompt.AppendLine("Analyze the plan step and generate a detailed action plan with specific code changes needed.");

        return prompt.ToString();
    }

    private StepActionPlan ParseActionPlan(string jsonResponse) {
        try {
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var planDto = JsonSerializer.Deserialize<StepActionPlanDto>(jsonResponse, options);

            if (planDto == null) {
                throw new InvalidOperationException("Failed to deserialize action plan from LLM response");
            }

            return new StepActionPlan {
                Actions = planDto.Actions?.Select(a => new CodeAction {
                    Type = Enum.Parse<ActionType>(a.Type ?? "ModifyFile", ignoreCase: true),
                    FilePath = a.FilePath ?? "",
                    Description = a.Description ?? "",
                    Request = new CodeGenerationRequest {
                        Content = a.Request?.Content ?? "",
                        BeforeMarker = a.Request?.BeforeMarker,
                        AfterMarker = a.Request?.AfterMarker,
                        Parameters = a.Request?.Parameters ?? []
                    }
                }).ToList() ?? [],
                Prerequisites = planDto.Prerequisites ?? [],
                RequiresTests = planDto.RequiresTests,
                TestFile = planDto.TestFile,
                MainFile = planDto.MainFile
            };
        }
        catch (JsonException ex) {
            _logger.LogError(ex, "Failed to parse action plan JSON: {Json}", jsonResponse);
            throw new InvalidOperationException("Invalid JSON response from LLM", ex);
        }
    }

    private static string DetectLanguageFromExtension(string filePath) {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" or ".tsx" => "typescript",
            ".jsx" => "javascript",
            ".py" => "python",
            ".java" => "java",
            ".go" => "go",
            ".rb" => "ruby",
            ".php" => "php",
            ".cpp" or ".cc" or ".cxx" => "cpp",
            ".c" => "c",
            _ => "unknown"
        };
    }

    private async Task<string?> DetectTestFrameworkAsync(
        string containerId,
        List<string> files,
        string language,
        CancellationToken cancellationToken) {
        // Check for test framework files/patterns
        if (language == "csharp") {
            if (files.Any(f => f.EndsWith(".csproj"))) {
                // Check for common test frameworks in project files
                var projectFiles = files.Where(f => f.EndsWith(".csproj")).ToList();
                foreach (var projectFile in projectFiles) {
                    try {
                        var content = await _containerManager.ReadFileInContainerAsync(
                            containerId: containerId,
                            filePath: projectFile,
                            cancellationToken: cancellationToken);

                        if (content.Contains("xunit", StringComparison.OrdinalIgnoreCase)) {
                            return "xUnit";
                        }
                        if (content.Contains("nunit", StringComparison.OrdinalIgnoreCase)) {
                            return "NUnit";
                        }
                        if (content.Contains("mstest", StringComparison.OrdinalIgnoreCase)) {
                            return "MSTest";
                        }
                    }
                    catch (Exception ex) {
                        // Ignore errors reading individual project files - file may not be accessible or parseable
                        _logger.LogDebug(ex, "Failed to read project file {ProjectFile}", projectFile);
                    }
                }
            }
        }
        else if (language == "javascript" || language == "typescript") {
            if (files.Any(f => f.EndsWith("package.json"))) {
                try {
                    var content = await _containerManager.ReadFileInContainerAsync(
                        containerId: containerId,
                        filePath: "package.json",
                        cancellationToken: cancellationToken);

                    if (content.Contains("jest", StringComparison.OrdinalIgnoreCase)) {
                        return "Jest";
                    }
                    if (content.Contains("mocha", StringComparison.OrdinalIgnoreCase)) {
                        return "Mocha";
                    }
                    if (content.Contains("jasmine", StringComparison.OrdinalIgnoreCase)) {
                        return "Jasmine";
                    }
                }
                catch (Exception ex) {
                    // Ignore errors reading package.json - file may not be accessible or parseable
                    _logger.LogDebug(ex, "Failed to read package.json");
                }
            }
        }
        else if (language == "python") {
            if (files.Any(f => f.Contains("pytest") || f.Contains("test_"))) {
                return "pytest";
            }
            if (files.Any(f => f.Contains("unittest"))) {
                return "unittest";
            }
        }

        return null;
    }

    private static string? DetectBuildTool(List<string> files) {
        if (files.Any(f => f.EndsWith(".csproj") || f.EndsWith(".sln"))) {
            return "dotnet";
        }
        if (files.Any(f => f.EndsWith("package.json"))) {
            return "npm";
        }
        if (files.Any(f => f.EndsWith("pom.xml"))) {
            return "maven";
        }
        if (files.Any(f => f.EndsWith("build.gradle") || f.EndsWith("build.gradle.kts"))) {
            return "gradle";
        }
        if (files.Any(f => f.EndsWith("Cargo.toml"))) {
            return "cargo";
        }
        if (files.Any(f => f.EndsWith("go.mod"))) {
            return "go";
        }
        if (files.Any(f => f.EndsWith("requirements.txt") || f.EndsWith("setup.py") || f.EndsWith("pyproject.toml"))) {
            return "pip";
        }

        return null;
    }

    // DTO classes for JSON deserialization
    private sealed class StepActionPlanDto {
        public List<CodeActionDto>? Actions { get; set; }
        public List<string>? Prerequisites { get; set; }
        public bool RequiresTests { get; set; }
        public string? TestFile { get; set; }
        public string? MainFile { get; set; }
    }

    private sealed class CodeActionDto {
        public string? Type { get; set; }
        public string? FilePath { get; set; }
        public string? Description { get; set; }
        public CodeGenerationRequestDto? Request { get; set; }
    }

    private sealed class CodeGenerationRequestDto {
        public string? Content { get; set; }
        public string? BeforeMarker { get; set; }
        public string? AfterMarker { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }
    }
}
