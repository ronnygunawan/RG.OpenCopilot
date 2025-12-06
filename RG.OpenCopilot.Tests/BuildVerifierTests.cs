using Moq;
using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App.Docker;
using RG.OpenCopilot.App.Executor;
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
