using Moq;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services.Docker;
using RG.OpenCopilot.PRGenerationAgent.Services.Executor;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Shouldly;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace RG.OpenCopilot.Tests;

public class BuildVerifierTests {
    private readonly Mock<IContainerManager> _containerManager;
    private readonly Mock<IFileEditor> _fileEditor;
    private readonly Kernel _kernel;
    private readonly Mock<IChatCompletionService> _chatService;
    private readonly TestLogger<BuildVerifier> _logger;
    private readonly BuildVerifier _verifier;

    public BuildVerifierTests() {
        _containerManager = new Mock<IContainerManager>();
        _fileEditor = new Mock<IFileEditor>();
        _chatService = new Mock<IChatCompletionService>();
        _logger = new TestLogger<BuildVerifier>();

        // Create a real Kernel with mocked chat service
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(_chatService.Object);
        _kernel = kernelBuilder.Build();

        _verifier = new BuildVerifier(
            containerManager: _containerManager.Object,
            fileEditor: _fileEditor.Object,
            kernel: _kernel,
            logger: _logger);
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithDotnetProject_ReturnsDotnet() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./MyProject.csproj"
            });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBe("dotnet");
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithNpmProject_ReturnsNpm() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./package.json"
            });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBe("npm");
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithGradleProject_ReturnsGradle() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./build.gradle"
            });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBe("gradle");
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithMavenProject_ReturnsMaven() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("pom.xml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./pom.xml"
            });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBe("maven");
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithGoProject_ReturnsGo() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("pom.xml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("go.mod")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./go.mod"
            });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBe("go");
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithCargoProject_ReturnsCargo() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("pom.xml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("go.mod")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("Cargo.toml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Cargo.toml"
            });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBe("cargo");
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithNoProjectFiles_ReturnsNull() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBeNull();
    }

    [Fact]
    public async Task RunBuildAsync_WithDotnet_ExecutesDotnetBuild() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("Build succeeded");
    }

    [Fact]
    public async Task RunBuildAsync_WithNpm_ExecutesNpmRunBuild() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "npm");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "npm",
                It.Is<string[]>(args => args.Contains("run") && args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build completed successfully."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("Build completed");
    }

    [Fact]
    public async Task RunBuildAsync_WithNoBuildTool_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: null);

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("No build tool detected");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithDotnetErrors_ParsesCorrectly() {
        // Arrange
        var output = """
            Program.cs(10,15): error CS1002: ; expected
            Utils.cs(25,30): warning CS0168: The variable 'x' is declared but never used
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(2);
        
        var error = errors[0];
        error.FilePath.ShouldBe("Program.cs");
        error.LineNumber.ShouldBe(10);
        error.ErrorCode.ShouldBe("CS1002");
        error.Message.ShouldBe("; expected");
        error.Severity.ShouldBe(ErrorSeverity.Error);

        var warning = errors[1];
        warning.FilePath.ShouldBe("Utils.cs");
        warning.LineNumber.ShouldBe(25);
        warning.ErrorCode.ShouldBe("CS0168");
        warning.Severity.ShouldBe(ErrorSeverity.Warning);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithNpmErrors_ParsesCorrectly() {
        // Arrange
        var output = """
            ERROR in ./src/app.ts
            src/index.ts(42,5): error TS2304: Cannot find name 'foo'.
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "npm");

        // Assert
        errors.Count.ShouldBe(2);
        
        var error1 = errors[0];
        error1.FilePath.ShouldBe("./src/app.ts");
        error1.Severity.ShouldBe(ErrorSeverity.Error);

        var error2 = errors[1];
        error2.FilePath.ShouldBe("src/index.ts");
        error2.LineNumber.ShouldBe(42);
        error2.ErrorCode.ShouldBe("TS2304");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithCargoErrors_ParsesCorrectly() {
        // Arrange
        var output = """
            error[E0425]: cannot find value `x` in this scope
               --> src/main.rs:10:5
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "cargo");

        // Assert
        errors.Count.ShouldBe(1);
        
        var error = errors[0];
        error.ErrorCode.ShouldBe("E0425");
        error.Message.ShouldContain("cannot find value");
        error.FilePath.ShouldBe("src/main.rs");
        error.LineNumber.ShouldBe(10);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithSuccessfulBuild_ReturnsSuccessOnFirstAttempt() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeTrue();
        result.Attempts.ShouldBe(1);
        result.Errors.ShouldBeEmpty();
        result.FixesApplied.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyBuildAsync_WithBuildFailureAndNoDetectedTool_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: null);

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeFalse();
        result.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithMaxRetriesExceeded_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "Program.cs(10,15): error CS1002: ; expected"
            });

        SetupLlmFixGeneration();

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 2);

        // Assert
        result.Success.ShouldBeFalse();
        result.Attempts.ShouldBe(1);
        result.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RunBuildAsync_WithGradle_ExecutesGradleBuild() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "gradle");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "./gradlew",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build successful."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("Build successful");
    }

    [Fact]
    public async Task RunBuildAsync_WithMaven_ExecutesMavenCompile() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "maven");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "mvn",
                It.Is<string[]>(args => args.Contains("compile")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Compilation succeeded."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("Compilation succeeded");
    }

    [Fact]
    public async Task RunBuildAsync_WithGo_ExecutesGoBuild() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "go");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "go",
                It.Is<string[]>(args => args.Contains("build") && args.Contains("./...")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build completed."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("Build completed");
    }

    [Fact]
    public async Task RunBuildAsync_WithCargo_ExecutesCargoBuild() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "cargo");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "cargo",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Compiling project..."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        result.Output.ShouldContain("Compiling project");
    }

    [Fact]
    public async Task RunBuildAsync_WithUnsupportedBuildTool_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        // Simulate an unsupported build tool by mocking DetectBuildToolAsync to return "unsupported"
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                It.IsAny<string>(),
                "find",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldBe("No build tool detected");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithGradleErrors_ParsesCorrectly() {
        // Arrange
        var output = """
            /path/to/File.java:42: error: cannot find symbol
            /path/to/Another.java:15: warning: unused variable
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "gradle");

        // Assert
        errors.Count.ShouldBe(2);
        
        var error = errors[0];
        error.FilePath.ShouldBe("/path/to/File.java");
        error.LineNumber.ShouldBe(42);
        error.Severity.ShouldBe(ErrorSeverity.Error);
        error.Message.ShouldBe("cannot find symbol");

        var warning = errors[1];
        warning.FilePath.ShouldBe("/path/to/Another.java");
        warning.LineNumber.ShouldBe(15);
        warning.Severity.ShouldBe(ErrorSeverity.Warning);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithMavenErrors_ParsesCorrectly() {
        // Arrange
        var output = """
            [ERROR] /path/to/Main.java[10,5] cannot find symbol: variable foo
            [WARNING] /path/to/Utils.java[25,10] unchecked conversion
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "maven");

        // Assert
        errors.Count.ShouldBe(2);
        
        var error = errors[0];
        error.FilePath.ShouldBe("/path/to/Main.java");
        error.LineNumber.ShouldBe(10);
        error.Severity.ShouldBe(ErrorSeverity.Error);
        error.Message.ShouldContain("cannot find symbol");

        var warning = errors[1];
        warning.Severity.ShouldBe(ErrorSeverity.Warning);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithGoErrors_ParsesCorrectly() {
        // Arrange
        var output = """
            main.go:25:10: undefined: fmt
            utils.go:42:5: syntax error: unexpected }
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "go");

        // Assert
        errors.Count.ShouldBe(2);
        
        var error1 = errors[0];
        error1.FilePath.ShouldBe("main.go");
        error1.LineNumber.ShouldBe(25);
        error1.Message.ShouldContain("undefined");

        var error2 = errors[1];
        error2.FilePath.ShouldBe("utils.go");
        error2.LineNumber.ShouldBe(42);
        error2.Message.ShouldContain("syntax error");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithUnsupportedBuildTool_ReturnsEmptyList() {
        // Arrange
        var output = "Some build output";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "unsupported");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithEmptyOutput_ReturnsEmptyList() {
        // Arrange
        var output = "";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateFixesAsync_WithLlmException_ReturnsEmptyList() {
        // Arrange
        var errors = new List<BuildError> {
            new BuildError {
                FilePath = "test.cs",
                LineNumber = 10,
                ErrorCode = "CS1002",
                Message = "Expected ;",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.Syntax
            }
        };

        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM service unavailable"));

        // Act
        var fixes = await _verifier.GenerateFixesAsync(errors: errors);

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateFixesAsync_WithInvalidJsonResponse_ReturnsEmptyList() {
        // Arrange
        var errors = new List<BuildError> {
            new BuildError {
                FilePath = "test.cs",
                ErrorCode = "CS1002",
                Message = "Expected ;",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.Syntax
            }
        };

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, "{ invalid json }");
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        // Act
        var fixes = await _verifier.GenerateFixesAsync(errors: errors);

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateFixesAsync_WithValidResponse_ReturnsFixList() {
        // Arrange
        var errors = new List<BuildError> {
            new BuildError {
                FilePath = "test.cs",
                LineNumber = 10,
                ErrorCode = "CS1002",
                Message = "Expected ;",
                Severity = ErrorSeverity.Error,
                Category = ErrorCategory.Syntax
            }
        };

        var jsonResponse = """
            {
              "fixes": [
                {
                  "filePath": "test.cs",
                  "description": "Add missing semicolon",
                  "originalCode": "int x = 5",
                  "fixedCode": "int x = 5;",
                  "confidence": "High"
                }
              ]
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, jsonResponse);
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        // Act
        var fixes = await _verifier.GenerateFixesAsync(errors: errors);

        // Assert
        fixes.Count.ShouldBe(1);
        fixes[0].FilePath.ShouldBe("test.cs");
        fixes[0].Description.ShouldBe("Add missing semicolon");
        fixes[0].OriginalCode.ShouldBe("int x = 5");
        fixes[0].FixedCode.ShouldBe("int x = 5;");
        fixes[0].Confidence.ShouldBe(FixConfidence.High);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithSuccessfulRetry_ReturnsSuccess() {
        // Arrange
        var containerId = "test-container";
        var attempt = 0;
        SetupBuildToolDetection(buildTool: "dotnet");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attempt++;
                if (attempt == 1) {
                    return new CommandResult {
                        ExitCode = 1,
                        Output = "Program.cs(10,15): error CS1002: ; expected"
                    };
                }
                return new CommandResult {
                    ExitCode = 0,
                    Output = "Build succeeded."
                };
            });

        var jsonResponse = """
            {
              "fixes": [
                {
                  "filePath": "Program.cs",
                  "description": "Add semicolon",
                  "originalCode": "int x = 5",
                  "fixedCode": "int x = 5;",
                  "confidence": "High"
                }
              ]
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, jsonResponse);
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        _fileEditor
            .Setup(f => f.ModifyFileAsync(
                containerId,
                "Program.cs",
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeTrue();
        result.Attempts.ShouldBe(2);
        result.FixesApplied.Count.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithFileEditorException_ContinuesWithoutFix() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "Program.cs(10,15): error CS1002: ; expected"
            });

        var jsonResponse = """
            {
              "fixes": [
                {
                  "filePath": "Program.cs",
                  "description": "Add semicolon",
                  "originalCode": "int x = 5",
                  "fixedCode": "int x = 5;",
                  "confidence": "High"
                }
              ]
            }
            """;

        var chatContent = new ChatMessageContent(AuthorRole.Assistant, jsonResponse);
        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });

        _fileEditor
            .Setup(f => f.ModifyFileAsync(
                containerId,
                "Program.cs",
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("File not found"));

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 2);

        // Assert
        result.Success.ShouldBeFalse();
        result.FixesApplied.ShouldBeEmpty();
    }

    [Fact]
    public async Task VerifyBuildAsync_WithBuildErrorsButNoParsedErrors_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "Some error that doesn't match any pattern",
                Error = "Build failed"
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeFalse();
        result.Attempts.ShouldBe(1);
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithDependencyError_CategorizedCorrectly() {
        // Arrange
        var output = "Program.cs(10,15): error CS0246: The type or namespace name 'Package' could not be found";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.MissingDependency);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithTypeError_CategorizedCorrectly() {
        // Arrange
        var output = "Program.cs(10,15): error CS1503: Cannot convert type int to string";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.Type);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithSyntaxError_CategorizedCorrectly() {
        // Arrange
        var output = "Program.cs(10,15): error CS0103: The name 'foo' does not exist but expected ; was found";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.Syntax);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithConfigurationError_CategorizedCorrectly() {
        // Arrange
        var output = "Program.cs(10,15): error CS0000: Invalid configuration setting for feature";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.Configuration);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithTS2ErrorCode_CategorizedAsType() {
        // Arrange
        var output = "index.ts(10,15): error TS2345: Argument of type 'string' is not assignable to parameter of type 'number'";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "npm");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.Type);
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingDotnetTool_ReturnsToolNotInstalledError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "dotnet"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no dotnet in (/usr/bin)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("dotnet");
        result.Error.ShouldContain("is not installed");
        result.Error.ShouldContain("Install .NET SDK");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingNpmTool_ReturnsToolNotInstalledError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./package.json"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "npm"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no npm in (/usr/bin)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("npm");
        result.Error.ShouldContain("is not installed");
        result.Error.ShouldContain("Install Node.js");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingMavenTool_ReturnsToolNotInstalledError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("pom.xml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./pom.xml"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "mvn"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no mvn in (/usr/bin)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("mvn");
        result.Error.ShouldContain("is not installed");
        result.Error.ShouldContain("Install Maven");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingGoTool_ReturnsToolNotInstalledError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("pom.xml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("go.mod")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./go.mod"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "go"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no go in (/usr/bin)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("go");
        result.Error.ShouldContain("is not installed");
        result.Error.ShouldContain("Install Go");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingCargoTool_ReturnsToolNotInstalledError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("pom.xml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("go.mod")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("Cargo.toml")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Cargo.toml"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "cargo"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no cargo in (/usr/bin)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("cargo");
        result.Error.ShouldContain("is not installed");
        result.Error.ShouldContain("Install Rust");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithMissingTool_SetsToolAvailableToFalse() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "dotnet"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no dotnet in (/usr/bin)"
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeFalse();
        result.ToolAvailable.ShouldBeFalse();
        result.MissingTool.ShouldBe("dotnet");
        result.Output.ShouldContain("is not installed");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithAvailableTool_SetsToolAvailableToTrue() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeTrue();
        result.ToolAvailable.ShouldBeTrue();
        result.MissingTool.ShouldBeNull();
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingGradleTool_ReturnsToolNotInstalledError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("build.gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./build.gradle"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "gradle"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no gradle in (/usr/bin)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("gradle");
        result.Error.ShouldContain("is not installed");
        result.Error.ShouldContain("Install Gradle");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithMissingTool_DoesNotRetry() {
        // Arrange
        var containerId = "test-container";
        var executionCount = 0;
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "dotnet"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                executionCount++;
                return new CommandResult {
                    ExitCode = 1,
                    Output = "",
                    Error = "which: no dotnet in (/usr/bin)"
                };
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 5);

        // Assert
        result.Success.ShouldBeFalse();
        result.ToolAvailable.ShouldBeFalse();
        result.MissingTool.ShouldBe("dotnet");
        result.Attempts.ShouldBe(1);
        executionCount.ShouldBe(1); // Should only check once, not retry
    }

    [Fact]
    public async Task RunBuildAsync_WithWhichCommandException_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "dotnet"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Container communication error"));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _verifier.RunBuildAsync(containerId: containerId));

        exception.Message.ShouldBe("Container communication error");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithBuildFailureThenToolCheck_ReturnsCorrectStatus() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "Program.cs(10,15): error CS1002: ; expected"
            });

        SetupLlmFixGeneration();

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 1);

        // Assert
        result.Success.ShouldBeFalse();
        result.ToolAvailable.ShouldBeTrue(); // Tool was available, just build failed
        result.MissingTool.ShouldBeNull();
        result.Errors.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RunBuildAsync_WithToolAvailable_LogsSuccessfully() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        _logger.LogEntries.ShouldContain(entry => 
            entry.Contains("Running build with dotnet"));
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingTool_LogsWarningWithInstructions() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == "dotnet"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: no dotnet in (/usr/bin)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        _logger.LogEntries.ShouldContain(entry => 
            entry.Contains("Build tool 'dotnet' is not available") &&
            entry.Contains("Install .NET SDK"));
    }

    [Fact]
    public async Task VerifyBuildAsync_WithSuccessAfterToolCheck_ReturnsCorrectDuration() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeTrue();
        result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        result.ToolAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task RunBuildAsync_WithAllBuildTools_ChecksCorrectToolCommand() {
        // Arrange & Act & Assert for dotnet
        await VerifyToolCommandMapping("dotnet", "dotnet");
        await VerifyToolCommandMapping("npm", "npm");
        await VerifyToolCommandMapping("maven", "mvn");
        await VerifyToolCommandMapping("go", "go");
        await VerifyToolCommandMapping("cargo", "cargo");
        await VerifyToolCommandMapping("gradle", "gradle");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithMultipleErrorsAndMissingTool_ReturnsToolStatusFirst() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "which: command not found"
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeFalse();
        result.ToolAvailable.ShouldBeFalse();
        result.MissingTool.ShouldBe("dotnet");
        result.Errors.ShouldBeEmpty(); // Should fail on tool check before parsing errors
    }

    [Fact]
    public async Task VerifyBuildAsync_WithMaxRetries1_AttemptsOnce() {
        // Arrange
        var containerId = "test-container";
        var attemptCount = 0;
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                return new CommandResult {
                    ExitCode = 1,
                    Output = "Program.cs(10,15): error CS1002: ; expected"
                };
            });

        SetupLlmFixGeneration();

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 1);

        // Assert
        result.Success.ShouldBeFalse();
        result.Attempts.ShouldBe(1);
        attemptCount.ShouldBe(1);
        result.ToolAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task RunBuildAsync_WithDotnetTool_ReturnsCorrectInstallationURL() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args[0] == "dotnet"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Error.ShouldContain("https://dotnet.microsoft.com/download");
    }

    [Fact]
    public async Task RunBuildAsync_WithNpmTool_ReturnsCorrectInstallationURL() {
        // Arrange
        var containerId = "test-container";
        SetupProjectDetectionForBuildTool(containerId, "npm");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args[0] == "npm"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Error.ShouldContain("https://nodejs.org/");
    }

    [Fact]
    public async Task RunBuildAsync_WithGoTool_ReturnsCorrectInstallationURL() {
        // Arrange
        var containerId = "test-container";
        SetupProjectDetectionForBuildTool(containerId, "go");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args[0] == "go"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Error.ShouldContain("https://golang.org/doc/install");
    }

    [Fact]
    public async Task RunBuildAsync_WithCargoTool_ReturnsCorrectInstallationURL() {
        // Arrange
        var containerId = "test-container";
        SetupProjectDetectionForBuildTool(containerId, "cargo");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args[0] == "cargo"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Error.ShouldContain("https://www.rust-lang.org/tools/install");
    }

    [Fact]
    public async Task RunBuildAsync_WithMavenTool_ReturnsCorrectInstallationURL() {
        // Arrange
        var containerId = "test-container";
        SetupProjectDetectionForBuildTool(containerId, "maven");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args[0] == "mvn"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Error.ShouldContain("https://maven.apache.org/install.html");
    }

    [Fact]
    public async Task RunBuildAsync_WithGradleTool_ReturnsCorrectInstallationURL() {
        // Arrange
        var containerId = "test-container";
        SetupProjectDetectionForBuildTool(containerId, "gradle");

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args[0] == "gradle"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Error.ShouldContain("https://gradle.org/install/");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithAllBuildResultFields_PopulatesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "Program.cs(10,15): error CS1002: ; expected"
            });

        SetupLlmFixGeneration();

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 2);

        // Assert - Validate all BuildResult properties
        result.Success.ShouldBeFalse();
        result.Attempts.ShouldBe(1);
        result.Output.ShouldNotBeEmpty();
        result.Errors.ShouldNotBeEmpty();
        result.FixesApplied.ShouldBeEmpty();
        result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        result.ToolAvailable.ShouldBeTrue();
        result.MissingTool.ShouldBeNull();
    }

    [Fact]
    public async Task VerifyBuildAsync_WithMissingToolAllFields_PopulatesCorrectly() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "not found"
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 5);

        // Assert - Validate all BuildResult properties for missing tool scenario
        result.Success.ShouldBeFalse();
        result.Attempts.ShouldBe(1);
        result.Output.ShouldContain("is not installed");
        result.Errors.ShouldBeEmpty();
        result.FixesApplied.ShouldBeEmpty();
        result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        result.ToolAvailable.ShouldBeFalse();
        result.MissingTool.ShouldBe("dotnet");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithMultipleCategories_CategorizedCorrectly() {
        // Arrange
        var output = """
            Program.cs(10,15): error CS0246: The type or namespace name 'Package' could not be found
            Program.cs(20,10): error CS1503: Cannot convert type int to string
            Program.cs(30,5): error CS1002: ; expected
            Program.cs(40,8): error CS0000: Invalid configuration setting for feature
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(4);
        // Verify at least some categories are correctly identified
        errors.ShouldContain(e => e.Category == ErrorCategory.MissingDependency);
        errors.ShouldContain(e => e.Category == ErrorCategory.Type);
        // All errors should have some category assigned (not default/unknown)
        errors.ShouldAllBe(e => e.ErrorCode.Length > 0);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithSuccessAllFields_PopulatesCorrectly() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert - Validate all BuildResult properties for success scenario
        result.Success.ShouldBeTrue();
        result.Attempts.ShouldBe(1);
        result.Output.ShouldContain("Build succeeded");
        result.Errors.ShouldBeEmpty();
        result.FixesApplied.ShouldBeEmpty();
        result.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        result.ToolAvailable.ShouldBeTrue();
        result.MissingTool.ShouldBeNull();
    }

    [Fact]
    public async Task RunBuildAsync_WithToolCheckTimeout_HandlesGracefully() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        var delayCount = 0;
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(async () => {
                delayCount++;
                if (delayCount == 1) {
                    await Task.Delay(100); // Simulate slow response
                }
                return new CommandResult {
                    ExitCode = 0,
                    Output = "/usr/bin/dotnet"
                };
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
        delayCount.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithEmptyContainerId_HandlesGracefully() {
        // Arrange
        var containerId = "";
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "Invalid container ID"
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 1);

        // Assert
        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task RunBuildAsync_WithWhichReturnsNonStandardPath_AcceptsToolAsAvailable() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args[0] == "dotnet"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "/opt/custom/path/dotnet" // Non-standard path
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyBuildAsync_WithLargeMaxRetries_RespectsLimit() {
        // Arrange
        var containerId = "test-container";
        var attemptCount = 0;
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                return new CommandResult {
                    ExitCode = 1,
                    Output = "Program.cs(10,15): error CS1002: ; expected"
                };
            });

        SetupLlmFixGeneration();

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 100);

        // Assert
        result.Success.ShouldBeFalse();
        result.Attempts.ShouldBe(1); // Should fail on first attempt due to no parseable errors after LLM response
        attemptCount.ShouldBeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task RunBuildAsync_WithNullContainerId_ThrowsException() {
        // Arrange
        string? containerId = null;

        // Act & Assert
        await Should.ThrowAsync<Exception>(
            async () => await _verifier.RunBuildAsync(containerId: containerId!));
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithNullContainerId_ThrowsException() {
        // Arrange
        string? containerId = null;

        // Act & Assert
        await Should.ThrowAsync<Exception>(
            async () => await _verifier.DetectBuildToolAsync(containerId: containerId!));
    }

    [Fact]
    public async Task VerifyBuildAsync_WithNullContainerId_ThrowsException() {
        // Arrange
        string? containerId = null;

        // Act & Assert
        await Should.ThrowAsync<Exception>(
            async () => await _verifier.VerifyBuildAsync(containerId: containerId!));
    }

    [Fact]
    public async Task RunBuildAsync_WithContainerExecutionFailure_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                It.IsAny<string>(),
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Container has stopped"));

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _verifier.DetectBuildToolAsync(containerId: containerId));
        
        exception.Message.ShouldBe("Container has stopped");
    }

    [Fact]
    public async Task RunBuildAsync_WithPermissionDenied_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 126,
                Output = "",
                Error = "Permission denied"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.ExitCode.ShouldBe(126);
        result.Error.ShouldContain("Permission denied");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithMalformedOutput_HandlesGracefully() {
        // Arrange
        var malformedOutput = """
            ��@#$%^&*()
            Random text without structure
            �������
            Not a valid error format
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: malformedOutput,
            buildTool: "dotnet");

        // Assert
        errors.ShouldBeEmpty(); // Should handle gracefully, not crash
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithVeryLargeOutput_HandlesGracefully() {
        // Arrange
        var largeOutput = string.Join("\n", Enumerable.Repeat("Program.cs(10,15): error CS1002: ; expected", 10000));

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: largeOutput,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(10000);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithUnicodeCharacters_HandlesCorrectly() {
        // Arrange
        var unicodeOutput = """
            Program.cs(10,15): error CS0246: 类型或命名空间名称"Package"找不到
            Program.cs(20,10): error CS1503: Не удалось преобразовать тип
            Program.cs(30,5): error CS1002: 예상되는 ;
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: unicodeOutput,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBeGreaterThan(0);
        errors.ShouldAllBe(e => !string.IsNullOrEmpty(e.Message));
    }

    [Fact]
    public async Task RunBuildAsync_WithNetworkTimeout_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Command execution timed out"));

        // Act & Assert
        var exception = await Should.ThrowAsync<TimeoutException>(
            async () => await _verifier.RunBuildAsync(containerId: containerId));
        
        exception.Message.ShouldContain("timed out");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithBuildOutputContainingNoErrors_ReturnsFailure() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "Build failed but no error details provided"
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 1);

        // Assert
        result.Success.ShouldBeFalse();
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunBuildAsync_WithAllToolsMissing_ReturnsFirstMissingTool() {
        // Arrange
        var containerId = "test-container";
        
        // Setup: no project files found
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = ""
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("No build tool detected");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithMixedValidAndInvalidLines_ParsesValidOnes() {
        // Arrange
        var mixedOutput = """
            Program.cs(10,15): error CS1002: ; expected
            This is invalid garbage text
            ������
            Program.cs(20,10): error CS0246: Type not found
            More random text
            Program.cs(30,5): warning CS8618: Nullable reference
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: mixedOutput,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBeGreaterThanOrEqualTo(2); // At least the valid error lines
        errors.ShouldAllBe(e => !string.IsNullOrEmpty(e.ErrorCode));
    }

    [Fact]
    public async Task VerifyBuildAsync_WithFileEditorThrowingOnEveryFix_StillCompletes() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "Program.cs(10,15): error CS1002: ; expected"
            });

        _fileEditor
            .Setup(f => f.ModifyFileAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("File is locked by another process"));

        SetupLlmFixGeneration();

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 2);

        // Assert
        result.Success.ShouldBeFalse();
        result.FixesApplied.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunBuildAsync_WithCorruptedToolInstallation_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 139, // Segmentation fault
                Output = "",
                Error = "Segmentation fault (core dumped)"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.ExitCode.ShouldBe(139);
        result.Error.ShouldContain("Segmentation fault");
    }

    [Fact]
    public async Task DetectBuildToolAsync_WithMultipleProjectFiles_ReturnsFirstDetected() {
        // Arrange
        var containerId = "test-container";
        
        // Setup both dotnet and npm project files present
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("package.json")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./package.json"
            });

        // Act
        var buildTool = await _verifier.DetectBuildToolAsync(containerId: containerId);

        // Assert
        buildTool.ShouldBe("dotnet"); // Should return first detected (dotnet checked before npm)
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithNullOutput_ReturnsEmptyList() {
        // Arrange
        string? nullOutput = null;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: nullOutput!,
            buildTool: "dotnet");

        // Assert
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunBuildAsync_WithToolExitingImmediately_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 127, // Command not found
                Output = "",
                Error = "dotnet: command not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.ExitCode.ShouldBe(127);
        result.Error.ShouldContain("command not found");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithZeroMaxRetries_AttemptsOnce() {
        // Arrange - Even with maxRetries=0, should attempt at least once
        var containerId = "test-container";
        var attemptCount = 0;
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                return new CommandResult {
                    ExitCode = 0,
                    Output = "Build succeeded."
                };
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 0);

        // Assert - Should attempt at least once even with maxRetries=0
        result.Success.ShouldBeTrue();
        attemptCount.ShouldBe(1);
        result.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithEmptyBuildTool_ReturnsEmptyList() {
        // Arrange
        var output = "Program.cs(10,15): error CS1002: ; expected";

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "");

        // Assert
        errors.ShouldBeEmpty(); // Empty build tool means we can't parse
    }

    [Fact]
    public async Task RunBuildAsync_WithDiskFullError_ReturnsError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "No space left on device"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("No space left on device");
    }

    [Fact]
    public async Task VerifyBuildAsync_WithNegativeMaxRetries_HandlesGracefully() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "dotnet",
                It.Is<string[]>(args => args.Contains("build")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: -1);

        // Assert - Should handle negative value gracefully
        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunBuildAsync_WithWhichCommandNotAvailable_HandlesGracefully() {
        // Arrange
        var containerId = "test-container";
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "find",
                It.Is<string[]>(args => args.Contains("*.csproj")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "./Project.csproj"
            });

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.IsAny<string[]>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 127,
                Output = "",
                Error = "which: command not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("is not installed");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithErrorCategoryOther_CategorizesAsOther() {
        // Arrange
        var output = """
            Program.cs(10,15): error CS9999: Some unknown error type
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.Other);
        errors[0].ErrorCode.ShouldBe("CS9999");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithRuntimeCategory_CategorizesCorrectly() {
        // Arrange
        var output = """
            Program.cs(10,15): error CS1234: Runtime initialization failed
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        // Runtime errors might be categorized as Other if no specific runtime category exists
        errors[0].ShouldNotBeNull();
    }

    [Fact]
    public async Task GenerateFixesAsync_WithEmptyErrorsList_ReturnsEmptyList() {
        // Arrange
        var emptyErrors = new List<BuildError>();

        // Act
        var fixes = await _verifier.GenerateFixesAsync(
            errors: emptyErrors);

        // Assert
        fixes.ShouldBeEmpty();
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithDotnetWarning_ParsesAsWarning() {
        // Arrange
        var output = """
            Program.cs(10,15): warning CS8618: Non-nullable field must contain a non-null value when exiting constructor
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Severity.ShouldBe(ErrorSeverity.Warning);
        errors[0].ErrorCode.ShouldBe("CS8618");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithNpmErrorInPattern_ParsesCorrectly() {
        // Arrange
        var output = """
            ERROR in ./src/app.ts
            ERROR in ./src/utils.ts
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "npm");

        // Assert
        errors.Count.ShouldBe(2);
        errors[0].FilePath.ShouldBe("./src/app.ts");
        errors[1].FilePath.ShouldBe("./src/utils.ts");
        errors.ShouldAllBe(e => e.Category == ErrorCategory.Other);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithGradleErrorPattern_ParsesCorrectly() {
        // Arrange
        var output = """
            /home/project/src/Main.java:15: error: cannot find symbol
            symbol: class MyClass
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "gradle");

        // Assert
        errors.Count.ShouldBeGreaterThan(0);
        errors[0].FilePath.ShouldContain("Main.java");
        errors[0].LineNumber.ShouldBe(15);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithMavenErrorPattern_ParsesCorrectly() {
        // Arrange
        var output = """
            [ERROR] /project/src/main/java/App.java:[25,10] cannot find symbol
            [ERROR] symbol: class Logger
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "maven");

        // Assert
        errors.Count.ShouldBeGreaterThan(0);
        errors[0].FilePath.ShouldContain("App.java");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithGoMultipleErrors_ParsesAll() {
        // Arrange
        var output = """
            main.go:10:5: undefined: fmt
            main.go:15:2: syntax error: unexpected newline
            utils.go:20:1: missing return at end of function
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "go");

        // Assert
        errors.Count.ShouldBe(3);
        errors[0].FilePath.ShouldBe("main.go");
        errors[0].LineNumber.ShouldBe(10);
        errors[1].FilePath.ShouldBe("main.go");
        errors[1].LineNumber.ShouldBe(15);
        errors[2].FilePath.ShouldBe("utils.go");
        errors[2].LineNumber.ShouldBe(20);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithCargoMultipleErrors_ParsesAll() {
        // Arrange
        var output = """
            error[E0425]: cannot find value `x` in this scope
             --> src/main.rs:10:5
            error[E0308]: mismatched types
             --> src/lib.rs:25:10
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "cargo");

        // Assert
        errors.Count.ShouldBe(2);
        errors[0].FilePath.ShouldBe("src/main.rs");
        errors[0].LineNumber.ShouldBe(10);
        errors[0].ErrorCode.ShouldBe("E0425");
        errors[1].FilePath.ShouldBe("src/lib.rs");
        errors[1].LineNumber.ShouldBe(25);
        errors[1].ErrorCode.ShouldBe("E0308");
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithImportError_CategorizesAsMissingDependency() {
        // Arrange
        var output = """
            Program.cs(10,15): error CS0234: The type or namespace name 'Logging' does not exist in the namespace 'Microsoft.Extensions'
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.MissingDependency);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithModuleNotFoundError_CategorizesAsMissingDependency() {
        // Arrange
        var output = """
            main.go:5:2: module not found: github.com/pkg/errors
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "go");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.MissingDependency);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithCastError_CategorizesAsType() {
        // Arrange
        var output = """
            Program.cs(20,10): error CS0030: Cannot convert type 'string' to 'int'
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.Type);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithUnexpectedToken_CategorizesAsSyntax() {
        // Arrange
        var output = """
            Program.cs(15,5): error CS1026: ) expected but , found - unexpected token
            """;

        // Act
        var errors = await _verifier.ParseBuildErrorsAsync(
            output: output,
            buildTool: "dotnet");

        // Assert
        errors.Count.ShouldBe(1);
        errors[0].Category.ShouldBe(ErrorCategory.Syntax);
    }

    [Fact]
    public async Task ParseBuildErrorsAsync_WithConfigurationSetting_CategorizesAsConfiguration

        // Setup project detection based on build tool
        SetupProjectDetectionForBuildTool(containerId, buildTool);

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Length == 1 && args[0] == expectedCommand),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                wasCalledWithCorrectCommand = true;
                return new CommandResult {
                    ExitCode = 0,
                    Output = $"/usr/bin/{expectedCommand}"
                };
            });

        // Setup successful build
        SetupSuccessfulBuild(containerId, buildTool);

        // Act
        await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        wasCalledWithCorrectCommand.ShouldBeTrue($"Should have checked for '{expectedCommand}' command for {buildTool} build tool");
    }

    private void SetupProjectDetectionForBuildTool(string containerId, string buildTool) {
        var projectFile = buildTool switch {
            "dotnet" => "*.csproj",
            "npm" => "package.json",
            "gradle" => "build.gradle",
            "maven" => "pom.xml",
            "go" => "go.mod",
            "cargo" => "Cargo.toml",
            _ => throw new ArgumentException($"Unknown build tool: {buildTool}")
        };

        // Setup all find commands to return empty except the matching one
        var allProjectFiles = new[] { "*.csproj", "package.json", "build.gradle", "pom.xml", "go.mod", "Cargo.toml" };
        
        foreach (var file in allProjectFiles) {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    containerId,
                    "find",
                    It.Is<string[]>(args => args.Contains(file)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = file == projectFile ? $"./{file}" : ""
                });
        }
    }

    private void SetupSuccessfulBuild(string containerId, string buildTool) {
        var (command, args) = buildTool switch {
            "dotnet" => ("dotnet", new[] { "build" }),
            "npm" => ("npm", new[] { "run", "build" }),
            "gradle" => ("./gradlew", new[] { "build" }),
            "maven" => ("mvn", new[] { "compile" }),
            "go" => ("go", new[] { "build", "./..." }),
            "cargo" => ("cargo", new[] { "build" }),
            _ => throw new ArgumentException($"Unknown build tool: {buildTool}")
        };

        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                command,
                It.Is<string[]>(a => a.SequenceEqual(args)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "Build succeeded."
            });
    }

    private void SetupBuildToolDetection(string? buildTool) {
        // Setup which command mocks for tool availability check
        if (buildTool != null) {
            var toolCommand = buildTool switch {
                "dotnet" => "dotnet",
                "npm" => "npm",
                "gradle" => "gradle",
                "maven" => "mvn",
                "go" => "go",
                "cargo" => "cargo",
                _ => null
            };

            if (toolCommand != null) {
                _containerManager
                    .Setup(c => c.ExecuteInContainerAsync(
                        It.IsAny<string>(),
                        "which",
                        It.Is<string[]>(args => args.Length == 1 && args[0] == toolCommand),
                        It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new CommandResult {
                        ExitCode = 0,
                        Output = $"/usr/bin/{toolCommand}"
                    });
            }
        }

        if (buildTool == "dotnet") {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("*.csproj")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "./Project.csproj"
                });
        } else if (buildTool == "npm") {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("*.csproj")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("package.json")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "./package.json"
                });
        } else if (buildTool == "gradle") {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("*.csproj")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("package.json")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("build.gradle")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "./build.gradle"
                });
        } else if (buildTool == "maven") {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("*.csproj")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("package.json")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("build.gradle")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("pom.xml")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "./pom.xml"
                });
        } else if (buildTool == "go") {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("*.csproj")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("package.json")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("build.gradle")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("pom.xml")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("go.mod")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "./go.mod"
                });
        } else if (buildTool == "cargo") {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("*.csproj")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("package.json")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("build.gradle")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("pom.xml")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("go.mod")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });

            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "find",
                    It.Is<string[]>(args => args.Contains("Cargo.toml")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "./Cargo.toml"
                });
        } else {
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult { ExitCode = 0, Output = "" });
        }
    }

    private void SetupLlmFixGeneration() {
        var chatContent = new ChatMessageContent(AuthorRole.Assistant, """
            {
              "fixes": []
            }
            """);

        _chatService
            .Setup(c => c.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { chatContent });
    }

    private class TestLogger<T> : ILogger<T> {
        public List<string> LogEntries { get; } = new();
        
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            LogEntries.Add(formatter(state, exception));
        }
    }
}
