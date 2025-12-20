using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services.Docker;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Executor;

/// <summary>
/// Service for running builds, detecting errors, and auto-fixing with LLM assistance
/// </summary>
public sealed class BuildVerifier : IBuildVerifier {
    private readonly IContainerManager _containerManager;
    private readonly IFileEditor _fileEditor;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<BuildVerifier> _logger;

    public BuildVerifier(
        IContainerManager containerManager,
        IFileEditor fileEditor,
        ExecutorKernel executorKernel,
        ILogger<BuildVerifier> logger) {
        _containerManager = containerManager;
        _fileEditor = fileEditor;
        _kernel = executorKernel.Kernel;
        _logger = logger;
        _chatService = executorKernel.Kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<BuildResult> VerifyBuildAsync(string containerId, int maxRetries = 3, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Starting build verification for container {ContainerId} with max {MaxRetries} retries", containerId, maxRetries);
        
        var stopwatch = Stopwatch.StartNew();
        var allFixesApplied = new List<CodeFix>();
        var attempts = 0;

        for (attempts = 1; attempts <= maxRetries; attempts++) {
            _logger.LogInformation("Build attempt {Attempt} of {MaxRetries}", attempts, maxRetries);

            var buildResult = await RunBuildAsync(containerId, cancellationToken);

            if (buildResult.Success) {
                _logger.LogInformation("Build succeeded on attempt {Attempt}", attempts);
                stopwatch.Stop();
                return new BuildResult {
                    Success = true,
                    Attempts = attempts,
                    Output = buildResult.Output,
                    Errors = [],
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed,
                    ToolAvailable = true
                };
            }

            // Check if build failed due to missing tool
            if (!string.IsNullOrEmpty(buildResult.Error) && buildResult.Error.Contains("is not available")) {
                _logger.LogWarning("Build failed due to missing tool");
                stopwatch.Stop();
                var missingTool = await DetectBuildToolAsync(containerId, cancellationToken);
                return new BuildResult {
                    Success = false,
                    Attempts = attempts,
                    Output = buildResult.Output,
                    Errors = [],
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed,
                    ToolAvailable = false,
                    MissingTool = missingTool
                };
            }

            // Detect build tool for error parsing
            var buildTool = await DetectBuildToolAsync(containerId, cancellationToken);
            if (buildTool == null) {
                _logger.LogWarning("Could not detect build tool, cannot parse errors");
                stopwatch.Stop();
                return new BuildResult {
                    Success = false,
                    Attempts = attempts,
                    Output = buildResult.Output,
                    Errors = [],
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed
                };
            }

            // Parse errors from build output
            var errors = await ParseBuildErrorsAsync(buildResult.Output + "\n" + buildResult.Error, buildTool, cancellationToken);
            if (errors.Count == 0) {
                _logger.LogWarning("Build failed but no parseable errors found");
                stopwatch.Stop();
                return new BuildResult {
                    Success = false,
                    Attempts = attempts,
                    Output = buildResult.Output + "\n" + buildResult.Error,
                    Errors = [],
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed
                };
            }

            _logger.LogInformation("Found {ErrorCount} errors in build output", errors.Count);

            // If this is the last attempt, don't generate fixes
            if (attempts >= maxRetries) {
                _logger.LogWarning("Max retries reached, returning failure");
                stopwatch.Stop();
                return new BuildResult {
                    Success = false,
                    Attempts = attempts,
                    Output = buildResult.Output + "\n" + buildResult.Error,
                    Errors = errors,
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed
                };
            }

            // Generate fixes using LLM
            var fixes = await GenerateFixesAsync(errors, cancellationToken);
            if (fixes.Count == 0) {
                _logger.LogWarning("No fixes could be generated for the errors");
                stopwatch.Stop();
                return new BuildResult {
                    Success = false,
                    Attempts = attempts,
                    Output = buildResult.Output + "\n" + buildResult.Error,
                    Errors = errors,
                    FixesApplied = allFixesApplied,
                    Duration = stopwatch.Elapsed
                };
            }

            _logger.LogInformation("Generated {FixCount} fixes", fixes.Count);

            // Apply fixes
            foreach (var fix in fixes) {
                try {
                    // Use ModifyFileAsync to replace the original code with fixed code
                    await _fileEditor.ModifyFileAsync(
                        containerId: containerId,
                        filePath: fix.FilePath,
                        transform: content => content.Replace(fix.OriginalCode, fix.FixedCode),
                        cancellationToken: cancellationToken);
                    
                    allFixesApplied.Add(fix);
                    _logger.LogInformation("Applied fix to {FilePath}: {Description}", fix.FilePath, fix.Description);
                }
                catch (Exception ex) {
                    _logger.LogWarning(ex, "Failed to apply fix to {FilePath}", fix.FilePath);
                }
            }

            // Add delay before retry (exponential backoff)
            if (attempts < maxRetries) {
                var delaySeconds = Math.Pow(2, attempts - 1);
                _logger.LogInformation("Waiting {Delay}s before retry", delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
            }
        }

        stopwatch.Stop();
        return new BuildResult {
            Success = false,
            Attempts = attempts,
            Output = "",
            Errors = [],
            FixesApplied = allFixesApplied,
            Duration = stopwatch.Elapsed
        };
    }

    public async Task<CommandResult> RunBuildAsync(string containerId, CancellationToken cancellationToken = default) {
        var buildTool = await DetectBuildToolAsync(containerId, cancellationToken);

        if (buildTool == null) {
            _logger.LogWarning("No build tool detected, cannot run build");
            return new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "No build tool detected"
            };
        }

        // Verify tool availability before running build
        var toolAvailable = await VerifyToolAvailabilityAsync(containerId, buildTool, cancellationToken);
        if (!toolAvailable) {
            var installInstructions = GetInstallInstructions(buildTool);
            _logger.LogWarning("Build tool '{BuildTool}' is not available in container. {Instructions}", buildTool, installInstructions);
            return new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = $"Build tool '{buildTool}' is not available. {installInstructions}"
            };
        }

        _logger.LogInformation("Running build with {BuildTool}", buildTool);

        return buildTool switch {
            "dotnet" => await _containerManager.ExecuteInContainerAsync(containerId, "dotnet", new[] { "build" }, cancellationToken),
            "npm" => await _containerManager.ExecuteInContainerAsync(containerId, "npm", new[] { "run", "build" }, cancellationToken),
            "gradle" => await _containerManager.ExecuteInContainerAsync(containerId, "./gradlew", new[] { "build" }, cancellationToken),
            "maven" => await _containerManager.ExecuteInContainerAsync(containerId, "mvn", new[] { "compile" }, cancellationToken),
            "go" => await _containerManager.ExecuteInContainerAsync(containerId, "go", new[] { "build", "./..." }, cancellationToken),
            "cargo" => await _containerManager.ExecuteInContainerAsync(containerId, "cargo", new[] { "build" }, cancellationToken),
            // Defensive code: unreachable since DetectBuildToolAsync only returns supported tools or null
            _ => new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = $"Unsupported build tool: {buildTool}"
            }
        };
    }

    public async Task<string?> DetectBuildToolAsync(string containerId, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Detecting build tool for container {ContainerId}", containerId);

        // Check for .NET
        var dotnetCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "3", "-name", "*.csproj", "-o", "-name", "*.fsproj", "-o", "-name", "*.vbproj" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(dotnetCheck.Output)) {
            _logger.LogInformation("Detected .NET project");
            return "dotnet";
        }

        // Check for npm
        var npmCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "2", "-name", "package.json" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(npmCheck.Output)) {
            _logger.LogInformation("Detected npm project");
            return "npm";
        }

        // Check for Gradle
        var gradleCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "2", "-name", "build.gradle", "-o", "-name", "build.gradle.kts" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(gradleCheck.Output)) {
            _logger.LogInformation("Detected Gradle project");
            return "gradle";
        }

        // Check for Maven
        var mavenCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "2", "-name", "pom.xml" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(mavenCheck.Output)) {
            _logger.LogInformation("Detected Maven project");
            return "maven";
        }

        // Check for Go
        var goCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "2", "-name", "go.mod" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(goCheck.Output)) {
            _logger.LogInformation("Detected Go project");
            return "go";
        }

        // Check for Cargo (Rust)
        var cargoCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-maxdepth", "2", "-name", "Cargo.toml" },
            cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(cargoCheck.Output)) {
            _logger.LogInformation("Detected Cargo (Rust) project");
            return "cargo";
        }

        _logger.LogWarning("No build tool detected");
        return null;
    }

    private async Task<bool> VerifyToolAvailabilityAsync(string containerId, string buildTool, CancellationToken cancellationToken) {
        var toolCommand = buildTool switch {
            "dotnet" => "dotnet",
            "npm" => "npm",
            "gradle" => "gradle",
            "maven" => "mvn",
            "go" => "go",
            "cargo" => "cargo",
            // Defensive code: unreachable since DetectBuildToolAsync only returns supported tools or null
            _ => null
        };
        
        // This null check is unreachable through normal code flow but provides safety if DetectBuildToolAsync
        // is modified in the future to return additional tool names
        if (toolCommand == null) {
            _logger.LogWarning("Unknown build tool '{BuildTool}', cannot verify availability", buildTool);
            return false;
        }
        
        var result = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "which",
            args: new[] { toolCommand },
            cancellationToken: cancellationToken);
        
        return result.Success;
    }

    private static string GetInstallInstructions(string buildTool) {
        return buildTool switch {
            "dotnet" => "Install .NET SDK: https://dotnet.microsoft.com/download",
            "npm" => "Install Node.js and npm: https://nodejs.org/",
            "gradle" => "Install Gradle: https://gradle.org/install/",
            "maven" => "Install Maven: https://maven.apache.org/install.html",
            "go" => "Install Go: https://golang.org/doc/install",
            "cargo" => "Install Rust and Cargo: https://www.rust-lang.org/tools/install",
            _ => "Please install the required build tool"
        };
    }

    public async Task<List<BuildError>> ParseBuildErrorsAsync(string output, string buildTool, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Parsing build errors for {BuildTool}", buildTool);

        var errors = new List<BuildError>();

        switch (buildTool) {
            case "dotnet":
                errors = ParseDotnetErrors(output);
                break;
            case "npm":
                errors = ParseNpmErrors(output);
                break;
            case "gradle":
                errors = ParseGradleErrors(output);
                break;
            case "maven":
                errors = ParseMavenErrors(output);
                break;
            case "go":
                errors = ParseGoErrors(output);
                break;
            case "cargo":
                errors = ParseCargoErrors(output);
                break;
            default:
                _logger.LogWarning("Unsupported build tool for error parsing: {BuildTool}", buildTool);
                break;
        }

        _logger.LogInformation("Parsed {ErrorCount} errors", errors.Count);
        return await Task.FromResult(errors);
    }

    private List<BuildError> ParseDotnetErrors(string output) {
        var errors = new List<BuildError>();
        
        // Pattern: file.cs(line,column): error CS1234: message
        var errorPattern = new Regex(@"^(.+?)\((\d+),\d+\):\s+(error|warning)\s+([A-Z]+\d+):\s+(.+)$", RegexOptions.Multiline);
        var matches = errorPattern.Matches(output);

        foreach (Match match in matches) {
            var severity = match.Groups[3].Value.ToLowerInvariant() == "error" ? ErrorSeverity.Error : ErrorSeverity.Warning;
            var errorCode = match.Groups[4].Value;
            var category = DetermineErrorCategory(errorCode, match.Groups[5].Value);

            errors.Add(new BuildError {
                FilePath = match.Groups[1].Value.Trim(),
                LineNumber = int.Parse(match.Groups[2].Value),
                Severity = severity,
                ErrorCode = errorCode,
                Message = match.Groups[5].Value.Trim(),
                Category = category
            });
        }

        return errors;
    }

    private List<BuildError> ParseNpmErrors(string output) {
        var errors = new List<BuildError>();
        
        // Pattern: ERROR in ./src/file.ts
        // or: file.ts(line,col): error TS1234: message
        var errorPattern1 = new Regex(@"ERROR in (.+?)$", RegexOptions.Multiline);
        var errorPattern2 = new Regex(@"^(.+?)\((\d+),\d+\):\s+error\s+([A-Z]+\d+):\s+(.+)$", RegexOptions.Multiline);

        var matches1 = errorPattern1.Matches(output);
        foreach (Match match in matches1) {
            errors.Add(new BuildError {
                FilePath = match.Groups[1].Value.Trim(),
                Severity = ErrorSeverity.Error,
                ErrorCode = "BUILD_ERROR",
                Message = "Build failed",
                Category = ErrorCategory.Other
            });
        }

        var matches2 = errorPattern2.Matches(output);
        foreach (Match match in matches2) {
            var errorCode = match.Groups[3].Value;
            errors.Add(new BuildError {
                FilePath = match.Groups[1].Value.Trim(),
                LineNumber = int.Parse(match.Groups[2].Value),
                Severity = ErrorSeverity.Error,
                ErrorCode = errorCode,
                Message = match.Groups[4].Value.Trim(),
                Category = DetermineErrorCategory(errorCode, match.Groups[4].Value)
            });
        }

        return errors;
    }

    private List<BuildError> ParseGradleErrors(string output) {
        var errors = new List<BuildError>();
        
        // Pattern: /path/file.java:line: error: message
        var errorPattern = new Regex(@"^(.+?):(\d+):\s+(error|warning):\s+(.+)$", RegexOptions.Multiline);
        var matches = errorPattern.Matches(output);

        foreach (Match match in matches) {
            var severity = match.Groups[3].Value.ToLowerInvariant() == "error" ? ErrorSeverity.Error : ErrorSeverity.Warning;
            errors.Add(new BuildError {
                FilePath = match.Groups[1].Value.Trim(),
                LineNumber = int.Parse(match.Groups[2].Value),
                Severity = severity,
                ErrorCode = "GRADLE_ERROR",
                Message = match.Groups[4].Value.Trim(),
                Category = DetermineErrorCategory("", match.Groups[4].Value)
            });
        }

        return errors;
    }

    private List<BuildError> ParseMavenErrors(string output) {
        var errors = new List<BuildError>();
        
        // Pattern: [ERROR] /path/file.java:[line,column] message
        var errorPattern = new Regex(@"\[(ERROR|WARNING)\]\s+(.+?)\[(\d+),\d+\]\s+(.+)$", RegexOptions.Multiline);
        var matches = errorPattern.Matches(output);

        foreach (Match match in matches) {
            var severity = match.Groups[1].Value == "ERROR" ? ErrorSeverity.Error : ErrorSeverity.Warning;
            errors.Add(new BuildError {
                FilePath = match.Groups[2].Value.Trim(),
                LineNumber = int.Parse(match.Groups[3].Value),
                Severity = severity,
                ErrorCode = "MAVEN_ERROR",
                Message = match.Groups[4].Value.Trim(),
                Category = DetermineErrorCategory("", match.Groups[4].Value)
            });
        }

        return errors;
    }

    private List<BuildError> ParseGoErrors(string output) {
        var errors = new List<BuildError>();
        
        // Pattern: file.go:line:column: message
        var errorPattern = new Regex(@"^(.+?):(\d+):\d+:\s+(.+)$", RegexOptions.Multiline);
        var matches = errorPattern.Matches(output);

        foreach (Match match in matches) {
            errors.Add(new BuildError {
                FilePath = match.Groups[1].Value.Trim(),
                LineNumber = int.Parse(match.Groups[2].Value),
                Severity = ErrorSeverity.Error,
                ErrorCode = "GO_ERROR",
                Message = match.Groups[3].Value.Trim(),
                Category = DetermineErrorCategory("", match.Groups[3].Value)
            });
        }

        return errors;
    }

    private List<BuildError> ParseCargoErrors(string output) {
        var errors = new List<BuildError>();
        
        // Pattern: error[E0123]: message
        //    --> src/file.rs:line:column
        var errorPattern = new Regex(@"error\[([A-Z]\d+)\]:\s+(.+?)[\r\n]+\s+-->\s+(.+?):(\d+):\d+", RegexOptions.Multiline);
        var matches = errorPattern.Matches(output);

        foreach (Match match in matches) {
            var errorCode = match.Groups[1].Value;
            errors.Add(new BuildError {
                ErrorCode = errorCode,
                Message = match.Groups[2].Value.Trim(),
                FilePath = match.Groups[3].Value.Trim(),
                LineNumber = int.Parse(match.Groups[4].Value),
                Severity = ErrorSeverity.Error,
                Category = DetermineErrorCategory(errorCode, match.Groups[2].Value)
            });
        }

        return errors;
    }

    private static ErrorCategory DetermineErrorCategory(string errorCode, string message) {
        var lowerMessage = message.ToLowerInvariant();

        // Check for dependency-related errors
        if (lowerMessage.Contains("package") || lowerMessage.Contains("module") || 
            lowerMessage.Contains("import") || lowerMessage.Contains("dependency") ||
            lowerMessage.Contains("not found") || lowerMessage.Contains("cannot find")) {
            return ErrorCategory.MissingDependency;
        }

        // Check for type errors
        if (lowerMessage.Contains("type") || lowerMessage.Contains("cast") ||
            errorCode.StartsWith("CS1") || errorCode.StartsWith("TS2")) {
            return ErrorCategory.Type;
        }

        // Check for syntax errors
        if (lowerMessage.Contains("syntax") || lowerMessage.Contains("expected") ||
            lowerMessage.Contains("unexpected")) {
            return ErrorCategory.Syntax;
        }

        // Check for configuration errors
        if (lowerMessage.Contains("config") || lowerMessage.Contains("setting")) {
            return ErrorCategory.Configuration;
        }

        return ErrorCategory.Other;
    }

    public async Task<List<CodeFix>> GenerateFixesAsync(List<BuildError> errors, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Generating fixes for {ErrorCount} errors using LLM", errors.Count);

        try {
            var prompt = BuildFixGenerationPrompt(errors);

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(GetFixGenerationSystemPrompt());
            chatHistory.AddUserMessage(prompt);

            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.2,
                MaxTokens = 4000,
                ResponseFormat = "json_object"
            };

            var response = await _chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            var responseContent = response.FirstOrDefault()?.Content ?? "{}";
            _logger.LogDebug("LLM response: {Response}", responseContent);

            var fixes = ParseFixesFromResponse(responseContent);
            _logger.LogInformation("Generated {FixCount} fixes from LLM", fixes.Count);
            return fixes;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating fixes with LLM");
            return [];
        }
    }

    private static string GetFixGenerationSystemPrompt() {
        return """
            You are an expert software developer specializing in debugging and fixing compilation errors.
            Your role is to analyze build errors and generate precise code fixes.

            You must respond with a JSON object that strictly follows this schema:

            {
              "fixes": [
                {
                  "filePath": "path/to/file.cs",
                  "description": "Brief description of what the fix does",
                  "originalCode": "exact code snippet that has the error",
                  "fixedCode": "corrected code snippet",
                  "confidence": "High|Medium|Low"
                }
              ]
            }

            Guidelines:
            - Only generate fixes you are confident about
            - Provide exact code snippets, not approximations
            - Include enough context in code snippets to uniquely identify the location
            - High confidence: syntax errors, missing imports, obvious type mismatches
            - Medium confidence: logic errors that have a clear fix
            - Low confidence: complex errors that may have multiple solutions
            - If you cannot confidently fix an error, omit it from the response
            """;
    }

    private static string BuildFixGenerationPrompt(List<BuildError> errors) {
        var sb = new StringBuilder();
        sb.AppendLine("I have the following build errors that need to be fixed:");
        sb.AppendLine();

        foreach (var error in errors.Take(10)) { // Limit to first 10 errors to avoid token limits
            sb.AppendLine($"Error in {error.FilePath ?? "unknown file"}:{error.LineNumber ?? 0}");
            sb.AppendLine($"  Code: {error.ErrorCode}");
            sb.AppendLine($"  Category: {error.Category}");
            sb.AppendLine($"  Message: {error.Message}");
            sb.AppendLine();
        }

        sb.AppendLine("Please generate fixes for these errors in JSON format.");
        return sb.ToString();
    }

    private List<CodeFix> ParseFixesFromResponse(string jsonResponse) {
        try {
            var options = new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };

            var response = JsonSerializer.Deserialize<FixResponse>(jsonResponse, options);
            return response?.Fixes ?? [];
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error parsing fixes from LLM response");
            return [];
        }
    }

    private sealed class FixResponse {
        public List<CodeFix> Fixes { get; set; } = [];
    }
}
