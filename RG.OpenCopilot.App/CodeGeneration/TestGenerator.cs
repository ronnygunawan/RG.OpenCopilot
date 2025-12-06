using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace RG.OpenCopilot.App.CodeGeneration;

public sealed class TestGenerator : ITestGenerator {
    private readonly IContainerManager _containerManager;
    private readonly IFileAnalyzer _fileAnalyzer;
    private readonly Kernel _kernel;
    private readonly ILogger<TestGenerator> _logger;
    private readonly IChatCompletionService _chatService;

    public TestGenerator(
        IContainerManager containerManager,
        IFileAnalyzer fileAnalyzer,
        Kernel kernel,
        ILogger<TestGenerator> logger) {
        _containerManager = containerManager;
        _fileAnalyzer = fileAnalyzer;
        _kernel = kernel;
        _logger = logger;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<string> GenerateTestsAsync(
        string containerId,
        string codeFilePath,
        string codeContent,
        string? testFramework = null,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Generating tests for {CodeFilePath} in container {ContainerId}", codeFilePath, containerId);

        try {
            // Detect test framework if not provided
            var framework = testFramework ?? await DetectTestFrameworkAsync(containerId, cancellationToken);
            if (string.IsNullOrEmpty(framework)) {
                _logger.LogWarning("Could not detect test framework for container {ContainerId}", containerId);
                framework = "xUnit"; // Default to xUnit for .NET
            }

            // Find existing tests to learn patterns
            var existingTests = await FindExistingTestsAsync(containerId, cancellationToken);
            var pattern = await AnalyzeTestPatternAsync(existingTests, cancellationToken);

            // Build prompt for test generation
            var prompt = BuildTestPrompt(codeFilePath, codeContent, framework, pattern);
            var systemPrompt = BuildTestSystemPrompt(framework);

            // Generate tests using LLM
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

            var generatedTests = ExtractCode(response.Content ?? "");

            _logger.LogInformation(
                "Generated {LineCount} lines of test code for {CodeFilePath}",
                generatedTests.Split('\n').Length,
                codeFilePath);

            return generatedTests;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error generating tests for {CodeFilePath}", codeFilePath);
            throw;
        }
    }

    public async Task<string?> DetectTestFrameworkAsync(
        string containerId,
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Detecting test framework in container {ContainerId}", containerId);

        try {
            // Look for common test framework indicators in project files
            var frameworks = new Dictionary<string, string[]> {
                ["xUnit"] = new[] { "xunit", "xunit.core" },
                ["NUnit"] = new[] { "nunit", "nunit.framework" },
                ["MSTest"] = new[] { "mstest", "microsoft.visualstudio.testtools" },
                ["Jest"] = new[] { "jest", "\"jest\":" },
                ["Mocha"] = new[] { "mocha", "\"mocha\":" },
                ["pytest"] = new[] { "pytest", "import pytest" },
                ["unittest"] = new[] { "unittest", "import unittest" }
            };

            // Check for .NET project files
            var csprojFiles = await _fileAnalyzer.ListFilesAsync(containerId, pattern: "*.csproj", cancellationToken);
            foreach (var csprojFile in csprojFiles) {
                var content = await _containerManager.ReadFileInContainerAsync(containerId, csprojFile, cancellationToken);
                foreach (var (framework, patterns) in frameworks) {
                    if (patterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase))) {
                        _logger.LogInformation("Detected test framework: {Framework}", framework);
                        return framework;
                    }
                }
            }

            // Check for Node.js package.json
            var packageJsonFiles = await _fileAnalyzer.ListFilesAsync(containerId, pattern: "package.json", cancellationToken);
            foreach (var packageFile in packageJsonFiles) {
                var content = await _containerManager.ReadFileInContainerAsync(containerId, packageFile, cancellationToken);
                foreach (var (framework, patterns) in frameworks) {
                    if (patterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase))) {
                        _logger.LogInformation("Detected test framework: {Framework}", framework);
                        return framework;
                    }
                }
            }

            // Check for Python requirements files
            var requirementFiles = await _fileAnalyzer.ListFilesAsync(containerId, pattern: "requirements*.txt", cancellationToken);
            foreach (var reqFile in requirementFiles) {
                var content = await _containerManager.ReadFileInContainerAsync(containerId, reqFile, cancellationToken);
                foreach (var (framework, patterns) in frameworks) {
                    if (patterns.Any(p => content.Contains(p, StringComparison.OrdinalIgnoreCase))) {
                        _logger.LogInformation("Detected test framework: {Framework}", framework);
                        return framework;
                    }
                }
            }

            _logger.LogDebug("No test framework detected in container {ContainerId}", containerId);
            return null;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Error detecting test framework in container {ContainerId}", containerId);
            return null;
        }
    }

    public async Task<List<TestFile>> FindExistingTestsAsync(
        string containerId,
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Finding existing tests in container {ContainerId}", containerId);

        var testFiles = new List<TestFile>();

        try {
            // Common test file patterns
            var patterns = new[] {
                "*Tests.cs",
                "*Test.cs",
                "*.test.js",
                "*.spec.js",
                "*.test.ts",
                "*.spec.ts",
                "test_*.py",
                "*_test.py"
            };

            foreach (var pattern in patterns) {
                var files = await _fileAnalyzer.ListFilesAsync(containerId, pattern, cancellationToken);
                
                // Limit to first 5 test files to avoid overwhelming the LLM
                foreach (var file in files.Take(5)) {
                    try {
                        var content = await _containerManager.ReadFileInContainerAsync(containerId, file, cancellationToken);
                        var framework = DetectFrameworkFromContent(content);
                        
                        testFiles.Add(new TestFile {
                            Path = file,
                            Content = content,
                            Framework = framework
                        });
                    }
                    catch (Exception ex) {
                        _logger.LogWarning(ex, "Error reading test file {FilePath}", file);
                    }
                }

                if (testFiles.Count >= 5) {
                    break; // We have enough examples
                }
            }

            _logger.LogInformation("Found {Count} existing test files in container {ContainerId}", testFiles.Count, containerId);
            return testFiles;
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Error finding existing tests in container {ContainerId}", containerId);
            return testFiles;
        }
    }

    public async Task<TestPattern> AnalyzeTestPatternAsync(
        List<TestFile> existingTests,
        CancellationToken cancellationToken = default) {
        _logger.LogDebug("Analyzing test patterns from {Count} test files", existingTests.Count);

        if (existingTests.Count == 0) {
            _logger.LogDebug("No existing tests to analyze, returning default pattern");
            return new TestPattern {
                NamingConvention = "MethodName_Scenario_ExpectedOutcome",
                AssertionStyle = "Shouldly",
                UsesArrangeActAssert = true,
                CommonImports = new List<string>(),
                BaseTestClass = ""
            };
        }

        try {
            // Use LLM to analyze patterns if we have the service
            var prompt = BuildPatternAnalysisPrompt(existingTests);
            var systemPrompt = "You are an expert at analyzing test code patterns and conventions. Analyze the provided test files and extract common patterns.";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(prompt);

            var executionSettings = new OpenAIPromptExecutionSettings {
                Temperature = 0.1,
                MaxTokens = 1000
            };

            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancellationToken);

            var analysisResult = response.Content ?? "";
            
            // Parse the analysis result
            return ParsePatternAnalysis(analysisResult, existingTests);
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Error analyzing test patterns, using heuristics");
            return AnalyzePatternHeuristically(existingTests);
        }
    }

    public async Task<TestResult> RunTestsAsync(
        string containerId,
        string testFilePath,
        CancellationToken cancellationToken = default) {
        _logger.LogInformation("Running tests in {TestFilePath} in container {ContainerId}", testFilePath, containerId);

        try {
            var fileExtension = Path.GetExtension(testFilePath).ToLowerInvariant();
            CommandResult result;

            // Determine test command based on file type
            if (fileExtension == ".cs") {
                // .NET tests
                var projectDir = Path.GetDirectoryName(testFilePath) ?? ".";
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "dotnet",
                    args: new[] { "test", projectDir, "--verbosity", "normal" },
                    cancellationToken: cancellationToken);
            }
            else if (fileExtension == ".js" || fileExtension == ".ts") {
                // JavaScript/TypeScript tests (Jest or Mocha)
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "npm",
                    args: new[] { "test", "--", testFilePath },
                    cancellationToken: cancellationToken);
            }
            else if (fileExtension == ".py") {
                // Python tests
                result = await _containerManager.ExecuteInContainerAsync(
                    containerId: containerId,
                    command: "python",
                    args: new[] { "-m", "pytest", testFilePath, "-v" },
                    cancellationToken: cancellationToken);
            }
            else {
                _logger.LogWarning("Unknown test file extension: {Extension}", fileExtension);
                return new TestResult {
                    Success = false,
                    Output = $"Unknown test file extension: {fileExtension}"
                };
            }

            return ParseTestResult(result);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error running tests in {TestFilePath}", testFilePath);
            return new TestResult {
                Success = false,
                Output = ex.Message
            };
        }
    }

    private string BuildTestPrompt(string codeFilePath, string codeContent, string framework, TestPattern pattern) {
        var prompt = new StringBuilder();

        prompt.AppendLine($"# Task: Generate Unit Tests Using {framework}");
        prompt.AppendLine();
        prompt.AppendLine("## Code to Test");
        prompt.AppendLine($"**File:** {codeFilePath}");
        prompt.AppendLine();
        prompt.AppendLine("```");
        prompt.AppendLine(codeContent);
        prompt.AppendLine("```");
        prompt.AppendLine();

        prompt.AppendLine("## Testing Patterns to Follow");
        prompt.AppendLine($"- **Naming Convention:** {pattern.NamingConvention}");
        prompt.AppendLine($"- **Assertion Style:** {pattern.AssertionStyle}");
        prompt.AppendLine($"- **Arrange-Act-Assert:** {(pattern.UsesArrangeActAssert ? "Yes" : "No")}");
        
        if (pattern.CommonImports.Count > 0) {
            prompt.AppendLine("- **Common Imports:**");
            foreach (var import in pattern.CommonImports) {
                prompt.AppendLine($"  - {import}");
            }
        }

        if (!string.IsNullOrEmpty(pattern.BaseTestClass)) {
            prompt.AppendLine($"- **Base Test Class:** {pattern.BaseTestClass}");
        }

        prompt.AppendLine();
        prompt.AppendLine("## Requirements");
        prompt.AppendLine("- Generate comprehensive unit tests that cover:");
        prompt.AppendLine("  - All public methods and properties");
        prompt.AppendLine("  - Happy path scenarios");
        prompt.AppendLine("  - Edge cases (null inputs, empty collections, boundary values)");
        prompt.AppendLine("  - Error conditions and exceptions");
        prompt.AppendLine("- Use meaningful test names that describe the scenario and expected outcome");
        prompt.AppendLine("- Include proper setup and teardown if needed");
        prompt.AppendLine("- Add comments for complex test scenarios");
        prompt.AppendLine("- Ensure tests are independent and can run in any order");
        prompt.AppendLine();
        prompt.AppendLine("Generate the complete test file content:");

        return prompt.ToString();
    }

    private string BuildTestSystemPrompt(string framework) {
        var prompt = new StringBuilder();

        prompt.AppendLine("You are an expert software testing engineer specializing in automated test generation.");
        prompt.AppendLine();
        prompt.AppendLine($"Your task is to generate high-quality unit tests using {framework}.");
        prompt.AppendLine();

        prompt.AppendLine("# Best Practices");
        prompt.AppendLine("- Follow the AAA (Arrange-Act-Assert) pattern");
        prompt.AppendLine("- Each test should test one specific behavior");
        prompt.AppendLine("- Test names should clearly describe what is being tested");
        prompt.AppendLine("- Include edge cases and boundary conditions");
        prompt.AppendLine("- Test both success and failure scenarios");
        prompt.AppendLine("- Use appropriate mocking for dependencies");
        prompt.AppendLine("- Ensure tests are isolated and independent");
        prompt.AppendLine("- Add helpful comments for complex test setups");
        prompt.AppendLine();

        // Framework-specific guidance
        prompt.AppendLine($"# {framework}-Specific Guidelines");
        switch (framework.ToLowerInvariant()) {
            case "xunit":
                prompt.AppendLine("- Use [Fact] for simple tests and [Theory] with [InlineData] for parameterized tests");
                prompt.AppendLine("- Use constructor for test setup and IDisposable for cleanup");
                prompt.AppendLine("- Prefer Shouldly assertions (e.g., result.ShouldBe(expected))");
                break;
            case "nunit":
                prompt.AppendLine("- Use [Test] attribute for test methods");
                prompt.AppendLine("- Use [SetUp] and [TearDown] for test lifecycle");
                prompt.AppendLine("- Use [TestCase] for parameterized tests");
                break;
            case "mstest":
                prompt.AppendLine("- Use [TestMethod] attribute for test methods");
                prompt.AppendLine("- Use [TestInitialize] and [TestCleanup] for test lifecycle");
                prompt.AppendLine("- Use Assert class for assertions");
                break;
            case "jest":
                prompt.AppendLine("- Use describe() blocks to group related tests");
                prompt.AppendLine("- Use test() or it() for individual test cases");
                prompt.AppendLine("- Use expect() for assertions");
                prompt.AppendLine("- Use jest.mock() for mocking dependencies");
                break;
            case "pytest":
                prompt.AppendLine("- Use test_ prefix for test functions");
                prompt.AppendLine("- Use fixtures for shared setup");
                prompt.AppendLine("- Use assert statements for assertions");
                prompt.AppendLine("- Use @pytest.mark for test categorization");
                break;
        }

        prompt.AppendLine();
        prompt.AppendLine("# Response Format");
        prompt.AppendLine("- Return ONLY the complete test file code");
        prompt.AppendLine("- Do not include markdown code blocks or explanations");
        prompt.AppendLine("- Include all necessary imports/using statements");
        prompt.AppendLine("- Ensure the code is ready to compile and run");

        return prompt.ToString();
    }

    private string BuildPatternAnalysisPrompt(List<TestFile> existingTests) {
        var prompt = new StringBuilder();

        prompt.AppendLine("# Task: Analyze Test Patterns");
        prompt.AppendLine();
        prompt.AppendLine("Analyze the following test files and identify common patterns:");
        prompt.AppendLine();

        for (int i = 0; i < existingTests.Count && i < 3; i++) {
            var test = existingTests[i];
            prompt.AppendLine($"## Test File {i + 1}: {test.Path}");
            prompt.AppendLine("```");
            // Limit content to first 50 lines to avoid token limits
            var lines = test.Content.Split('\n');
            var limitedContent = string.Join('\n', lines.Take(50));
            prompt.AppendLine(limitedContent);
            if (lines.Length > 50) {
                prompt.AppendLine("... (truncated)");
            }
            prompt.AppendLine("```");
            prompt.AppendLine();
        }

        prompt.AppendLine("# Analysis Required");
        prompt.AppendLine("Identify and describe:");
        prompt.AppendLine("1. Naming convention for test methods (e.g., MethodName_Scenario_ExpectedResult)");
        prompt.AppendLine("2. Assertion style used (e.g., Shouldly, FluentAssertions, Assert)");
        prompt.AppendLine("3. Whether Arrange-Act-Assert pattern is used (yes/no)");
        prompt.AppendLine("4. Common imports/using statements");
        prompt.AppendLine("5. Base test class if any");
        prompt.AppendLine();
        prompt.AppendLine("Format your response as:");
        prompt.AppendLine("NamingConvention: <pattern>");
        prompt.AppendLine("AssertionStyle: <style>");
        prompt.AppendLine("UsesArrangeActAssert: <yes/no>");
        prompt.AppendLine("CommonImports: <import1>, <import2>, ...");
        prompt.AppendLine("BaseTestClass: <class name or none>");

        return prompt.ToString();
    }

    private TestPattern ParsePatternAnalysis(string analysisResult, List<TestFile> existingTests) {
        var namingConvention = "";
        var assertionStyle = "";
        var usesArrangeActAssert = false;
        var commonImports = new List<string>();
        var baseTestClass = "";

        var lines = analysisResult.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines) {
            var parts = line.Split(':', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            var key = parts[0].ToLowerInvariant();
            var value = parts[1].Trim();

            switch (key) {
                case "namingconvention":
                    namingConvention = value;
                    break;
                case "assertionstyle":
                    assertionStyle = value;
                    break;
                case "usesarrangeactassert":
                    usesArrangeActAssert = value.ToLowerInvariant() == "yes";
                    break;
                case "commonimports":
                    commonImports = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    break;
                case "basetestclass":
                    baseTestClass = value == "none" ? "" : value;
                    break;
            }
        }

        // Fallback to heuristics if parsing failed
        if (string.IsNullOrEmpty(namingConvention)) {
            return AnalyzePatternHeuristically(existingTests);
        }

        return new TestPattern {
            NamingConvention = namingConvention,
            AssertionStyle = assertionStyle,
            UsesArrangeActAssert = usesArrangeActAssert,
            CommonImports = commonImports,
            BaseTestClass = baseTestClass
        };
    }

    private TestPattern AnalyzePatternHeuristically(List<TestFile> existingTests) {
        var namingConvention = "MethodName_Scenario_ExpectedOutcome";
        var assertionStyle = "";
        var usesArrangeActAssert = false;
        var imports = new List<string>();

        if (existingTests.Count == 0) {
            return new TestPattern {
                NamingConvention = namingConvention,
                AssertionStyle = "Shouldly",
                UsesArrangeActAssert = usesArrangeActAssert,
                CommonImports = imports,
                BaseTestClass = ""
            };
        }

        var combinedContent = string.Join("\n", existingTests.Select(t => t.Content));

        // Detect assertion style
        if (combinedContent.Contains(".ShouldBe(") || combinedContent.Contains(".ShouldNotBe(")) {
            assertionStyle = "Shouldly";
        }
        else if (combinedContent.Contains(".Should().Be(") || combinedContent.Contains("FluentAssertions")) {
            assertionStyle = "FluentAssertions";
        }
        else if (combinedContent.Contains("Assert.")) {
            assertionStyle = "Assert";
        }
        else if (combinedContent.Contains("expect(")) {
            assertionStyle = "expect";
        }

        // Detect AAA pattern
        if (combinedContent.Contains("// Arrange") || combinedContent.Contains("# Arrange")) {
            usesArrangeActAssert = true;
        }

        // Extract common imports (simple heuristic)
        var lines = combinedContent.Split('\n');
        foreach (var line in lines.Take(30)) { // Look at first 30 lines
            if (line.Trim().StartsWith("using ") || line.Trim().StartsWith("import ")) {
                imports.Add(line.Trim());
            }
        }

        return new TestPattern {
            NamingConvention = namingConvention,
            AssertionStyle = assertionStyle,
            UsesArrangeActAssert = usesArrangeActAssert,
            CommonImports = imports.Distinct().Take(10).ToList(),
            BaseTestClass = ""
        };
    }

    private string DetectFrameworkFromContent(string content) {
        if (content.Contains("using Xunit;") || content.Contains("[Fact]") || content.Contains("[Theory]")) {
            return "xUnit";
        }
        if (content.Contains("using NUnit.Framework;") || content.Contains("[Test]")) {
            return "NUnit";
        }
        if (content.Contains("using Microsoft.VisualStudio.TestTools") || content.Contains("[TestMethod]")) {
            return "MSTest";
        }
        if (content.Contains("describe(") && content.Contains("it(")) {
            return "Jest";
        }
        if (content.Contains("import pytest") || content.Contains("def test_")) {
            return "pytest";
        }
        if (content.Contains("import unittest")) {
            return "unittest";
        }
        return "Unknown";
    }

    private TestResult ParseTestResult(CommandResult result) {
        var success = result.Success;
        var output = result.Output;
        var totalTests = 0;
        var passedTests = 0;
        var failedTests = 0;
        var failures = new List<string>();

        // Parse .NET test results
        var dotnetMatch = Regex.Match(output, @"Total tests: (\d+).*?Passed: (\d+).*?Failed: (\d+)", RegexOptions.Singleline);
        if (dotnetMatch.Success) {
            totalTests = int.Parse(dotnetMatch.Groups[1].Value);
            passedTests = int.Parse(dotnetMatch.Groups[2].Value);
            failedTests = int.Parse(dotnetMatch.Groups[3].Value);
        }

        // Parse Jest results
        var jestMatch = Regex.Match(output, @"Tests:.*?(\d+) passed.*?(\d+) total", RegexOptions.Singleline);
        if (jestMatch.Success) {
            passedTests = int.Parse(jestMatch.Groups[1].Value);
            totalTests = int.Parse(jestMatch.Groups[2].Value);
            failedTests = totalTests - passedTests;
        }

        // Parse pytest results
        var pytestMatch = Regex.Match(output, @"(\d+) passed.*?(\d+) failed", RegexOptions.Singleline);
        if (pytestMatch.Success) {
            passedTests = int.Parse(pytestMatch.Groups[1].Value);
            failedTests = int.Parse(pytestMatch.Groups[2].Value);
            totalTests = passedTests + failedTests;
        }

        // Extract failure messages
        var failureMatches = Regex.Matches(output, @"Failed\s+(.*?)\s*\n", RegexOptions.Multiline);
        foreach (Match match in failureMatches) {
            failures.Add(match.Groups[1].Value.Trim());
        }

        return new TestResult {
            Success = success,
            TotalTests = totalTests,
            PassedTests = passedTests,
            FailedTests = failedTests,
            Failures = failures,
            Output = output
        };
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
}
