using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

namespace RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;

/// <summary>
/// Service for generating and maintaining code documentation
/// </summary>
internal sealed class DocumentationGenerator : IDocumentationGenerator {
    private readonly Kernel _kernel;
    private readonly ILogger<DocumentationGenerator> _logger;
    private readonly IChatCompletionService _chatService;
    private readonly IFileAnalyzer _fileAnalyzer;
    private readonly IFileEditor _fileEditor;
    private readonly IContainerManager _containerManager;

    public DocumentationGenerator(
        ExecutorKernel executorKernel,
        ILogger<DocumentationGenerator> logger,
        IFileAnalyzer fileAnalyzer,
        IFileEditor fileEditor,
        IContainerManager containerManager) {
        _kernel = executorKernel.Kernel;
        _logger = logger;
        _chatService = executorKernel.Kernel.GetRequiredService<IChatCompletionService>();
        _fileAnalyzer = fileAnalyzer;
        _fileEditor = fileEditor;
        _containerManager = containerManager;
    }

    public async Task<DocumentedCode> GenerateInlineDocsAsync(
        string code,
        string language,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Generating inline documentation for {Language}", language);

        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Code cannot be null or empty", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(language)) {
            throw new ArgumentException("Language cannot be null or empty", nameof(language));
        }

        try {
            var systemPrompt = BuildInlineDocSystemPrompt(language);
            var userPrompt = BuildInlineDocUserPrompt(code, language);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.3,
                MaxTokens = 4000
            };

            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            var documentedCode = ExtractCode(response.Content ?? "");
            var docCount = CountDocumentationComments(documentedCode, language);

            _logger.LogInformation(
                "Generated {Count} documentation comments for {Language}",
                docCount,
                language);

