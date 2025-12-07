using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for build tools verification that use actual Docker containers.
/// These tests require Docker to be installed and running.
/// </summary>
public class BuildToolsVerificationIntegrationTests : IDisposable {
    private readonly DockerContainerManager _containerManager;
    private readonly string _dotnetContainerId;
    private readonly string _nodeContainerId;
    private readonly string _javaContainerId;
    private readonly string _goContainerId;
    private readonly string _rustContainerId;
    private readonly ILogger<DockerContainerManager> _logger;

    public BuildToolsVerificationIntegrationTests() {
        var commandExecutor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        _logger = new TestLogger<DockerContainerManager>();
        _containerManager = new DockerContainerManager(commandExecutor, _logger);
        
        // Create test containers for different image types
        _dotnetContainerId = CreateTestContainerAsync("mcr.microsoft.com/dotnet/sdk:10.0").GetAwaiter().GetResult();
        _nodeContainerId = CreateTestContainerAsync("node:20-bookworm").GetAwaiter().GetResult();
        _javaContainerId = CreateTestContainerAsync("eclipse-temurin:21-jdk").GetAwaiter().GetResult();
        _goContainerId = CreateTestContainerAsync("golang:1.22-bookworm").GetAwaiter().GetResult();
        _rustContainerId = CreateTestContainerAsync("rust:1-bookworm").GetAwaiter().GetResult();
    }

    private async Task<string> CreateTestContainerAsync(string image) {
        var commandExecutor = new ProcessCommandExecutor(new TestLogger<ProcessCommandExecutor>());
        var containerName = $"opencopilot-buildtools-test-{Guid.NewGuid():N}".ToLowerInvariant();

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
        // Cleanup test containers
        try {
            _containerManager.CleanupContainerAsync(_dotnetContainerId).GetAwaiter().GetResult();
            _containerManager.CleanupContainerAsync(_nodeContainerId).GetAwaiter().GetResult();
            _containerManager.CleanupContainerAsync(_javaContainerId).GetAwaiter().GetResult();
            _containerManager.CleanupContainerAsync(_goContainerId).GetAwaiter().GetResult();
            _containerManager.CleanupContainerAsync(_rustContainerId).GetAwaiter().GetResult();
        }
        catch {
            // Ignore cleanup errors
        }
    }

    [Fact(Skip = "Integration test - requires Docker")]
    public async Task VerifyBuildToolsAsync_IntegrationTest_DotNetContainer_HasDotnet() {
        // Act
        var status = await _containerManager.VerifyBuildToolsAsync(containerId: _dotnetContainerId);

        // Assert
        status.ShouldNotBeNull();
        status.DotnetAvailable.ShouldBeTrue();
        status.MissingTools.ShouldNotContain("dotnet");
    }

    [Fact(Skip = "Integration test - requires Docker")]
    public async Task VerifyBuildToolsAsync_IntegrationTest_NodeContainer_HasNpm() {
        // Act
        var status = await _containerManager.VerifyBuildToolsAsync(containerId: _nodeContainerId);

        // Assert
        status.ShouldNotBeNull();
        status.NpmAvailable.ShouldBeTrue();
        status.MissingTools.ShouldNotContain("npm");
    }

    [Fact(Skip = "Integration test - requires Docker")]
    public async Task VerifyBuildToolsAsync_IntegrationTest_JavaContainer_HasMaven() {
        // Act
        var status = await _containerManager.VerifyBuildToolsAsync(containerId: _javaContainerId);

        // Assert
        status.ShouldNotBeNull();
        status.MavenAvailable.ShouldBeFalse(); // Maven is not pre-installed in eclipse-temurin
        status.MissingTools.ShouldContain("maven");
    }

    [Fact(Skip = "Integration test - requires Docker")]
    public async Task VerifyBuildToolsAsync_IntegrationTest_GoContainer_HasGo() {
        // Act
        var status = await _containerManager.VerifyBuildToolsAsync(containerId: _goContainerId);

        // Assert
        status.ShouldNotBeNull();
        status.GoAvailable.ShouldBeTrue();
        status.MissingTools.ShouldNotContain("go");
    }

    [Fact(Skip = "Integration test - requires Docker")]
    public async Task VerifyBuildToolsAsync_IntegrationTest_RustContainer_HasCargo() {
        // Act
        var status = await _containerManager.VerifyBuildToolsAsync(containerId: _rustContainerId);

        // Assert
        status.ShouldNotBeNull();
        status.CargoAvailable.ShouldBeTrue();
        status.MissingTools.ShouldNotContain("cargo");
    }

    [Fact(Skip = "Integration test - requires Docker")]
    public async Task VerifyBuildToolsAsync_IntegrationTest_AllContainers_ReportMissingTools() {
        // Act
        var dotnetStatus = await _containerManager.VerifyBuildToolsAsync(containerId: _dotnetContainerId);
        var nodeStatus = await _containerManager.VerifyBuildToolsAsync(containerId: _nodeContainerId);
        var javaStatus = await _containerManager.VerifyBuildToolsAsync(containerId: _javaContainerId);
        var goStatus = await _containerManager.VerifyBuildToolsAsync(containerId: _goContainerId);
        var rustStatus = await _containerManager.VerifyBuildToolsAsync(containerId: _rustContainerId);

        // Assert - each container should have some tools missing
        dotnetStatus.MissingTools.ShouldNotBeEmpty();
        nodeStatus.MissingTools.ShouldNotBeEmpty();
        javaStatus.MissingTools.ShouldNotBeEmpty();
        goStatus.MissingTools.ShouldNotBeEmpty();
        rustStatus.MissingTools.ShouldNotBeEmpty();
    }

    // Test helper class
    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
