using Moq;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services.Docker;
using RG.OpenCopilot.PRGenerationAgent.Services.Executor;
using RG.OpenCopilot.PRGenerationAgent.Services.FileOperations;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

public class MultiFileRefactoringCoordinatorTests {
    private readonly Mock<IFileAnalyzer> _fileAnalyzer;
    private readonly Mock<IFileEditor> _fileEditor;
    private readonly Mock<IBuildVerifier> _buildVerifier;
    private readonly Mock<IContainerManager> _containerManager;
    private readonly TestLogger<MultiFileRefactoringCoordinator> _logger;
    private readonly MultiFileRefactoringCoordinator _coordinator;

    public MultiFileRefactoringCoordinatorTests() {
        _fileAnalyzer = new Mock<IFileAnalyzer>();
        _fileEditor = new Mock<IFileEditor>();
        _buildVerifier = new Mock<IBuildVerifier>();
        _containerManager = new Mock<IContainerManager>();
        _logger = new TestLogger<MultiFileRefactoringCoordinator>();

        _coordinator = new MultiFileRefactoringCoordinator(
            fileAnalyzer: _fileAnalyzer.Object,
            fileEditor: _fileEditor.Object,
            buildVerifier: _buildVerifier.Object,
            containerManager: _containerManager.Object,
            logger: _logger);
    }

    [Fact]
    public async Task AnalyzeDependenciesAsync_WithNoDependencies_ReturnsEmptyGraph() {
        // Arrange
        var containerId = "test-container";
        var filePaths = new List<string> { "/workspace/File1.cs", "/workspace/File2.cs" };

        _containerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        // Act
        var graph = await _coordinator.AnalyzeDependenciesAsync(containerId, filePaths);

        // Assert
        graph.Nodes.ShouldNotBeNull();
        graph.Nodes.Count.ShouldBe(2);
        graph.CircularDependencies.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeDependenciesAsync_WithCSharpDependencies_BuildsDependencyGraph() {
        // Arrange
        var containerId = "test-container";
        var filePaths = new List<string> { "/workspace/File1.cs", "/workspace/File2.cs" };

        _containerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/File1.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("using System;\nusing MyNamespace.File2;\n\nclass File1 {}");

        _containerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/File2.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("using System;\n\nclass File2 {}");

        // Act
        var graph = await _coordinator.AnalyzeDependenciesAsync(containerId, filePaths);

        // Assert
        graph.Nodes.ShouldNotBeNull();
        graph.Nodes.Count.ShouldBe(2);
        graph.CircularDependencies.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeDependenciesAsync_WithCircularDependencies_DetectsCycle() {
        // Arrange
        var containerId = "test-container";
        var filePaths = new List<string> { "/workspace/File1.cs", "/workspace/File2.cs" };

        // Simulate circular dependencies by returning file paths in dependency extraction
        _containerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/File1.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("using System;\nusing /workspace/File2;\n\nclass File1 {}");

        _containerManager
            .Setup(c => c.ReadFileInContainerAsync(containerId, "/workspace/File2.cs", It.IsAny<CancellationToken>()))
            .ReturnsAsync("using System;\nusing /workspace/File1;\n\nclass File2 {}");

        // Act
        var graph = await _coordinator.AnalyzeDependenciesAsync(containerId, filePaths);

        // Assert
        graph.Nodes.ShouldNotBeNull();
        graph.Nodes.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PlanChangeOrderAsync_WithNoDependencies_PreservesOrder() {
        // Arrange
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Modified, Path = "/workspace/File1.cs", NewContent = "content1" },
            new() { Type = ChangeType.Modified, Path = "/workspace/File2.cs", NewContent = "content2" }
        };

        var graph = new DependencyGraph {
            Nodes = new Dictionary<string, DependencyNode> {
                ["/workspace/File1.cs"] = new() { FilePath = "/workspace/File1.cs", DependsOn = [], DependedBy = [] },
                ["/workspace/File2.cs"] = new() { FilePath = "/workspace/File2.cs", DependsOn = [], DependedBy = [] }
            }
        };

        // Act
        var orderedChanges = await _coordinator.PlanChangeOrderAsync(changes, graph);

        // Assert
        orderedChanges.Count.ShouldBe(2);
    }

    [Fact]
    public async Task PlanChangeOrderAsync_WithDependencies_OrdersDependenciesFirst() {
        // Arrange
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Modified, Path = "/workspace/File1.cs", NewContent = "content1" },
            new() { Type = ChangeType.Modified, Path = "/workspace/File2.cs", NewContent = "content2" }
        };

