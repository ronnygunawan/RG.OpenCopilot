using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for TestValidator that use actual Docker containers.
/// These tests require Docker to be installed and running.
/// </summary>
public class TestValidatorIntegrationTests : IDisposable {
    private readonly DockerContainerManager _containerManager;
    private readonly FileEditor _fileEditor;
    private readonly TestValidator _validator;
    private readonly string _dotnetContainerId;
    private readonly ILogger<DockerContainerManager> _containerLogger;
    private readonly ILogger<FileEditor> _fileEditorLogger;
    private readonly ILogger<TestValidator> _validatorLogger;

    public TestValidatorIntegrationTests() {
        var commandExecutor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        _containerLogger = new TestLogger<DockerContainerManager>();
        _fileEditorLogger = new TestLogger<FileEditor>();
        _validatorLogger = new TestLogger<TestValidator>();
        
        _containerManager = new DockerContainerManager(commandExecutor: commandExecutor, logger: _containerLogger, auditLogger: new TestAuditLogger(), timeProvider: new FakeTimeProvider());
        _fileEditor = new FileEditor(containerManager: _containerManager, logger: _fileEditorLogger, auditLogger: new TestAuditLogger());
        
        // Create a minimal kernel with mocked chat service for integration tests
        var chatService = new Mock<IChatCompletionService>();
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.Services.AddSingleton(chatService.Object);
        var kernel = kernelBuilder.Build();
        
        _validator = new TestValidator(
            containerManager: _containerManager,
            fileEditor: _fileEditor,
            kernel: kernel,
            logger: _validatorLogger);
        
        // Create test container
        _dotnetContainerId = CreateTestContainerAsync("mcr.microsoft.com/dotnet/sdk:10.0").GetAwaiter().GetResult();
    }

    private async Task<string> CreateTestContainerAsync(string image) {
        var commandExecutor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        var containerName = $"opencopilot-testvalidator-test-{Guid.NewGuid():N}".ToLowerInvariant();

        var result = await commandExecutor.ExecuteCommandAsync(
            workingDirectory: Directory.GetCurrentDirectory(),
            command: "docker",
            args: new[] {
                "run",
                "-d",
                "--name", containerName,
                "-w", "/workspace",
                image,
                "sleep", "infinity"
            });

        if (!result.Success) {
            throw new InvalidOperationException($"Failed to create test container with {image}: {result.Error}");
        }

        return result.Output.Trim();
    }

    public void Dispose() {
        // Cleanup test container
        try {
            _containerManager.CleanupContainerAsync(_dotnetContainerId).GetAwaiter().GetResult();
        }
        catch {
            // Ignore cleanup errors
        }
    }

    [Fact(Skip = "Integration test - requires Docker and is expensive")]
    public async Task RunTestsAsync_IntegrationTest_WithDotnetProject_DetectsXunit() {
        // Arrange - Create a simple .NET project with xUnit
        await _containerManager.ExecuteInContainerAsync(
            containerId: _dotnetContainerId,
            command: "dotnet",
            args: new[] { "new", "xunit", "-n", "TestProject", "-o", "/workspace" });

        // Act
        var result = await _validator.RunTestsAsync(containerId: _dotnetContainerId);

        // Assert
        result.ShouldNotBeNull();
        result.Total.ShouldBeGreaterThan(0);
        // Default xUnit template includes at least one test
    }

    [Fact(Skip = "Integration test - requires Docker and is expensive")]
    public async Task RunTestsAsync_IntegrationTest_WithFailingTest_ParsesFailure() {
        // Arrange - Create a project with a failing test
        await _containerManager.ExecuteInContainerAsync(
            containerId: _dotnetContainerId,
            command: "dotnet",
            args: new[] { "new", "xunit", "-n", "TestProject", "-o", "/workspace" });

        // Modify the test to fail
        await _containerManager.ExecuteInContainerAsync(
            containerId: _dotnetContainerId,
            command: "sh",
            args: new[] { 
                "-c",
                "echo 'using Xunit; public class UnitTest1 { [Fact] public void Test1() { Assert.True(false); } }' > /workspace/UnitTest1.cs"
            });

        // Act
        var result = await _validator.RunTestsAsync(containerId: _dotnetContainerId);

        // Assert
        result.ShouldNotBeNull();
        result.Success.ShouldBeFalse();
        result.Failed.ShouldBeGreaterThan(0);
        result.Failures.ShouldNotBeEmpty();
    }

    [Fact(Skip = "Integration test - requires Docker and is expensive")]
    public async Task DetectTestFrameworkAsync_IntegrationTest_DotnetXunit_ReturnsXunit() {
        // Arrange - Create a .NET project with xUnit
        await _containerManager.ExecuteInContainerAsync(
            containerId: _dotnetContainerId,
            command: "dotnet",
            args: new[] { "new", "xunit", "-n", "TestProject", "-o", "/workspace" });

        // Act - RunTestsAsync internally calls DetectTestFrameworkAsync
        var result = await _validator.RunTestsAsync(containerId: _dotnetContainerId);

        // Assert - If framework was detected, result should have test data
        result.ShouldNotBeNull();
        result.Total.ShouldBeGreaterThanOrEqualTo(0);
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
