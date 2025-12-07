using Microsoft.Extensions.Logging;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class CodeQualityCheckerTests {
    [Fact]
    public async Task CheckAndFixAsync_WithDotNetProject_RunsFormatterAndLinter() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupDotNetProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.CheckAndFixAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.ToolsRun.ShouldContain("formatter");
        result.ToolsRun.ShouldContain("dotnet-format");
        result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CheckAndFixAsync_WithLintErrors_ReturnsIssues() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupEslintProjectWithErrors();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.CheckAndFixAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Issues.ShouldNotBeEmpty();
        result.ToolsRun.ShouldContain("eslint");
    }

    [Fact]
    public async Task RunLinterAsync_WithNoDotNetProject_ReturnsNone() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Tool.ShouldBe("none");
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunLinterAsync_WithDotNetProject_UsesDotNetFormat() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupDotNetProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Tool.ShouldBe("dotnet-format");
    }

    [Fact]
    public async Task RunLinterAsync_WithEslintProject_UsesEslint() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupEslintProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Tool.ShouldBe("eslint");
    }

    [Fact]
    public async Task RunLinterAsync_WithPylintProject_UsesPylint() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupPylintProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Tool.ShouldBe("pylint");
    }

    [Fact]
    public async Task RunLinterAsync_WithBlackProject_UsesBlack() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupBlackProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Tool.ShouldBe("black");
    }

    [Fact]
    public async Task RunLinterAsync_WithGoProject_UsesGolint() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupGoProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Tool.ShouldBe("golint");
    }

    [Fact]
    public async Task RunLinterAsync_WithEslintErrors_ParsesIssues() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupEslintProjectWithErrors();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Issues.ShouldNotBeEmpty();
        result.Issues[0].RuleId.ShouldBe("no-unused-vars");
        result.Issues[0].Severity.ShouldBe(IssueSeverity.Error);
        result.Issues[0].FilePath.ShouldContain("app.js");
        result.Issues[0].LineNumber.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RunLinterAsync_WithPylintErrors_ParsesIssues() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupPylintProjectWithErrors();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunLinterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Issues.ShouldNotBeEmpty();
        result.Issues[0].RuleId.ShouldBe("C0103");
        result.Issues[0].Severity.ShouldBe(IssueSeverity.Info);
        result.Issues[0].FilePath.ShouldContain("main.py");
    }

    [Fact]
    public async Task RunFormatterAsync_WithNoDetectedFormatter_ReturnsSuccess() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunFormatterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.FilesFormatted.ShouldBe(0);
    }

    [Fact]
    public async Task RunFormatterAsync_WithDotNetProject_FormatsFiles() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupDotNetProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunFormatterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task RunFormatterAsync_WithPrettierProject_UsesPrettier() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupPrettierProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunFormatterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task RunFormatterAsync_WithBlackProject_UsesBlack() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupBlackProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunFormatterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task RunFormatterAsync_WithGoProject_UsesGofmt() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupGoProject();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunFormatterAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task RunStaticAnalysisAsync_ReturnsSuccess() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.RunStaticAnalysisAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeTrue();
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public async Task AutoFixIssuesAsync_WithAutoFixableIssues_AppliesFixes() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetFileContent(filePath: "test.js", content: "const x = 1;\nconst y = 2;");
        var checker = CreateChecker(containerManager: containerManager);

        var issues = new List<QualityIssue> {
            new QualityIssue {
                RuleId = "semi",
                Message = "Missing semicolon",
                FilePath = "test.js",
                LineNumber = 2,
                Severity = IssueSeverity.Warning,
                Category = IssueCategory.Style,
                AutoFixable = true,
                SuggestedFix = "const y = 2;"
            }
        };

        // Act
        await checker.AutoFixIssuesAsync(containerId: "test-container", issues: issues);

        // Assert
        containerManager.WrittenFiles.ShouldContainKey("test.js");
        containerManager.WrittenFiles["test.js"].ShouldContain("const y = 2;");
    }

    [Fact]
    public async Task AutoFixIssuesAsync_WithNonAutoFixableIssues_DoesNotModifyFile() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetFileContent(filePath: "test.js", content: "const x = 1;");
        var checker = CreateChecker(containerManager: containerManager);

        var issues = new List<QualityIssue> {
            new QualityIssue {
                RuleId = "complexity",
                Message = "Function is too complex",
                FilePath = "test.js",
                LineNumber = 1,
                Severity = IssueSeverity.Warning,
                Category = IssueCategory.Maintainability,
                AutoFixable = false,
                SuggestedFix = null
            }
        };

        // Act
        await checker.AutoFixIssuesAsync(containerId: "test-container", issues: issues);

        // Assert
        containerManager.WrittenFiles.ShouldNotContainKey("test.js");
    }

    [Fact]
    public async Task AutoFixIssuesAsync_WithFileReadError_ContinuesWithoutThrowing() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetReadFileError(filePath: "test.js");
        var checker = CreateChecker(containerManager: containerManager);

        var issues = new List<QualityIssue> {
            new QualityIssue {
                RuleId = "semi",
                Message = "Missing semicolon",
                FilePath = "test.js",
                LineNumber = 1,
                Severity = IssueSeverity.Warning,
                Category = IssueCategory.Style,
                AutoFixable = true,
                SuggestedFix = "const x = 1;"
            }
        };

        // Act & Assert - should not throw
        await checker.AutoFixIssuesAsync(containerId: "test-container", issues: issues);
    }

    [Fact]
    public async Task CheckAndFixAsync_WithException_ReturnsFailureResult() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetExecuteCommandError();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.CheckAndFixAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task CheckAndFixAsync_WithAutoFixableIssues_IncrementsFixedCount() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupDotNetProjectWithAutoFixableIssues();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.CheckAndFixAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.FixedCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CheckAndFixAsync_WithMixedSeverities_CountsCorrectly() {
        // Arrange
        var containerManager = new TestContainerManagerForQualityCheck();
        containerManager.SetupEslintProjectWithMixedSeverities();
        var checker = CreateChecker(containerManager: containerManager);

        // Act
        var result = await checker.CheckAndFixAsync(containerId: "test-container");

        // Assert
        result.ShouldNotBeNull();
        result.ErrorCount.ShouldBe(1);
        result.WarningCount.ShouldBe(1);
        result.Issues.Count.ShouldBe(2);
    }

    private static ICodeQualityChecker CreateChecker(IContainerManager containerManager) {
        var logger = new TestLogger<RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration.CodeQualityChecker>();
        return new RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration.CodeQualityChecker(containerManager: containerManager, logger: logger);
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    private class TestContainerManagerForQualityCheck : IContainerManager {
        private readonly Dictionary<string, string> _fileContents = new();
        private readonly HashSet<string> _readErrorFiles = new();
        private string _projectType = "none";
        private string _lintOutput = "";
        private bool _throwExecuteError = false;
        private bool _persistLintOutputAfterFix = false;
        private int _linterCallCount = 0;

        public Dictionary<string, string> WrittenFiles { get; } = new();

        public void SetupDotNetProject() {
            _projectType = "dotnet";
        }

        public void SetupDotNetProjectWithLintErrors() {
            _projectType = "dotnet";
            _lintOutput = "Formatted Program.cs (1 errors)";
        }

        public void SetupDotNetProjectWithAutoFixableIssues() {
            _projectType = "dotnet";
            _lintOutput = "Formatted Program.cs (1 errors)";
        }

        public void SetupEslintProject() {
            _projectType = "eslint";
        }

        public void SetupEslintProjectWithErrors() {
            _projectType = "eslint";
            _lintOutput = """
                [
                    {
                        "filePath": "/workspace/app.js",
                        "messages": [
                            {
                                "ruleId": "no-unused-vars",
                                "message": "Variable 'x' is defined but never used",
                                "line": 5,
                                "severity": 2
                            }
                        ]
                    }
                ]
                """;
            // Set lintOutput to persist after auto-fix attempts (not all issues are auto-fixable)
            _persistLintOutputAfterFix = true;
        }

        public void SetupEslintProjectWithMixedSeverities() {
            _projectType = "eslint";
            _lintOutput = """
                [
                    {
                        "filePath": "/workspace/app.js",
                        "messages": [
                            {
                                "ruleId": "no-unused-vars",
                                "message": "Variable 'x' is defined but never used",
                                "line": 5,
                                "severity": 2
                            },
                            {
                                "ruleId": "semi",
                                "message": "Missing semicolon",
                                "line": 10,
                                "severity": 1
                            }
                        ]
                    }
                ]
                """;
            _persistLintOutputAfterFix = true;
        }

        public void SetupPylintProject() {
            _projectType = "pylint";
        }

        public void SetupPylintProjectWithErrors() {
            _projectType = "pylint";
            _lintOutput = """
                [
                    {
                        "type": "convention",
                        "module": "main",
                        "obj": "",
                        "line": 3,
                        "message-id": "C0103",
                        "message": "Constant name doesn't conform to UPPER_CASE naming style",
                        "path": "main.py"
                    }
                ]
                """;
        }

        public void SetupBlackProject() {
            _projectType = "black";
        }

        public void SetupGoProject() {
            _projectType = "go";
        }

        public void SetupPrettierProject() {
            _projectType = "prettier";
        }

        public void SetFileContent(string filePath, string content) {
            _fileContents[filePath] = content;
        }

        public void SetReadFileError(string filePath) {
            _readErrorFiles.Add(filePath);
        }

        public void SetExecuteCommandError() {
            _throwExecuteError = true;
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container-id");
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, ContainerImageType imageType, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container-id");
        }

        public Task<CommandResult> ExecuteInContainerAsync(string containerId, string command, string[] args, CancellationToken cancellationToken = default) {
            if (_throwExecuteError) {
                throw new InvalidOperationException("Simulated execution error");
            }

            // Handle find commands for project detection
            if (command == "find" && args.Contains("*.csproj")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = _projectType == "dotnet" ? 0 : 1,
                    Output = _projectType == "dotnet" ? "Project.csproj" : "",
                    Error = ""
                });
            }

            if (command == "find" && args.Contains("go.mod")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = _projectType == "go" ? 0 : 1,
                    Output = _projectType == "go" ? "go.mod" : "",
                    Error = ""
                });
            }

            if (command == "test" && args.Any(a => a.Contains(".eslintrc"))) {
                return Task.FromResult(new CommandResult {
                    ExitCode = _projectType == "eslint" ? 0 : 1,
                    Output = "",
                    Error = ""
                });
            }

            if (command == "test" && args.Any(a => a.Contains(".prettierrc"))) {
                return Task.FromResult(new CommandResult {
                    ExitCode = _projectType == "prettier" ? 0 : 1,
                    Output = "",
                    Error = ""
                });
            }

            if (command == "which" && args.Contains("pylint")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = _projectType == "pylint" ? 0 : 1,
                    Output = _projectType == "pylint" ? "/usr/bin/pylint" : "",
                    Error = ""
                });
            }

            if (command == "which" && args.Contains("black")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = _projectType == "black" ? 0 : 1,
                    Output = _projectType == "black" ? "/usr/bin/black" : "",
                    Error = ""
                });
            }

            // Handle linter/formatter commands
            if (command == "dotnet" && args.Contains("format")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = _lintOutput,
                    Error = ""
                });
            }

            if (command == "npx" && args.Contains("eslint")) {
                _linterCallCount++;
                // After first call (auto-fix), return empty if not persisting
                var output = (_linterCallCount > 1 && !_persistLintOutputAfterFix) ? "[]" : _lintOutput;
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = output,
                    Error = ""
                });
            }

            if (command == "npx" && args.Contains("prettier")) {
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = "",
                    Error = ""
                });
            }

            if (command == "black") {
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = _lintOutput,
                    Error = ""
                });
            }

            if (command == "pylint") {
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = _lintOutput,
                    Error = ""
                });
            }

            if (command == "golangci-lint") {
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = _lintOutput,
                    Error = ""
                });
            }

            if (command == "gofmt") {
                return Task.FromResult(new CommandResult {
                    ExitCode = 0,
                    Output = "",
                    Error = ""
                });
            }

            return Task.FromResult(new CommandResult {
                ExitCode = 0,
                Output = "",
                Error = ""
            });
        }

        public Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
            if (_readErrorFiles.Contains(filePath)) {
                throw new InvalidOperationException($"Failed to read file {filePath}");
            }

            return Task.FromResult(_fileContents.GetValueOrDefault(key: filePath, defaultValue: ""));
        }

        public Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
            WrittenFiles[filePath] = content;
            return Task.CompletedTask;
        }

        public Task CommitAndPushAsync(string containerId, string commitMessage, string owner, string repo, string branch, string token, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CreateDirectoryAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<bool> DirectoryExistsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(true);
        }

        public Task MoveAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CopyAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string containerId, string path, bool recursive = false, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<List<string>> ListContentsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<string>());
        }

        public Task<BuildToolsStatus> VerifyBuildToolsAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.FromResult(new BuildToolsStatus {
                DotnetAvailable = true,
                NpmAvailable = true,
                GradleAvailable = true,
                MavenAvailable = true,
                GoAvailable = true,
                CargoAvailable = true,
                MissingTools = []
            });
        }
    }
}
