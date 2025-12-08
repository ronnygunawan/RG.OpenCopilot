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
    public async Task RunBuildAsync_WithMissingDotnetTool_ReturnsToolNotAvailableError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        // Mock tool not available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("dotnet")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "dotnet not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("is not available");
        result.Error.ShouldContain("dotnet");
        result.Error.ShouldContain("Install .NET SDK");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingNpmTool_ReturnsToolNotAvailableError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "npm");
        
        // Mock tool not available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("npm")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "npm not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("is not available");
        result.Error.ShouldContain("npm");
        result.Error.ShouldContain("Node.js");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingGradleTool_ReturnsToolNotAvailableError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "gradle");
        
        // Mock tool not available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("gradle")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "gradle not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("is not available");
        result.Error.ShouldContain("gradle");
        result.Error.ShouldContain("Gradle");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingMavenTool_ReturnsToolNotAvailableError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "maven");
        
        // Mock tool not available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("mvn")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "mvn not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("is not available");
        result.Error.ShouldContain("maven");
        result.Error.ShouldContain("Maven");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingGoTool_ReturnsToolNotAvailableError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "go");
        
        // Mock tool not available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("go")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "go not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("is not available");
        result.Error.ShouldContain("go");
        result.Error.ShouldContain("Install Go");
    }

    [Fact]
    public async Task RunBuildAsync_WithMissingCargoTool_ReturnsToolNotAvailableError() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "cargo");
        
        // Mock tool not available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("cargo")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "cargo not found"
            });

        // Act
        var result = await _verifier.RunBuildAsync(containerId: containerId);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldContain("is not available");
        result.Error.ShouldContain("cargo");
        result.Error.ShouldContain("Rust");
    }

    [Fact]
    public async Task RunBuildAsync_WithAvailableDotnetTool_ProceedsToBuild() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        // Mock tool available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("dotnet")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "/usr/bin/dotnet"
            });
        
        // Mock successful build
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
        
        // Verify that both which and build commands were called
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "which",
            It.Is<string[]>(args => args.Contains("dotnet")),
            It.IsAny<CancellationToken>()), Times.Once);
        
        _containerManager.Verify(c => c.ExecuteInContainerAsync(
            containerId,
            "dotnet",
            It.Is<string[]>(args => args.Contains("build")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithMissingTool_ReturnsToolNotAvailableInResult() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        // Mock tool not available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("dotnet")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 1,
                Output = "",
                Error = "dotnet not found"
            });

        // Act
        var result = await _verifier.VerifyBuildAsync(
            containerId: containerId,
            maxRetries: 3);

        // Assert
        result.Success.ShouldBeFalse();
        result.ToolAvailable.ShouldBe(false);
        result.MissingTool.ShouldBe("dotnet");
        result.Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task VerifyBuildAsync_WithAvailableTool_SetsToolAvailableToTrue() {
        // Arrange
        var containerId = "test-container";
        SetupBuildToolDetection(buildTool: "dotnet");
        
        // Mock tool available
        _containerManager
            .Setup(c => c.ExecuteInContainerAsync(
                containerId,
                "which",
                It.Is<string[]>(args => args.Contains("dotnet")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandResult {
                ExitCode = 0,
                Output = "/usr/bin/dotnet"
            });
        
        // Mock successful build
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
        result.ToolAvailable.ShouldBe(true);
        result.MissingTool.ShouldBeNull();
        result.Attempts.ShouldBe(1);
    }

    private void SetupBuildToolDetection(string? buildTool) {
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
            
            // Setup which command for tool availability check
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "which",
                    It.Is<string[]>(args => args.Contains("dotnet")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "/usr/bin/dotnet"
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
            
            // Setup which command for tool availability check
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "which",
                    It.Is<string[]>(args => args.Contains("npm")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "/usr/bin/npm"
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
            
            // Setup which command for tool availability check
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "which",
                    It.Is<string[]>(args => args.Contains("gradle")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "/usr/bin/gradle"
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
            
            // Setup which command for tool availability check
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "which",
                    It.Is<string[]>(args => args.Contains("mvn")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "/usr/bin/mvn"
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
            
            // Setup which command for tool availability check
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "which",
                    It.Is<string[]>(args => args.Contains("go")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "/usr/bin/go"
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
            
            // Setup which command for tool availability check
            _containerManager
                .Setup(c => c.ExecuteInContainerAsync(
                    It.IsAny<string>(),
                    "which",
                    It.Is<string[]>(args => args.Contains("cargo")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandResult {
                    ExitCode = 0,
                    Output = "/usr/bin/cargo"
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
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
