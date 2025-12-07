using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;

internal sealed class CodeQualityChecker : ICodeQualityChecker {
    private readonly IContainerManager _containerManager;
    private readonly ILogger<CodeQualityChecker> _logger;

    public CodeQualityChecker(IContainerManager containerManager, ILogger<CodeQualityChecker> logger) {
        _containerManager = containerManager;
        _logger = logger;
    }

    public async Task<QualityResult> CheckAndFixAsync(string containerId, CancellationToken cancellationToken = default) {
        var startTime = Stopwatch.StartNew();
        var toolsRun = new List<string>();
        var allIssues = new List<QualityIssue>();
        var fixedCount = 0;

        _logger.LogInformation("Starting code quality check for container {ContainerId}", containerId);

        try {
            // Run formatter first to fix style issues
            var formatResult = await RunFormatterAsync(containerId: containerId, cancellationToken: cancellationToken);
            if (formatResult.Success) {
                toolsRun.Add("formatter");
                fixedCount += formatResult.FilesFormatted;
                _logger.LogInformation("Formatted {Count} files", formatResult.FilesFormatted);
            }

            // Run linter to detect code quality issues
            var lintResult = await RunLinterAsync(containerId: containerId, cancellationToken: cancellationToken);
            if (!string.IsNullOrEmpty(lintResult.Tool)) {
                toolsRun.Add(lintResult.Tool);
                allIssues.AddRange(lintResult.Issues);
            }

            // Auto-fix auto-fixable issues
            var autoFixableIssues = allIssues.Where(i => i.AutoFixable).ToList();
            if (autoFixableIssues.Count > 0) {
                await AutoFixIssuesAsync(containerId: containerId, issues: autoFixableIssues, cancellationToken: cancellationToken);
                
                // Re-run linter to verify fixes
                var postFixLintResult = await RunLinterAsync(containerId: containerId, cancellationToken: cancellationToken);
                var remainingIssues = postFixLintResult.Issues.Count;
                fixedCount += autoFixableIssues.Count - remainingIssues;
                allIssues = postFixLintResult.Issues;
                
                _logger.LogInformation("Auto-fixed {FixedCount} issues, {RemainingCount} issues remain", 
                    fixedCount, remainingIssues);
            }

            // Run static analysis
            var analysisResult = await RunStaticAnalysisAsync(containerId: containerId, cancellationToken: cancellationToken);
            if (analysisResult.Success) {
                toolsRun.Add("static-analysis");
                allIssues.AddRange(analysisResult.Issues);
            }

            var errorCount = allIssues.Count(i => i.Severity == IssueSeverity.Error);
            var warningCount = allIssues.Count(i => i.Severity == IssueSeverity.Warning);
            var success = errorCount == 0;

            startTime.Stop();

            return new QualityResult {
                Success = success,
                Issues = allIssues,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                FixedCount = fixedCount,
                ToolsRun = toolsRun,
                Duration = startTime.Elapsed
            };
        } catch (Exception ex) {
            _logger.LogError(ex, "Error during code quality check");
            startTime.Stop();
            
            return new QualityResult {
                Success = false,
                Issues = allIssues,
                ErrorCount = allIssues.Count(i => i.Severity == IssueSeverity.Error),
                WarningCount = allIssues.Count(i => i.Severity == IssueSeverity.Warning),
                FixedCount = fixedCount,
                ToolsRun = toolsRun,
                Duration = startTime.Elapsed
            };
        }
    }