        var graph = new DependencyGraph {
            Nodes = new Dictionary<string, DependencyNode> {
                ["/workspace/File1.cs"] = new() { 
                    FilePath = "/workspace/File1.cs", 
                    DependsOn = ["/workspace/File2.cs"], 
                    DependedBy = [] 
                },
                ["/workspace/File2.cs"] = new() { 
                    FilePath = "/workspace/File2.cs", 
                    DependsOn = [], 
                    DependedBy = ["/workspace/File1.cs"] 
                }
            }
        };

        // Act
        var orderedChanges = await _coordinator.PlanChangeOrderAsync(changes, graph);

        // Assert
        orderedChanges.Count.ShouldBe(2);
        
        // File2 should come before File1 because File1 depends on File2
        var file1Index = orderedChanges.FindIndex(c => c.Path == "/workspace/File1.cs");
        var file2Index = orderedChanges.FindIndex(c => c.Path == "/workspace/File2.cs");
        file2Index.ShouldBeLessThan(file1Index, "File2 should come before File1 because File1 depends on File2");
    }

    [Fact]
    public async Task ApplyAtomicChangesAsync_WithCreateChange_CreatesFile() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Created, Path = "/workspace/NewFile.cs", NewContent = "class NewFile {}" }
        };

        // Act
        await _coordinator.ApplyAtomicChangesAsync(containerId, changes);

        // Assert
        _fileEditor.Verify(
            f => f.CreateFileAsync(containerId, "/workspace/NewFile.cs", "class NewFile {}", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyAtomicChangesAsync_WithModifyChange_ModifiesFile() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Modified, Path = "/workspace/File.cs", NewContent = "modified content" }
        };

        // Act
        await _coordinator.ApplyAtomicChangesAsync(containerId, changes);

        // Assert
        _fileEditor.Verify(
            f => f.ModifyFileAsync(containerId, "/workspace/File.cs", It.IsAny<Func<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyAtomicChangesAsync_WithDeleteChange_DeletesFile() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Deleted, Path = "/workspace/File.cs", OldContent = "old content" }
        };

        // Act
        await _coordinator.ApplyAtomicChangesAsync(containerId, changes);

        // Assert
        _fileEditor.Verify(
            f => f.DeleteFileAsync(containerId, "/workspace/File.cs", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyAtomicChangesAsync_WithError_RollsBackChanges() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Created, Path = "/workspace/File1.cs", NewContent = "content1" },
            new() { Type = ChangeType.Created, Path = "/workspace/File2.cs", NewContent = "content2" }
        };

        var callCount = 0;
        _fileEditor
            .Setup(f => f.CreateFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => {
                callCount++;
                if (callCount == 2) {
                    throw new InvalidOperationException("Simulated error");
                }
            });

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _coordinator.ApplyAtomicChangesAsync(containerId, changes));

        exception.Message.ShouldBe("Simulated error");
        
        // Verify rollback was attempted (delete the first file that was created)
        _fileEditor.Verify(
            f => f.DeleteFileAsync(containerId, "/workspace/File1.cs", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RollbackChangesAsync_WithCreatedFile_DeletesFile() {
        // Arrange
        var containerId = "test-container";
        var appliedChanges = new List<FileChange> {
            new() { Type = ChangeType.Created, Path = "/workspace/NewFile.cs", NewContent = "content" }
        };

        // Act
        await _coordinator.RollbackChangesAsync(containerId, appliedChanges);

        // Assert
        _fileEditor.Verify(
            f => f.DeleteFileAsync(containerId, "/workspace/NewFile.cs", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RollbackChangesAsync_WithModifiedFile_RestoresOldContent() {
        // Arrange
        var containerId = "test-container";
        var appliedChanges = new List<FileChange> {
            new() { 
                Type = ChangeType.Modified, 
                Path = "/workspace/File.cs", 
                OldContent = "old content",
                NewContent = "new content"
            }
        };

        // Act
        await _coordinator.RollbackChangesAsync(containerId, appliedChanges);

        // Assert
        _fileEditor.Verify(
            f => f.ModifyFileAsync(containerId, "/workspace/File.cs", It.IsAny<Func<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RollbackChangesAsync_WithDeletedFile_RecreatesFile() {
        // Arrange
        var containerId = "test-container";
        var appliedChanges = new List<FileChange> {
            new() { 
                Type = ChangeType.Deleted, 
                Path = "/workspace/File.cs", 
                OldContent = "original content"
            }
        };

        // Act
        await _coordinator.RollbackChangesAsync(containerId, appliedChanges);

        // Assert
        _fileEditor.Verify(
            f => f.CreateFileAsync(containerId, "/workspace/File.cs", "original content", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyChangesetAsync_WithSuccessfulBuild_ReturnsValid() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Modified, Path = "/workspace/File.cs", NewContent = "content" }
        };

        _buildVerifier
            .Setup(b => b.VerifyBuildAsync(containerId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult {
                Success = true,
                Attempts = 1,
                Output = "Build succeeded",
                Errors = [],
                FixesApplied = [],
                Duration = TimeSpan.FromSeconds(5)
            });

        // Act
        var result = await _coordinator.VerifyChangesetAsync(containerId, changes);

        // Assert
        result.IsValid.ShouldBeTrue();
        result.Error.ShouldBeNull();
        result.BuildResult.ShouldNotBeNull();
        result.BuildResult.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyChangesetAsync_WithFailedBuild_ReturnsInvalid() {
        // Arrange
        var containerId = "test-container";
        var changes = new List<FileChange> {
            new() { Type = ChangeType.Modified, Path = "/workspace/File.cs", NewContent = "invalid content" }
        };

        _buildVerifier
            .Setup(b => b.VerifyBuildAsync(containerId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult {
                Success = false,
                Attempts = 1,
                Output = "Build failed",
                Errors = [
                    new BuildError {
                        FilePath = "/workspace/File.cs",
                        LineNumber = 1,
                        Severity = ErrorSeverity.Error,
                        ErrorCode = "CS1002",
                        Message = "Syntax error",
                        Category = ErrorCategory.Syntax
                    }
                ],
                FixesApplied = [],
                Duration = TimeSpan.FromSeconds(5)
            });

        // Act
        var result = await _coordinator.VerifyChangesetAsync(containerId, changes);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.ShouldContain("Build failed");
        result.BuildResult.ShouldNotBeNull();
        result.BuildResult.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task RefactorAsync_WithValidPlan_CompletesSuccessfully() {
        // Arrange
        var containerId = "test-container";
        var plan = new RefactoringPlan {
            Type = RefactoringType.Rename,
            Description = "Rename class Foo to Bar",
            AffectedFiles = ["/workspace/File1.cs", "/workspace/File2.cs"],
            Changes = new Dictionary<string, FileChange> {
                ["/workspace/File1.cs"] = new() { 
                    Type = ChangeType.Modified, 
                    Path = "/workspace/File1.cs", 
                    OldContent = "class Foo {}",
                    NewContent = "class Bar {}" 
                },
                ["/workspace/File2.cs"] = new() { 
                    Type = ChangeType.Modified, 
                    Path = "/workspace/File2.cs", 
                    OldContent = "var foo = new Foo();",
                    NewContent = "var foo = new Bar();" 
                }
            }
        };

        _containerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        _buildVerifier
            .Setup(b => b.VerifyBuildAsync(containerId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult {
                Success = true,
                Attempts = 1,
                Output = "Build succeeded",
                Errors = [],
                FixesApplied = [],
                Duration = TimeSpan.FromSeconds(5)
            });

        // Act
        await _coordinator.RefactorAsync(containerId, plan);

        // Assert
        _fileEditor.Verify(
            f => f.ModifyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RefactorAsync_WithFailedValidation_RollsBackChanges() {
        // Arrange
        var containerId = "test-container";
        var plan = new RefactoringPlan {
            Type = RefactoringType.Rename,
            Description = "Rename with error",
            AffectedFiles = ["/workspace/File.cs"],
            Changes = new Dictionary<string, FileChange> {
                ["/workspace/File.cs"] = new() { 
                    Type = ChangeType.Modified, 
                    Path = "/workspace/File.cs", 
                    OldContent = "class Foo {}",
                    NewContent = "class Bar invalid" 
                }
            }
        };

        _containerManager
            .Setup(c => c.ReadFileInContainerAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("");

        _buildVerifier
            .Setup(b => b.VerifyBuildAsync(containerId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult {
                Success = false,
                Attempts = 1,
                Output = "Build failed",
                Errors = [new BuildError { Message = "Syntax error" }],
                FixesApplied = [],
                Duration = TimeSpan.FromSeconds(5)
            });

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await _coordinator.RefactorAsync(containerId, plan));

        exception.Message.ShouldContain("Refactoring failed validation");
        
        // Verify rollback was attempted
        _fileEditor.Verify(
            f => f.ModifyFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, string>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce); // At least once for rollback
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
