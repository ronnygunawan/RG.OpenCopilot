using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Models;
using RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.DependencyManagement;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class DependencyManagerNegativeTests {
    [Fact]
    public async Task AddDependencyAsync_NoPackageManagerDetected_ReturnsFailure() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        containerManager.SetupNoPackageFiles();
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "some-package");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Could not detect package manager");
    }

    [Fact]
    public async Task AddDependencyAsync_InstallationFails_ReturnsFailure() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        // Setup detection to find package.json
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "packages.config");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/package.json"
        }, pattern: "package.json");
        containerManager.SetExecuteResult("npm", new CommandResult {
            ExitCode = 1,
            Error = "Package not found"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "nonexistent-package");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Failed to install package");
    }

    [Fact]
    public async Task AddDependencyAsync_EmptyPackageName_ReturnsFailure() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "packages.config");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/package.json"
        }, pattern: "package.json");
        containerManager.SetExecuteResult("npm", new CommandResult {
            ExitCode = 1,
            Error = "Package name required"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task AddDependencyAsync_InvalidContainerId_ReturnsFailure() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        containerManager.ThrowOnExecute = true;
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "invalid-container",
            packageName: "some-package");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListInstalledPackagesAsync_CommandFails_ReturnsEmptyList() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "packages.config");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/package.json"
        }, pattern: "package.json");
        containerManager.SetExecuteResult("npm", new CommandResult {
            ExitCode = 1,
            Error = "Failed to list packages"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);

        // Act
        var result = await manager.ListInstalledPackagesAsync("test-container");

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecommendPackagesAsync_LlmReturnsInvalidJson_ReturnsEmptyList() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);

        // Act - With an unconfigured kernel, the call will fail
        var result = await manager.RecommendPackagesAsync(
            requirement: "need JSON parsing",
            language: "C#");

        // Assert - Should return empty list on error
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateDependencyFileAsync_FileUpdateFails_LogsWarning() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 1,
            Error = "Permission denied"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);
        var package = new Package {
            Name = "requests",
            Version = "2.31.0",
            Manager = PackageManager.Pip
        };

        // Act
        await manager.UpdateDependencyFileAsync("test-container", package);

        // Assert - Should not throw, just log warning
        logger.WarningCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ResolveVersionConflictsAsync_NullVersions_HandlesGracefully() {
        // Arrange
        var dependencies = new List<Package> {
            new Package { Name = "lodash", Version = null, Manager = PackageManager.Npm },
            new Package { Name = "lodash", Version = "4.17.21", Manager = PackageManager.Npm }
        };
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(new TestContainerManagerForDependencyManagerNegative(), new ExecutorKernel(kernel), logger);

        // Act
        var result = await manager.ResolveVersionConflictsAsync(dependencies);

        // Assert
        result.Success.ShouldBeTrue();
        result.ResolvedPackages.Count.ShouldBe(1);
        result.ResolvedPackages[0].Version.ShouldBe("4.17.21");
    }

    [Fact]
    public async Task DetectPackageManagerAsync_MultiplePackageFiles_PrefersFirst() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManagerNegative();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/MyApp.csproj"
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/package.json"
        }, pattern: "package.json");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, new ExecutorKernel(kernel), logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert - Should prefer NuGet (first in detection order)
        result.ShouldBe(PackageManager.NuGet);
    }

    private static Kernel CreateMockKernel() {
        var kernelBuilder = Kernel.CreateBuilder();
        return kernelBuilder.Build();
    }

    private class TestContainerManagerForDependencyManagerNegative : IContainerManager {
        private readonly Dictionary<(string command, string? pattern), CommandResult> _executeResults = new();
        public bool ThrowOnExecute { get; set; }

        public void SetExecuteResult(string command, CommandResult result, string? pattern = null) {
            _executeResults[(command, pattern)] = result;
        }

        public void SetupNoPackageFiles() {
            var emptyResult = new CommandResult { ExitCode = 0, Output = "" };
            SetExecuteResult("sh", emptyResult, pattern: "*.csproj");
            SetExecuteResult("sh", emptyResult, pattern: "packages.config");
            SetExecuteResult("sh", emptyResult, pattern: "package.json");
            SetExecuteResult("sh", emptyResult, pattern: "requirements.txt");
            SetExecuteResult("sh", emptyResult, pattern: "Pipfile");
            SetExecuteResult("sh", emptyResult, pattern: "pom.xml");
            SetExecuteResult("sh", emptyResult, pattern: "build.gradle");
            SetExecuteResult("sh", emptyResult, pattern: "build.gradle.kts");
            SetExecuteResult("sh", emptyResult, pattern: "Cargo.toml");
            SetExecuteResult("sh", emptyResult, pattern: "go.mod");
            SetExecuteResult("sh", emptyResult, pattern: "composer.json");
            SetExecuteResult("sh", emptyResult, pattern: "Gemfile");
        }

        public Task<CommandResult> ExecuteInContainerAsync(
            string containerId,
            string command,
            string[] args,
            CancellationToken cancellationToken = default) {
            
            if (ThrowOnExecute) {
                throw new InvalidOperationException("Container execution failed");
            }

            // Determine pattern from args for find commands
            string? pattern = null;
            if (command == "sh" && args.Length > 1 && args[1].Contains("find")) {
                var findCmd = args[1];
                var nameIndex = findCmd.IndexOf("-name");
                if (nameIndex >= 0) {
                    var startQuote = findCmd.IndexOf('\'', nameIndex);
                    var endQuote = findCmd.IndexOf('\'', startQuote + 1);
                    if (startQuote >= 0 && endQuote > startQuote) {
                        pattern = findCmd.Substring(startQuote + 1, endQuote - startQuote - 1);
                    }
                }
            }

            if (_executeResults.TryGetValue((command, pattern), out var result)) {
                return Task.FromResult(result);
            }

            if (_executeResults.TryGetValue((command, null), out var defaultResult)) {
                return Task.FromResult(defaultResult);
            }

            return Task.FromResult(new CommandResult { ExitCode = 0, Output = "" });
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container");
        }

        public Task<string> CreateContainerAsync(string owner, string repo, string token, string branch, ContainerImageType imageType, CancellationToken cancellationToken = default) {
            return Task.FromResult("test-container");
        }

        public Task<string> ReadFileInContainerAsync(string containerId, string filePath, CancellationToken cancellationToken = default) {
            return Task.FromResult("");
        }

        public Task WriteFileInContainerAsync(string containerId, string filePath, string content, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CommitAndPushAsync(string containerId, string commitMessage, string owner, string repo, string branch, string token, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task CleanupContainerAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<BuildToolsStatus> VerifyBuildToolsAsync(string containerId, CancellationToken cancellationToken = default) {
            return Task.FromResult(new BuildToolsStatus());
        }

        public Task CreateDirectoryAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task<bool> DirectoryExistsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(true);
        }

        public Task<List<string>> ListContentsAsync(string containerId, string dirPath, CancellationToken cancellationToken = default) {
            return Task.FromResult(new List<string>());
        }

        public Task CopyAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string containerId, string path, bool recursive = false, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }

        public Task MoveAsync(string containerId, string source, string dest, CancellationToken cancellationToken = default) {
            return Task.CompletedTask;
        }
    }

    private class TestLogger<T> : ILogger<T> {
        public int WarningCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (logLevel == LogLevel.Warning) {
                WarningCount++;
            }
        }
    }
}
