using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Models;
using RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.DependencyManagement;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class DependencyManagerTests {
    [Fact]
    public async Task DetectPackageManagerAsync_CSharpProject_ReturnsNuGet() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/MyApp.csproj"
        }, pattern: "*.csproj");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.NuGet);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_NodeProject_ReturnsNpm() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/package.json"
        }, pattern: "package.json");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.Npm);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_PythonProject_ReturnsPip() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "package.json");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/requirements.txt"
        }, pattern: "requirements.txt");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.Pip);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_MavenProject_ReturnsMaven() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "package.json");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "requirements.txt");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/pom.xml"
        }, pattern: "pom.xml");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.Maven);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_GradleProject_ReturnsGradle() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "package.json");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "requirements.txt");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "pom.xml");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/build.gradle"
        }, pattern: "build.gradle");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.Gradle);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_CargoProject_ReturnsCargo() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "package.json");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "requirements.txt");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "pom.xml");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "build.gradle");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "build.gradle.kts");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/Cargo.toml"
        }, pattern: "Cargo.toml");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.Cargo);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_GoProject_ReturnsGoModules() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "package.json");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "requirements.txt");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "pom.xml");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "build.gradle");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "build.gradle.kts");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "Cargo.toml");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/go.mod"
        }, pattern: "go.mod");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.GoModules);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_ComposerProject_ReturnsComposer() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "package.json");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "requirements.txt");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "pom.xml");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "build.gradle");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "build.gradle.kts");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "Cargo.toml");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "go.mod");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/composer.json"
        }, pattern: "composer.json");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.Composer);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_RubyProject_ReturnsRubyGems() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetupNoPackageFiles();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/Gemfile"
        }, pattern: "Gemfile");
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBe(PackageManager.RubyGems);
    }

    [Fact]
    public async Task DetectPackageManagerAsync_NoPackageFile_ReturnsNull() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetupNoPackageFiles();
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.DetectPackageManagerAsync("test-container");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task AddDependencyAsync_NuGetPackage_ReturnsSuccess() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/MyApp.csproj"
        });
        containerManager.SetExecuteResult("dotnet", new CommandResult {
            ExitCode = 0,
            Output = "Package 'Newtonsoft.Json' added successfully"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "Newtonsoft.Json",
            version: "13.0.3");

        // Assert
        result.Success.ShouldBeTrue();
        result.InstalledPackage.ShouldNotBeNull();
        result.InstalledPackage.Name.ShouldBe("Newtonsoft.Json");
        result.InstalledPackage.Version.ShouldBe("13.0.3");
        result.InstalledPackage.Manager.ShouldBe(PackageManager.NuGet);
    }

    [Fact]
    public async Task AddDependencyAsync_NpmPackage_ReturnsSuccess() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/package.json"
        }, pattern: "package.json");
        containerManager.SetExecuteResult("npm", new CommandResult {
            ExitCode = 0,
            Output = "added 1 package"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "express",
            version: "4.18.2");

        // Assert
        result.Success.ShouldBeTrue();
        result.InstalledPackage.ShouldNotBeNull();
        result.InstalledPackage.Name.ShouldBe("express");
        result.InstalledPackage.Version.ShouldBe("4.18.2");
        result.InstalledPackage.Manager.ShouldBe(PackageManager.Npm);
    }

    [Fact]
    public async Task AddDependencyAsync_PipPackage_ReturnsSuccess() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "*.csproj");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        }, pattern: "package.json");
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/requirements.txt"
        }, pattern: "requirements.txt");
        containerManager.SetExecuteResult("pip", new CommandResult {
            ExitCode = 0,
            Output = "Successfully installed requests-2.31.0"
        });
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = ""
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "requests",
            version: "2.31.0");

        // Assert
        result.Success.ShouldBeTrue();
        result.InstalledPackage.ShouldNotBeNull();
        result.InstalledPackage.Name.ShouldBe("requests");
        result.InstalledPackage.Version.ShouldBe("2.31.0");
        result.InstalledPackage.Manager.ShouldBe(PackageManager.Pip);
    }

    [Fact]
    public async Task AddDependencyAsync_CargoPackage_ReturnsSuccess() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetupNoPackageFiles();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/Cargo.toml"
        }, pattern: "Cargo.toml");
        containerManager.SetExecuteResult("cargo", new CommandResult {
            ExitCode = 0,
            Output = "Adding serde v1.0.0 to dependencies"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "serde",
            version: "1.0.0");

        // Assert
        result.Success.ShouldBeTrue();
        result.InstalledPackage.ShouldNotBeNull();
        result.InstalledPackage.Name.ShouldBe("serde");
        result.InstalledPackage.Version.ShouldBe("1.0.0");
        result.InstalledPackage.Manager.ShouldBe(PackageManager.Cargo);
    }

    [Fact]
    public async Task AddDependencyAsync_GoPackage_ReturnsSuccess() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetupNoPackageFiles();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/go.mod"
        }, pattern: "go.mod");
        containerManager.SetExecuteResult("go", new CommandResult {
            ExitCode = 0,
            Output = "go: added github.com/gin-gonic/gin v1.9.0"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "github.com/gin-gonic/gin",
            version: "v1.9.0");

        // Assert
        result.Success.ShouldBeTrue();
        result.InstalledPackage.ShouldNotBeNull();
        result.InstalledPackage.Name.ShouldBe("github.com/gin-gonic/gin");
        result.InstalledPackage.Version.ShouldBe("v1.9.0");
        result.InstalledPackage.Manager.ShouldBe(PackageManager.GoModules);
    }

    [Fact]
    public async Task AddDependencyAsync_WithoutVersion_InstallsLatest() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/package.json"
        }, pattern: "package.json");
        containerManager.SetExecuteResult("npm", new CommandResult {
            ExitCode = 0,
            Output = "added 1 package"
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.AddDependencyAsync(
            containerId: "test-container",
            packageName: "lodash");

        // Assert
        result.Success.ShouldBeTrue();
        result.InstalledPackage.ShouldNotBeNull();
        result.InstalledPackage.Name.ShouldBe("lodash");
        result.InstalledPackage.Version.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveVersionConflictsAsync_SingleVersion_ReturnsResolved() {
        // Arrange
        var dependencies = new List<Package> {
            new Package { Name = "lodash", Version = "4.17.21", Manager = PackageManager.Npm },
            new Package { Name = "express", Version = "4.18.2", Manager = PackageManager.Npm }
        };
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(new TestContainerManagerForDependencyManager(), kernel, logger);

        // Act
        var result = await manager.ResolveVersionConflictsAsync(dependencies);

        // Assert
        result.Success.ShouldBeTrue();
        result.ResolvedPackages.Count.ShouldBe(2);
        result.RemainingConflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveVersionConflictsAsync_MultipleVersions_ResolvesToLatest() {
        // Arrange
        var dependencies = new List<Package> {
            new Package { Name = "lodash", Version = "4.17.20", Manager = PackageManager.Npm },
            new Package { Name = "lodash", Version = "4.17.21", Manager = PackageManager.Npm },
            new Package { Name = "lodash", Version = "4.17.19", Manager = PackageManager.Npm }
        };
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(new TestContainerManagerForDependencyManager(), kernel, logger);

        // Act
        var result = await manager.ResolveVersionConflictsAsync(dependencies);

        // Assert
        result.Success.ShouldBeTrue();
        result.ResolvedPackages.Count.ShouldBe(1);
        result.ResolvedPackages[0].Version.ShouldBe("4.17.21");
        result.RemainingConflicts.Count.ShouldBe(2);
        result.RemainingConflicts.All(c => c.Severity == ConflictSeverity.Warning).ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveVersionConflictsAsync_EmptyList_ReturnsEmptyResolution() {
        // Arrange
        var dependencies = new List<Package>();
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(new TestContainerManagerForDependencyManager(), kernel, logger);

        // Act
        var result = await manager.ResolveVersionConflictsAsync(dependencies);

        // Assert
        result.Success.ShouldBeTrue();
        result.ResolvedPackages.ShouldBeEmpty();
        result.RemainingConflicts.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListInstalledPackagesAsync_NuGetPackages_ReturnsPackageList() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetExecuteResult("sh", new CommandResult {
            ExitCode = 0,
            Output = "/workspace/MyApp.csproj"
        });
        containerManager.SetExecuteResult("dotnet", new CommandResult {
            ExitCode = 0,
            Output = """
                Package                           Version
                --------------------------------  -------
                Newtonsoft.Json                   13.0.3
                Microsoft.Extensions.Logging      8.0.0
                """
        });
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.ListInstalledPackagesAsync("test-container");

        // Assert
        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Newtonsoft.Json");
        result[0].Version.ShouldBe("13.0.3");
        result[1].Name.ShouldBe("Microsoft.Extensions.Logging");
        result[1].Version.ShouldBe("8.0.0");
    }

    [Fact]
    public async Task ListInstalledPackagesAsync_NoPackageManager_ReturnsEmptyList() {
        // Arrange
        var containerManager = new TestContainerManagerForDependencyManager();
        containerManager.SetupNoPackageFiles();
        var kernel = CreateMockKernel();
        var logger = new TestLogger<DependencyManager>();
        var manager = new DependencyManager(containerManager, kernel, logger);

        // Act
        var result = await manager.ListInstalledPackagesAsync("test-container");

        // Assert
        result.ShouldBeEmpty();
    }

    private static Kernel CreateMockKernel() {
        var kernelBuilder = Kernel.CreateBuilder();
        return kernelBuilder.Build();
    }

    private class TestContainerManagerForDependencyManager : IContainerManager {
        private readonly Dictionary<(string command, string? pattern), CommandResult> _executeResults = new();

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
            
            // Determine pattern from args for find commands
            string? pattern = null;
            if (command == "sh" && args.Length > 1 && args[1].Contains("sh")) {
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
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