    public async Task<LintResult> RunLinterAsync(string containerId, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Running linter for container {ContainerId}", containerId);

        // Detect which linter to use
        var linterTool = await DetectLinterAsync(containerId: containerId, cancellationToken: cancellationToken);
        if (string.IsNullOrEmpty(linterTool)) {
            _logger.LogWarning("No linter detected for container {ContainerId}", containerId);
            return new LintResult {
                Success = true,
                Tool = "none",
                Issues = [],
                Output = "No linter detected"
            };
        }

        _logger.LogInformation("Using linter: {Linter}", linterTool);

        CommandResult result;
        switch (linterTool) {
            case "dotnet-format":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "dotnet",
                    args: new[] { "format", "--verify-no-changes", "--verbosity", "diagnostic" },
                    cancellationToken: cancellationToken);
                break;

            case "eslint":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "npx",
                    args: new[] { "eslint", ".", "--format", "json" },
                    cancellationToken: cancellationToken);
                break;

            case "black":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "black",
                    args: new[] { "--check", "--diff", "." },
                    cancellationToken: cancellationToken);
                break;

            case "pylint":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "pylint",
                    args: new[] { "--output-format=json", "." },
                    cancellationToken: cancellationToken);
                break;

            case "golint":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "golangci-lint",
                    args: new[] { "run", "--out-format", "json" },
                    cancellationToken: cancellationToken);
                break;

            default:
                _logger.LogWarning("Unsupported linter: {Linter}", linterTool);
                return new LintResult {
                    Success = false,
                    Tool = linterTool,
                    Issues = [],
                    Output = $"Unsupported linter: {linterTool}"
                };
        }

        var issues = ParseLintIssues(output: result.Output, tool: linterTool);

        return new LintResult {
            Success = result.Success,
            Tool = linterTool,
            Issues = issues,
            Output = result.Output
        };
    }

    public async Task<FormatResult> RunFormatterAsync(string containerId, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Running formatter for container {ContainerId}", containerId);

        // Detect which formatter to use
        var formatterTool = await DetectFormatterAsync(containerId: containerId, cancellationToken: cancellationToken);
        if (string.IsNullOrEmpty(formatterTool)) {
            _logger.LogWarning("No formatter detected for container {ContainerId}", containerId);
            return new FormatResult {
                Success = true,
                FilesFormatted = 0,
                FormattedFiles = []
            };
        }

        _logger.LogInformation("Using formatter: {Formatter}", formatterTool);

        CommandResult result;
        switch (formatterTool) {
            case "dotnet-format":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "dotnet",
                    args: new[] { "format" },
                    cancellationToken: cancellationToken);
                break;

            case "prettier":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "npx",
                    args: new[] { "prettier", "--write", "." },
                    cancellationToken: cancellationToken);
                break;

            case "black":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "black",
                    args: new[] { "." },
                    cancellationToken: cancellationToken);
                break;

            case "gofmt":
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "gofmt",
                    args: new[] { "-w", "." },
                    cancellationToken: cancellationToken);
                break;

            default:
                _logger.LogWarning("Unsupported formatter: {Formatter}", formatterTool);
                return new FormatResult {
                    Success = false,
                    FilesFormatted = 0,
                    FormattedFiles = []
                };
        }

        var formattedFiles = ParseFormattedFiles(output: result.Output, tool: formatterTool);

        return new FormatResult {
            Success = result.Success,
            FilesFormatted = formattedFiles.Count,
            FormattedFiles = formattedFiles
        };
    }

    public async Task<AnalysisResult> RunStaticAnalysisAsync(string containerId, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Running static analysis for container {ContainerId}", containerId);

        // For now, static analysis returns empty results
        // This can be enhanced later with security scanning tools like:
        // - Roslyn analyzers for .NET
        // - SonarQube
        // - Bandit for Python
        // - npm audit for JavaScript

        return new AnalysisResult {
            Success = true,
            Issues = [],
            Output = "Static analysis not yet implemented"
        };
    }

    public async Task AutoFixIssuesAsync(string containerId, List<QualityIssue> issues, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Auto-fixing {Count} issues in container {ContainerId}", issues.Count, containerId);

        foreach (var issue in issues) {
            if (!issue.AutoFixable || string.IsNullOrEmpty(issue.SuggestedFix)) {
                continue;
            }

            try {
                // Read the file
                var content = await _containerManager.ReadFileInContainerAsync(
                    containerId: containerId,
                    filePath: issue.FilePath,
                    cancellationToken: cancellationToken);

                // Apply the suggested fix
                // This is a simple implementation - in practice, you'd need more sophisticated parsing
                var lines = content.Split('\n');
                if (issue.LineNumber > 0 && issue.LineNumber <= lines.Length) {
                    lines[issue.LineNumber - 1] = issue.SuggestedFix;
                    var fixedContent = string.Join('\n', lines);

                    // Write back the fixed file
                    await _containerManager.WriteFileInContainerAsync(
                        containerId: containerId,
                        filePath: issue.FilePath,
                        content: fixedContent,
                        cancellationToken: cancellationToken);

                    _logger.LogDebug("Fixed issue {RuleId} in {FilePath} at line {LineNumber}", 
                        issue.RuleId, issue.FilePath, issue.LineNumber);
                }
            } catch (Exception ex) {
                _logger.LogWarning(ex, "Failed to auto-fix issue {RuleId} in {FilePath}", 
                    issue.RuleId, issue.FilePath);
            }
        }
    }

    private async Task<string> DetectLinterAsync(string containerId, CancellationToken cancellationToken = default) {
        // Check for .NET project
        var dotnetCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-name", "*.csproj", "-o", "-name", "*.fsproj", "-o", "-name", "*.vbproj" },
            cancellationToken: cancellationToken);
        if (dotnetCheck.Success && !string.IsNullOrWhiteSpace(dotnetCheck.Output)) {
            return "dotnet-format";
        }

        // Check for JavaScript/TypeScript project with ESLint
        var eslintCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "test",
            args: new[] { "-f", ".eslintrc.js", "-o", "-f", ".eslintrc.json", "-o", "-f", ".eslintrc.yml" },
            cancellationToken: cancellationToken);
        if (eslintCheck.Success) {
            return "eslint";
        }

        // Check for Python project with pylint
        var pylintCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "which",
            args: new[] { "pylint" },
            cancellationToken: cancellationToken);
        if (pylintCheck.Success) {
            return "pylint";
        }

        // Check for Python project with black
        var blackCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "which",
            args: new[] { "black" },
            cancellationToken: cancellationToken);
        if (blackCheck.Success) {
            return "black";
        }

        // Check for Go project
        var goCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-name", "go.mod" },
            cancellationToken: cancellationToken);
        if (goCheck.Success && !string.IsNullOrWhiteSpace(goCheck.Output)) {
            return "golint";
        }

        return "";
    }

    private async Task<string> DetectFormatterAsync(string containerId, CancellationToken cancellationToken = default) {
        // Check for .NET project
        var dotnetCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-name", "*.csproj", "-o", "-name", "*.fsproj", "-o", "-name", "*.vbproj" },
            cancellationToken: cancellationToken);
        if (dotnetCheck.Success && !string.IsNullOrWhiteSpace(dotnetCheck.Output)) {
            return "dotnet-format";
        }

        // Check for JavaScript/TypeScript project with Prettier
        var prettierCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "test",
            args: new[] { "-f", ".prettierrc", "-o", "-f", ".prettierrc.json", "-o", "-f", ".prettierrc.js" },
            cancellationToken: cancellationToken);
        if (prettierCheck.Success) {
            return "prettier";
        }

        // Check for Python project with black
        var blackCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "which",
            args: new[] { "black" },
            cancellationToken: cancellationToken);
        if (blackCheck.Success) {
            return "black";
        }

        // Check for Go project
        var goCheck = await _containerManager.ExecuteInContainerAsync(
            containerId: containerId,
            command: "find",
            args: new[] { ".", "-name", "go.mod" },
            cancellationToken: cancellationToken);
        if (goCheck.Success && !string.IsNullOrWhiteSpace(goCheck.Output)) {
            return "gofmt";
        }

        return "";
    }

    private List<QualityIssue> ParseLintIssues(string output, string tool) {
        var issues = new List<QualityIssue>();

        try {
            switch (tool) {
                case "dotnet-format":
                    issues.AddRange(ParseDotnetFormatOutput(output: output));
                    break;

                case "eslint":
                    issues.AddRange(ParseEslintOutput(output: output));
                    break;

                case "black":
                    issues.AddRange(ParseBlackOutput(output: output));
                    break;

                case "pylint":
                    issues.AddRange(ParsePylintOutput(output: output));
                    break;

                case "golint":
                    issues.AddRange(ParseGolintOutput(output: output));
                    break;
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to parse linter output for {Tool}", tool);
        }

        return issues;
    }

    private List<QualityIssue> ParseDotnetFormatOutput(string output) {
        var issues = new List<QualityIssue>();
        
        // dotnet format output typically looks like:
        // "  Formatted file.cs (1 errors)"
        // For simplicity, we'll extract file names and report as style issues
        var filePattern = new Regex(@"Formatted\s+(.+?)\s+\(", RegexOptions.Multiline);
        var matches = filePattern.Matches(output);
        
        foreach (Match match in matches) {
            if (match.Groups.Count > 1) {
                issues.Add(new QualityIssue {
                    RuleId = "format",
                    Message = "File requires formatting",
                    FilePath = match.Groups[1].Value.Trim(),
                    LineNumber = 0,
                    Severity = IssueSeverity.Warning,
                    Category = IssueCategory.Style,
                    AutoFixable = true,
                    SuggestedFix = null
                });
            }
        }

        return issues;
    }

    private List<QualityIssue> ParseEslintOutput(string output) {
        var issues = new List<QualityIssue>();

        try {
            // ESLint JSON output format
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array) {
                foreach (var file in root.EnumerateArray()) {
                    var filePath = file.GetProperty("filePath").GetString() ?? "";
                    var messages = file.GetProperty("messages");

                    foreach (var message in messages.EnumerateArray()) {
                        var ruleId = message.TryGetProperty("ruleId", out var ruleIdProp) 
                            ? ruleIdProp.GetString() ?? "" 
                            : "";
                        var messageText = message.GetProperty("message").GetString() ?? "";
                        var line = message.GetProperty("line").GetInt32();
                        var severityValue = message.GetProperty("severity").GetInt32();
                        var fix = message.TryGetProperty("fix", out var fixProp);

                        issues.Add(new QualityIssue {
                            RuleId = ruleId,
                            Message = messageText,
                            FilePath = filePath,
                            LineNumber = line,
                            Severity = severityValue == 2 ? IssueSeverity.Error : IssueSeverity.Warning,
                            Category = IssueCategory.Style,
                            AutoFixable = fix,
                            SuggestedFix = null
                        });
                    }
                }
            }
        } catch {
            // If JSON parsing fails, return empty list
        }

        return issues;
    }

    private List<QualityIssue> ParseBlackOutput(string output) {
        var issues = new List<QualityIssue>();

        // Black output in check mode shows files that would be reformatted
        var filePattern = new Regex(@"would reformat\s+(.+)", RegexOptions.Multiline);
        var matches = filePattern.Matches(output);

        foreach (Match match in matches) {
            if (match.Groups.Count > 1) {
                issues.Add(new QualityIssue {
                    RuleId = "format",
                    Message = "File would be reformatted by Black",
                    FilePath = match.Groups[1].Value.Trim(),
                    LineNumber = 0,
                    Severity = IssueSeverity.Warning,
                    Category = IssueCategory.Style,
                    AutoFixable = true,
                    SuggestedFix = null
                });
            }
        }

        return issues;
    }

    private List<QualityIssue> ParsePylintOutput(string output) {
        var issues = new List<QualityIssue>();

        try {
            // Pylint JSON output format
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array) {
                foreach (var issue in root.EnumerateArray()) {
                    var type = issue.GetProperty("type").GetString() ?? "";
                    var module = issue.GetProperty("module").GetString() ?? "";
                    var obj = issue.TryGetProperty("obj", out var objProp) ? objProp.GetString() ?? "" : "";
                    var line = issue.GetProperty("line").GetInt32();
                    var messageId = issue.GetProperty("message-id").GetString() ?? "";
                    var message = issue.GetProperty("message").GetString() ?? "";
                    var path = issue.GetProperty("path").GetString() ?? "";

                    var severity = type switch {
                        "error" => IssueSeverity.Error,
                        "warning" => IssueSeverity.Warning,
                        "convention" => IssueSeverity.Info,
                        "refactor" => IssueSeverity.Suggestion,
                        _ => IssueSeverity.Warning
                    };

                    issues.Add(new QualityIssue {
                        RuleId = messageId,
                        Message = message,
                        FilePath = path,
                        LineNumber = line,
                        Severity = severity,
                        Category = IssueCategory.Style,
                        AutoFixable = false,
                        SuggestedFix = null
                    });
                }
            }
        } catch {
            // If JSON parsing fails, return empty list
        }

        return issues;
    }

    private List<QualityIssue> ParseGolintOutput(string output) {
        var issues = new List<QualityIssue>();

        try {
            // golangci-lint JSON output format
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (root.TryGetProperty("Issues", out var issuesArray) && issuesArray.ValueKind == JsonValueKind.Array) {
                foreach (var issue in issuesArray.EnumerateArray()) {
                    var fromLinter = issue.GetProperty("FromLinter").GetString() ?? "";
                    var text = issue.GetProperty("Text").GetString() ?? "";
                    var pos = issue.GetProperty("Pos");
                    var filename = pos.GetProperty("Filename").GetString() ?? "";
                    var line = pos.GetProperty("Line").GetInt32();
                    var severity = issue.TryGetProperty("Severity", out var severityProp) 
                        ? severityProp.GetString() ?? "" 
                        : "";

                    var issueSeverity = severity.ToLowerInvariant() switch {
                        "error" => IssueSeverity.Error,
                        "warning" => IssueSeverity.Warning,
                        _ => IssueSeverity.Info
                    };

                    issues.Add(new QualityIssue {
                        RuleId = fromLinter,
                        Message = text,
                        FilePath = filename,
                        LineNumber = line,
                        Severity = issueSeverity,
                        Category = IssueCategory.Style,
                        AutoFixable = false,
                        SuggestedFix = null
                    });
                }
            }
        } catch {
            // If JSON parsing fails, return empty list
        }

        return issues;
    }

    private List<string> ParseFormattedFiles(string output, string tool) {
        var files = new List<string>();

        try {
            switch (tool) {
                case "dotnet-format":
                    // Parse dotnet format output
                    var dotnetPattern = new Regex(@"Formatted\s+(.+?)\s+\(", RegexOptions.Multiline);
                    var dotnetMatches = dotnetPattern.Matches(output);
                    files.AddRange(dotnetMatches.Select(m => m.Groups[1].Value.Trim()));
                    break;

                case "prettier":
                    // Parse prettier output - it lists files it formatted
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    files.AddRange(lines.Where(l => !l.StartsWith("Checking") && !l.StartsWith("Code style issues")));
                    break;

                case "black":
                    // Parse black output - it shows "reformatted <file>"
                    var blackPattern = new Regex(@"reformatted\s+(.+)", RegexOptions.Multiline);
                    var blackMatches = blackPattern.Matches(output);
                    files.AddRange(blackMatches.Select(m => m.Groups[1].Value.Trim()));
                    break;

                case "gofmt":
                    // gofmt doesn't output formatted files by default
                    // We would need to track which files were modified
                    break;
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "Failed to parse formatted files for {Tool}", tool);
        }

        return files;
    }
}
