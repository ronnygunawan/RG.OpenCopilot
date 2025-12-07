using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;

public sealed class CodeGenerator : ICodeGenerator {
    private readonly Kernel _kernel;
    private readonly ILogger<CodeGenerator> _logger;
    private readonly IChatCompletionService _chatService;
    private readonly IGitHubClient _gitHubClient;
    private string? _cachedRepositoryInstructions;
    private string? _cachedRepositoryOwner;
    private string? _cachedRepositoryName;

    public CodeGenerator(
        Kernel kernel,
        ILogger<CodeGenerator> logger,
        IGitHubClient gitHubClient) {
        _kernel = kernel;
        _logger = logger;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _gitHubClient = gitHubClient;
    }

    public async Task<string> GenerateCodeAsync(
        LlmCodeGenerationRequest request,
        string? existingCode = null,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation(
            "Generating code for {Language} - {Description}",
            request.Language,
            request.Description);

        try {
            var prompt = BuildCodePrompt(request, existingCode);
            var systemPrompt = await BuildSystemPromptAsync(request, cancellationToken);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(prompt);

            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.2,
                MaxTokens = 4000
            };

            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            var generatedCode = ExtractCode(response.Content ?? "");
            
            _logger.LogInformation(
                "Generated {LineCount} lines of {Language} code",
                generatedCode.Split('\n').Length,
                request.Language);

            return generatedCode;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating code for {Language}", request.Language);
            throw;
        }
    }

    public async Task<string> GenerateClassAsync(
        string className,
        string description,
        string language,
        CancellationToken cancellationToken = default) {
        var request = new LlmCodeGenerationRequest {
            Description = $"Create a class named '{className}'. {description}",
            Language = language,
            FilePath = ""
        };

        return await GenerateCodeAsync(request, existingCode: null, cancellationToken);
    }

    public async Task<string> GenerateFunctionAsync(
        string functionName,
        string description,
        string language,
        CancellationToken cancellationToken = default) {
        var request = new LlmCodeGenerationRequest {
            Description = $"Create a function named '{functionName}'. {description}",
            Language = language,
            FilePath = ""
        };

        return await GenerateCodeAsync(request, existingCode: null, cancellationToken);
    }

    public Task<bool> ValidateSyntaxAsync(
        string code,
        string language,
        CancellationToken cancellationToken = default) {
        try {
            var isValid = language.ToLowerInvariant() switch {
                "c#" or "csharp" => ValidateCSharpSyntax(code),
                "javascript" or "js" => ValidateJavaScriptSyntax(code),
                "typescript" or "ts" => ValidateTypeScriptSyntax(code),
                "python" or "py" => ValidatePythonSyntax(code),
                _ => ValidateGenericSyntax(code)
            };

            return Task.FromResult(isValid);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Syntax validation failed for {Language}", language);
            return Task.FromResult(false);
        }
    }

    private async Task<string> BuildSystemPromptAsync(
        LlmCodeGenerationRequest request,
        CancellationToken cancellationToken) {
        var promptBuilder = new StringBuilder();

        // 1. Core role and responsibility
        promptBuilder.AppendLine("You are an expert software developer with deep knowledge of multiple programming languages and best practices.");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Your role is to generate high-quality, production-ready code based on requirements.");
        promptBuilder.AppendLine();

        // 2. General best practices (curated from community standards)
        promptBuilder.AppendLine(CodeGenerationPrompts.GetBestPractices());
        promptBuilder.AppendLine();

        // 3. Technology-specific prompts based on the language
        var technologyPrompt = CodeGenerationPrompts.GetTechnologyPrompt(request.Language);
        promptBuilder.AppendLine(technologyPrompt);
        promptBuilder.AppendLine();

        // 4. Repository-specific instructions from copilot-instructions.md
        var repoInstructions = await LoadRepositoryInstructionsAsync(request, cancellationToken);
        if (!string.IsNullOrWhiteSpace(repoInstructions)) {
            promptBuilder.AppendLine("# Repository-Specific Instructions");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine(repoInstructions);
            promptBuilder.AppendLine();
        }

        // 5. Code modification guidelines
        promptBuilder.AppendLine("# When Modifying Existing Code");
        promptBuilder.AppendLine("- Preserve the original code style and structure");
        promptBuilder.AppendLine("- Only make the minimal necessary changes");
        promptBuilder.AppendLine("- Maintain consistency with existing patterns");
        promptBuilder.AppendLine("- Keep existing comments unless they're outdated");
        promptBuilder.AppendLine();

        // 6. Response format requirements
        promptBuilder.AppendLine("# Response Format");
        promptBuilder.AppendLine("- Return ONLY the code, without markdown code blocks or explanations");
        promptBuilder.AppendLine("- Do not include ```language``` markers");
        promptBuilder.AppendLine("- Do not add explanatory text before or after the code");
        promptBuilder.AppendLine("- Start directly with the code");

        return promptBuilder.ToString();
    }

    private async Task<string?> LoadRepositoryInstructionsAsync(
        LlmCodeGenerationRequest request,
        CancellationToken cancellationToken) {
        // Extract repository info from context if provided
        if (!request.Context.TryGetValue("RepositoryOwner", out var owner) ||
            !request.Context.TryGetValue("RepositoryName", out var repo)) {
            return null;
        }

        // Use cached instructions if loading from the same repository
        if (owner == _cachedRepositoryOwner && 
            repo == _cachedRepositoryName && 
            _cachedRepositoryInstructions != null) {
            return _cachedRepositoryInstructions;
        }

        try {
            // Try to load copilot-instructions.md from the repository
            var contents = await _gitHubClient.Repository.Content.GetAllContents(
                owner: owner,
                name: repo,
                path: "copilot-instructions.md");

            if (contents != null && contents.Count > 0 && !string.IsNullOrEmpty(contents[0].Content)) {
                _logger.LogInformation(
                    "Loaded repository instructions from {Owner}/{Repo}/copilot-instructions.md",
                    owner,
                    repo);

                _cachedRepositoryOwner = owner;
                _cachedRepositoryName = repo;
                _cachedRepositoryInstructions = contents[0].Content;

                return _cachedRepositoryInstructions;
            }
        }
        catch (NotFoundException) {
            _logger.LogDebug(
                "No copilot-instructions.md found in {Owner}/{Repo}",
                owner,
                repo);
        }
        catch (Exception ex) {
            _logger.LogWarning(
                ex,
                "Error loading repository instructions from {Owner}/{Repo}",
                owner,
                repo);
        }

        return null;
    }

    private string BuildCodePrompt(LlmCodeGenerationRequest request, string? existingCode) {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Task: Generate {request.Language} Code");
        prompt.AppendLine();
        prompt.AppendLine("## Requirements");
        prompt.AppendLine(request.Description);
        prompt.AppendLine();

        if (!string.IsNullOrEmpty(request.FilePath)) {
            prompt.AppendLine($"## Target File Path");
            prompt.AppendLine(request.FilePath);
            prompt.AppendLine();
        }

        if (request.Dependencies.Count > 0) {
            prompt.AppendLine("## Dependencies");
            foreach (var dependency in request.Dependencies) {
                prompt.AppendLine($"- {dependency}");
            }
            prompt.AppendLine();
        }

        if (request.Context.Count > 0) {
            prompt.AppendLine("## Additional Context");
            foreach (var (key, value) in request.Context) {
                prompt.AppendLine($"**{key}:**");
                prompt.AppendLine(value);
                prompt.AppendLine();
            }
        }

        if (!string.IsNullOrEmpty(existingCode)) {
            prompt.AppendLine("## Existing Code to Modify");
            prompt.AppendLine("```");
            prompt.AppendLine(existingCode);
            prompt.AppendLine("```");
            prompt.AppendLine();
            prompt.AppendLine("Modify the above code according to the requirements, preserving its style and structure.");
        }
        else {
            prompt.AppendLine("Generate new code according to the requirements above.");
        }

        return prompt.ToString();
    }

    private string ExtractCode(string llmResponse) {
        if (string.IsNullOrWhiteSpace(llmResponse)) {
            return "";
        }

        // Remove markdown code blocks if present
        var codeBlockPattern = @"```(?:\w+)?\s*\n?(.*?)\n?```";
        var match = Regex.Match(llmResponse, codeBlockPattern, RegexOptions.Singleline);
        
        if (match.Success) {
            return match.Groups[1].Value.Trim();
        }

        // If no code block markers, return the response as-is but trimmed
        return llmResponse.Trim();
    }

    private bool ValidateCSharpSyntax(string code) {
        // Basic C# syntax checks
        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        // Check for balanced braces
        var openBraces = code.Count(c => c == '{');
        var closeBraces = code.Count(c => c == '}');
        if (openBraces != closeBraces) {
            _logger.LogWarning("Unbalanced braces in C# code");
            return false;
        }

        // Check for balanced parentheses
        var openParens = code.Count(c => c == '(');
        var closeParens = code.Count(c => c == ')');
        if (openParens != closeParens) {
            _logger.LogWarning("Unbalanced parentheses in C# code");
            return false;
        }

        // Check for balanced brackets
        var openBrackets = code.Count(c => c == '[');
        var closeBrackets = code.Count(c => c == ']');
        if (openBrackets != closeBrackets) {
            _logger.LogWarning("Unbalanced brackets in C# code");
            return false;
        }

        return true;
    }

    private bool ValidateJavaScriptSyntax(string code) {
        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        // Basic JavaScript syntax checks
        var openBraces = code.Count(c => c == '{');
        var closeBraces = code.Count(c => c == '}');
        if (openBraces != closeBraces) {
            _logger.LogWarning("Unbalanced braces in JavaScript code");
            return false;
        }

        var openParens = code.Count(c => c == '(');
        var closeParens = code.Count(c => c == ')');
        if (openParens != closeParens) {
            _logger.LogWarning("Unbalanced parentheses in JavaScript code");
            return false;
        }

        return true;
    }

    private bool ValidateTypeScriptSyntax(string code) {
        // TypeScript has same basic syntax rules as JavaScript
        return ValidateJavaScriptSyntax(code);
    }

    private bool ValidatePythonSyntax(string code) {
        if (string.IsNullOrWhiteSpace(code)) {
            return false;
        }

        // Basic Python syntax checks
        var openParens = code.Count(c => c == '(');
        var closeParens = code.Count(c => c == ')');
        if (openParens != closeParens) {
            _logger.LogWarning("Unbalanced parentheses in Python code");
            return false;
        }

        var openBrackets = code.Count(c => c == '[');
        var closeBrackets = code.Count(c => c == ']');
        if (openBrackets != closeBrackets) {
            _logger.LogWarning("Unbalanced brackets in Python code");
            return false;
        }

        return true;
    }

    private bool ValidateGenericSyntax(string code) {
        // For unknown languages, just check if code is not empty
        return !string.IsNullOrWhiteSpace(code);
    }
}