            return new DocumentedCode {
                Language = language,
                OriginalCode = code,
                DocumentedCodeContent = documentedCode,
                DocumentationCount = docCount
            };
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating inline documentation for {Language}", language);
            throw;
        }
    }

    public async Task UpdateReadmeAsync(
        string containerId,
        List<FileChange> changes,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Updating README in container {ContainerId}", containerId);

        if (string.IsNullOrWhiteSpace(containerId)) {
            throw new ArgumentException("Container ID cannot be null or empty", nameof(containerId));
        }

        ArgumentNullException.ThrowIfNull(changes);

        try {
            // Find README file
            var readmeFiles = await _fileAnalyzer.ListFilesAsync(
                containerId,
                pattern: "README.md",
                cancellationToken);

            string readmePath;
            string currentContent = "";

            if (readmeFiles.Count > 0) {
                readmePath = readmeFiles[0];
                currentContent = await _containerManager.ReadFileInContainerAsync(
                    containerId,
                    readmePath,
                    cancellationToken);
            }
            else {
                // Create new README if it doesn't exist
                readmePath = "/workspace/README.md";
                _logger.LogInformation("README.md not found, will create new one");
            }

            // Generate updated README content
            var updatedContent = await GenerateUpdatedReadmeAsync(
                currentContent,
                changes,
                cancellationToken);

            // Write updated README
            await _containerManager.WriteFileInContainerAsync(
                containerId,
                readmePath,
                updatedContent,
                cancellationToken);

            _logger.LogInformation("Successfully updated README at {Path}", readmePath);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error updating README in container {ContainerId}", containerId);
            throw;
        }
    }

    public async Task<ApiDocumentation> GenerateApiDocsAsync(
        string containerId,
        ApiDocFormat format = ApiDocFormat.Markdown,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation(
            "Generating API documentation in {Format} format for container {ContainerId}",
            format,
            containerId);

        if (string.IsNullOrWhiteSpace(containerId)) {
            throw new ArgumentException("Container ID cannot be null or empty", nameof(containerId));
        }

        try {
            // Scan for code files
            var codeFiles = await ScanCodeFilesAsync(containerId, cancellationToken);

            if (codeFiles.Count == 0) {
                _logger.LogWarning("No code files found in container {ContainerId}", containerId);
                return new ApiDocumentation {
                    Format = format,
                    Content = "No API documentation available - no code files found.",
                    FilePath = $"/workspace/API.{GetFileExtension(format)}"
                };
            }

            // Read all code files
            var codeContents = new Dictionary<string, string>();
            foreach (var file in codeFiles) {
                var content = await _containerManager.ReadFileInContainerAsync(containerId, file, cancellationToken);
                codeContents[file] = content;
            }

            // Generate API documentation
            var docContent = await GenerateApiDocumentationAsync(
                codeContents,
                format,
                cancellationToken);

            var filePath = $"/workspace/API.{GetFileExtension(format)}";

            _logger.LogInformation(
                "Generated API documentation at {Path}",
                filePath);

            return new ApiDocumentation {
                Format = format,
                Content = docContent,
                FilePath = filePath
            };
        }
        catch (Exception ex) {
            _logger.LogError(
                ex,
                "Error generating API documentation for container {ContainerId}",
                containerId);
            throw;
        }
    }

    public async Task UpdateChangelogAsync(
        string containerId,
        string version,
        List<ChangelogEntry> changes,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation(
            "Updating CHANGELOG for version {Version} in container {ContainerId}",
            version,
            containerId);

        if (string.IsNullOrWhiteSpace(containerId)) {
            throw new ArgumentException("Container ID cannot be null or empty", nameof(containerId));
        }

        if (string.IsNullOrWhiteSpace(version)) {
            throw new ArgumentException("Version cannot be null or empty", nameof(version));
        }

        ArgumentNullException.ThrowIfNull(changes);

        try {
            // Find CHANGELOG file
            var changelogFiles = await _fileAnalyzer.ListFilesAsync(
                containerId,
                pattern: "CHANGELOG.md",
                cancellationToken);

            string changelogPath;
            string currentContent = "";

            if (changelogFiles.Count > 0) {
                changelogPath = changelogFiles[0];
                currentContent = await _containerManager.ReadFileInContainerAsync(
                    containerId,
                    changelogPath,
                    cancellationToken);
            }
            else {
                // Create new CHANGELOG if it doesn't exist
                changelogPath = "/workspace/CHANGELOG.md";
                currentContent = "# Changelog\n\nAll notable changes to this project will be documented in this file.\n\n";
                _logger.LogInformation("CHANGELOG.md not found, will create new one");
            }

            // Generate updated CHANGELOG content
            var updatedContent = await GenerateUpdatedChangelogAsync(
                currentContent,
                version,
                changes,
                cancellationToken);

            // Write updated CHANGELOG
            await _containerManager.WriteFileInContainerAsync(
                containerId,
                changelogPath,
                updatedContent,
                cancellationToken);

            _logger.LogInformation(
                "Successfully updated CHANGELOG at {Path} with version {Version}",
                changelogPath,
                version);
        }
        catch (Exception ex) {
            _logger.LogError(
                ex,
                "Error updating CHANGELOG in container {ContainerId}",
                containerId);
            throw;
        }
    }

    public async Task<string> GenerateUsageExamplesAsync(
        string containerId,
        string apiFilePath,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation(
            "Generating usage examples for {FilePath} in container {ContainerId}",
            apiFilePath,
            containerId);

        if (string.IsNullOrWhiteSpace(containerId)) {
            throw new ArgumentException("Container ID cannot be null or empty", nameof(containerId));
        }

        if (string.IsNullOrWhiteSpace(apiFilePath)) {
            throw new ArgumentException("API file path cannot be null or empty", nameof(apiFilePath));
        }

        try {
            // Read the API file
            var apiCode = await _containerManager.ReadFileInContainerAsync(
                containerId,
                apiFilePath,
                cancellationToken);

            // Determine language from file extension
            var language = DetermineLanguage(apiFilePath);

            // Generate usage examples
            var examples = await GenerateExamplesAsync(
                apiCode,
                language,
                cancellationToken);

            _logger.LogInformation(
                "Generated usage examples for {FilePath}",
                apiFilePath);

            return examples;
        }
        catch (Exception ex) {
            _logger.LogError(
                ex,
                "Error generating usage examples for {FilePath}",
                apiFilePath);
            throw;
        }
    }

    private string BuildInlineDocSystemPrompt(string language) {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert technical writer specializing in code documentation.");
        prompt.AppendLine();
        prompt.AppendLine(DocumentationPrompts.GetInlineDocumentationPrompt(language));
        prompt.AppendLine();
        prompt.AppendLine("# Response Format");
        prompt.AppendLine("- Return ONLY the documented code");
        prompt.AppendLine("- Do not include markdown code blocks or explanations");
        prompt.AppendLine("- Do not add explanatory text before or after the code");
        prompt.AppendLine("- Preserve the original code structure and formatting");
        prompt.AppendLine("- Add documentation comments above each public member");

        return prompt.ToString();
    }

    private string BuildInlineDocUserPrompt(string code, string language) {
        return $"""
            Add inline documentation to the following {language} code.
            Document all public APIs, classes, methods, and functions.
            
            ```{language}
            {code}
            ```
            
            Return the same code with documentation comments added.
            """;
    }

    private async Task<string> GenerateUpdatedReadmeAsync(
        string currentContent,
        List<FileChange> changes,
        CancellationToken cancellationToken) {
        var systemPrompt = BuildReadmeSystemPrompt();
        var userPrompt = BuildReadmeUserPrompt(currentContent, changes);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings {
            Temperature = 0.4,
            MaxTokens = 4000
        };

        var response = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel,
            cancellationToken);

        return response.Content ?? currentContent;
    }

    private string BuildReadmeSystemPrompt() {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert technical writer specializing in README documentation.");
        prompt.AppendLine();
        prompt.AppendLine(DocumentationPrompts.GetReadmeUpdatePrompt());
        prompt.AppendLine();
        prompt.AppendLine("# Response Format");
        prompt.AppendLine("- Return ONLY the updated README content");
        prompt.AppendLine("- Use proper markdown formatting");
        prompt.AppendLine("- Maintain existing structure and style");

        return prompt.ToString();
    }

    private string BuildReadmeUserPrompt(string currentContent, List<FileChange> changes) {
        var prompt = new StringBuilder();
        prompt.AppendLine("Update the README based on these code changes:");
        prompt.AppendLine();

        foreach (var change in changes) {
            prompt.AppendLine($"## {change.Type}: {change.Path}");
            if (!string.IsNullOrEmpty(change.NewContent)) {
                prompt.AppendLine("```");
                prompt.AppendLine(change.NewContent.Length > 500
                    ? change.NewContent[..500] + "..."
                    : change.NewContent);
                prompt.AppendLine("```");
            }
            prompt.AppendLine();
        }

        if (!string.IsNullOrEmpty(currentContent)) {
            prompt.AppendLine("Current README:");
            prompt.AppendLine("```markdown");
            prompt.AppendLine(currentContent);
            prompt.AppendLine("```");
        }

        return prompt.ToString();
    }

    private async Task<string> GenerateApiDocumentationAsync(
        Dictionary<string, string> codeContents,
        ApiDocFormat format,
        CancellationToken cancellationToken) {
        var systemPrompt = BuildApiDocSystemPrompt(format);
        var userPrompt = BuildApiDocUserPrompt(codeContents);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings {
            Temperature = 0.3,
            MaxTokens = 6000
        };

        var response = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel,
            cancellationToken);

        return response.Content ?? "";
    }

    private string BuildApiDocSystemPrompt(ApiDocFormat format) {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert technical writer specializing in API documentation.");
        prompt.AppendLine();
        prompt.AppendLine(DocumentationPrompts.GetApiDocGenerationPrompt());
        prompt.AppendLine();
        prompt.AppendLine($"# Output Format: {format}");

        switch (format) {
            case ApiDocFormat.Markdown:
                prompt.AppendLine("- Use markdown formatting");
                prompt.AppendLine("- Include a table of contents");
                prompt.AppendLine("- Use code blocks for examples");
                break;
            case ApiDocFormat.Html:
                prompt.AppendLine("- Generate valid HTML");
                prompt.AppendLine("- Include CSS for styling");
                prompt.AppendLine("- Make it responsive");
                break;
            case ApiDocFormat.Xml:
                prompt.AppendLine("- Generate well-formed XML");
                prompt.AppendLine("- Use appropriate schema");
                break;
        }

        return prompt.ToString();
    }

    private string BuildApiDocUserPrompt(Dictionary<string, string> codeContents) {
        var prompt = new StringBuilder();
        prompt.AppendLine("Generate comprehensive API documentation for the following code:");
        prompt.AppendLine();

        foreach (var (filePath, content) in codeContents) {
            prompt.AppendLine($"## File: {filePath}");
            prompt.AppendLine("```");
            prompt.AppendLine(content.Length > 1000 ? content[..1000] + "..." : content);
            prompt.AppendLine("```");
            prompt.AppendLine();
        }

        return prompt.ToString();
    }

    private async Task<string> GenerateUpdatedChangelogAsync(
        string currentContent,
        string version,
        List<ChangelogEntry> changes,
        CancellationToken cancellationToken) {
        var systemPrompt = BuildChangelogSystemPrompt();
        var userPrompt = BuildChangelogUserPrompt(currentContent, version, changes);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings {
            Temperature = 0.3,
            MaxTokens = 4000
        };

        var response = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel,
            cancellationToken);

        return response.Content ?? currentContent;
    }

    private string BuildChangelogSystemPrompt() {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert technical writer specializing in changelog documentation.");
        prompt.AppendLine();
        prompt.AppendLine(DocumentationPrompts.GetChangelogUpdatePrompt());
        prompt.AppendLine();
        prompt.AppendLine("# Response Format");
        prompt.AppendLine("- Return ONLY the updated CHANGELOG content");
        prompt.AppendLine("- Follow Keep a Changelog format");
        prompt.AppendLine("- Maintain existing entries");

        return prompt.ToString();
    }

    private string BuildChangelogUserPrompt(
        string currentContent,
        string version,
        List<ChangelogEntry> changes) {
        var prompt = new StringBuilder();
        prompt.AppendLine($"Add version {version} to the CHANGELOG with these changes:");
        prompt.AppendLine();

        var grouped = changes.GroupBy(c => c.Type);
        foreach (var group in grouped.OrderBy(g => g.Key)) {
            prompt.AppendLine($"### {group.Key}");
            foreach (var change in group) {
                prompt.AppendLine($"- {change.Description}");
            }
            prompt.AppendLine();
        }

        if (!string.IsNullOrEmpty(currentContent)) {
            prompt.AppendLine("Current CHANGELOG:");
            prompt.AppendLine("```markdown");
            prompt.AppendLine(currentContent);
            prompt.AppendLine("```");
        }

        return prompt.ToString();
    }

    private async Task<string> GenerateExamplesAsync(
        string apiCode,
        string language,
        CancellationToken cancellationToken) {
        var systemPrompt = BuildExamplesSystemPrompt();
        var userPrompt = BuildExamplesUserPrompt(apiCode, language);

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);
        chatHistory.AddUserMessage(userPrompt);

        var executionSettings = new OpenAIPromptExecutionSettings {
            Temperature = 0.4,
            MaxTokens = 3000
        };

        var response = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings,
            _kernel,
            cancellationToken);

        return response.Content ?? "";
    }

    private string BuildExamplesSystemPrompt() {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are an expert developer creating practical code examples.");
        prompt.AppendLine();
        prompt.AppendLine(DocumentationPrompts.GetUsageExamplesPrompt());

        return prompt.ToString();
    }

    private string BuildExamplesUserPrompt(string apiCode, string language) {
        return $"""
            Generate practical usage examples for this {language} API:
            
            ```{language}
            {apiCode}
            ```
            
            Show common use cases with complete, runnable examples.
            """;
    }

    private async Task<List<string>> ScanCodeFilesAsync(
        string containerId,
        CancellationToken cancellationToken) {
        var extensions = new[] { "*.cs", "*.js", "*.ts", "*.py", "*.java", "*.go" };
        var files = new List<string>();

        foreach (var pattern in extensions) {
            var found = await _fileAnalyzer.ListFilesAsync(
                containerId,
                pattern,
                cancellationToken);
            files.AddRange(found);
        }

        return files;
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

        return llmResponse.Trim();
    }

    private int CountDocumentationComments(string code, string language) {
        return language.ToLowerInvariant() switch {
            "c#" or "csharp" => Regex.Matches(code, @"///").Count,
            "javascript" or "js" or "typescript" or "ts" => Regex.Matches(code, @"/\*\*").Count,
            "python" or "py" => Regex.Matches(code, "\"\"\"").Count,
            "java" => Regex.Matches(code, @"/\*\*").Count,
            "go" => Regex.Matches(code, @"^//", RegexOptions.Multiline).Count,
            _ => 0
        };
    }

    private string DetermineLanguage(string filePath) {
        var extension = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch {
            ".cs" => "C#",
            ".js" => "JavaScript",
            ".ts" => "TypeScript",
            ".py" => "Python",
            ".java" => "Java",
            ".go" => "Go",
            _ => "Unknown"
        };
    }

    private string GetFileExtension(ApiDocFormat format) {
        return format switch {
            ApiDocFormat.Markdown => "md",
            ApiDocFormat.Html => "html",
            ApiDocFormat.Xml => "xml",
            _ => "md"
        };
    }
}
