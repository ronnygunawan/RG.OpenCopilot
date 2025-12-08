using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace RG.OpenCopilot.PRGenerationAgent.Services.FileOperations;

/// <summary>
/// Coordinates multi-file refactoring operations with dependency-aware ordering and atomic transactions
/// </summary>
internal sealed class MultiFileRefactoringCoordinator : IMultiFileRefactoringCoordinator {
    private readonly IFileAnalyzer _fileAnalyzer;
    private readonly IFileEditor _fileEditor;
    private readonly IBuildVerifier _buildVerifier;
    private readonly IContainerManager _containerManager;
    private readonly ILogger<MultiFileRefactoringCoordinator> _logger;
    private readonly ConcurrentBag<string> _transactionLog = [];

    // Compiled regex patterns for better performance
    private static readonly Regex CSharpUsingPattern = new(@"using\s+(?:static\s+)?([A-Za-z0-9_.]+);", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex JavaImportPattern = new(@"import\s+(?:static\s+)?([A-Za-z0-9_.]+);", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex TypeScriptImportPattern = new(@"import\s+.*?\s+from\s+['""]([^'""]+)['""];?", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex PythonImportPattern = new(@"(?:from\s+([A-Za-z0-9_.]+)\s+)?import\s+([A-Za-z0-9_., ]+)", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex GoImportBlockPattern = new(@"import\s+(?:\(\s*([^)]+)\s*\)|""([^""]+)"")", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex GoImportPattern = new(@"""([^""]+)""", RegexOptions.Compiled);

    public MultiFileRefactoringCoordinator(
        IFileAnalyzer fileAnalyzer,
        IFileEditor fileEditor,
        IBuildVerifier buildVerifier,
        IContainerManager containerManager,
        ILogger<MultiFileRefactoringCoordinator> logger) {
        _fileAnalyzer = fileAnalyzer;
        _fileEditor = fileEditor;
        _buildVerifier = buildVerifier;
        _containerManager = containerManager;
        _logger = logger;
    }

    public async Task RefactorAsync(string containerId, RefactoringPlan plan, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Starting refactoring operation: {Type} - {Description}", plan.Type, plan.Description);
        _transactionLog.Clear();

        try {
            // Step 1: Analyze dependencies
            _logger.LogInformation("Analyzing dependencies for {FileCount} files", plan.AffectedFiles.Count);
            var dependencyGraph = await AnalyzeDependenciesAsync(containerId, plan.AffectedFiles, cancellationToken);

            if (dependencyGraph.CircularDependencies.Count > 0) {
                _logger.LogWarning("Detected {Count} circular dependencies", dependencyGraph.CircularDependencies.Count);
                foreach (var cycle in dependencyGraph.CircularDependencies) {
                    _logger.LogWarning("Circular dependency: {Cycle}", cycle);
                }
            }

            // Step 2: Convert changes dictionary to list
            var changes = plan.Changes.Values.ToList();

            // Step 3: Plan change order
            _logger.LogInformation("Planning change order for {ChangeCount} changes", changes.Count);
            var orderedChanges = await PlanChangeOrderAsync(changes, dependencyGraph, cancellationToken);

            // Step 4: Apply changes atomically
            _logger.LogInformation("Applying {ChangeCount} changes atomically", orderedChanges.Count);
            await ApplyAtomicChangesAsync(containerId, orderedChanges, cancellationToken);

            // Step 5: Verify changeset
            _logger.LogInformation("Verifying changeset");
            var validationResult = await VerifyChangesetAsync(containerId, orderedChanges, cancellationToken);

            if (!validationResult.IsValid) {
                _logger.LogError("Changeset validation failed: {Error}", validationResult.Error);
                _logger.LogInformation("Rolling back changes");
                await RollbackChangesAsync(containerId, validationResult.AppliedChanges, cancellationToken);
                throw new InvalidOperationException($"Refactoring failed validation: {validationResult.Error}");
            }

            _logger.LogInformation("Refactoring completed successfully");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Refactoring operation failed");
            throw;
        }
    }

    public async Task<DependencyGraph> AnalyzeDependenciesAsync(string containerId, List<string> filePaths, CancellationToken cancellationToken = default) {
        var nodes = new Dictionary<string, DependencyNode>();
        var circularDependencies = new List<string>();

        _logger.LogInformation("Analyzing dependencies for {FileCount} files", filePaths.Count);

        // Build nodes for each file
        foreach (var filePath in filePaths) {
            var dependencies = await ExtractDependenciesAsync(containerId, filePath, cancellationToken);
            
            nodes[filePath] = new DependencyNode {
                FilePath = filePath,
                DependsOn = dependencies.Where(d => filePaths.Contains(d)).ToList(),
                DependedBy = []
            };
        }

        // Build reverse dependencies
        foreach (var node in nodes.Values) {
            foreach (var dependency in node.DependsOn) {
                if (nodes.ContainsKey(dependency)) {
                    nodes[dependency].DependedBy.Add(node.FilePath);
                }
            }
        }

        // Detect circular dependencies
        foreach (var node in nodes.Values) {
            var visited = new HashSet<string>();
            var path = new List<string>();
            
            if (HasCircularDependency(node.FilePath, nodes, visited, path)) {
                var cycle = string.Join(" -> ", path);
                if (!circularDependencies.Contains(cycle)) {
                    circularDependencies.Add(cycle);
                }
            }
        }

        return new DependencyGraph {
            Nodes = nodes,
            CircularDependencies = circularDependencies
        };
    }

    public Task<List<FileChange>> PlanChangeOrderAsync(List<FileChange> changes, DependencyGraph graph, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Planning change order for {ChangeCount} changes", changes.Count);

        // Create a mapping from file path to change
        var changeMap = changes.ToDictionary(c => c.Path);

        // Topological sort based on dependencies
        var orderedPaths = TopologicalSort(graph);

        // Order changes based on topological sort
        var orderedChanges = new List<FileChange>();
        
        foreach (var path in orderedPaths) {
            if (changeMap.ContainsKey(path)) {
                orderedChanges.Add(changeMap[path]);
            }
        }

        // Add any changes not in the dependency graph at the end
        foreach (var change in changes) {
            if (!orderedChanges.Contains(change)) {
                orderedChanges.Add(change);
            }
        }

        _logger.LogInformation("Ordered {ChangeCount} changes", orderedChanges.Count);
        return Task.FromResult(orderedChanges);
    }

    public async Task ApplyAtomicChangesAsync(string containerId, List<FileChange> orderedChanges, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Applying {ChangeCount} changes atomically", orderedChanges.Count);
        
        var appliedChanges = new List<FileChange>();
        
        try {
            foreach (var change in orderedChanges) {
                _transactionLog.Add($"[{DateTime.UtcNow:O}] Applying {change.Type} to {change.Path}");
                
                switch (change.Type) {
                    case ChangeType.Created:
                        await _fileEditor.CreateFileAsync(containerId, change.Path, change.NewContent ?? "", cancellationToken);
                        break;
                    
                    case ChangeType.Modified:
                        await _fileEditor.ModifyFileAsync(
                            containerId, 
                            change.Path, 
                            _ => change.NewContent ?? "", 
                            cancellationToken);
                        break;
                    
                    case ChangeType.Deleted:
                        await _fileEditor.DeleteFileAsync(containerId, change.Path, cancellationToken);
                        break;
                    
                    default:
                        throw new InvalidOperationException($"Unsupported change type: {change.Type}");
                }
                
                appliedChanges.Add(change);
                _transactionLog.Add($"[{DateTime.UtcNow:O}] Successfully applied {change.Type} to {change.Path}");
            }

            _logger.LogInformation("All {ChangeCount} changes applied successfully", orderedChanges.Count);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error applying changes, rolling back {AppliedCount} changes", appliedChanges.Count);
            _transactionLog.Add($"[{DateTime.UtcNow:O}] ERROR: {ex.Message}");
            await RollbackChangesAsync(containerId, appliedChanges, cancellationToken);
            throw;
        }
    }

    public async Task RollbackChangesAsync(string containerId, List<FileChange> appliedChanges, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Rolling back {ChangeCount} changes", appliedChanges.Count);
        
        // Reverse the order to rollback in reverse
        var reversedChanges = appliedChanges.AsEnumerable().Reverse().ToList();
        
        foreach (var change in reversedChanges) {
            try {
                _transactionLog.Add($"[{DateTime.UtcNow:O}] Rolling back {change.Type} on {change.Path}");
                
                switch (change.Type) {
                    case ChangeType.Created:
                        // Rollback creation by deleting the file
                        await _fileEditor.DeleteFileAsync(containerId, change.Path, cancellationToken);
                        _transactionLog.Add($"[{DateTime.UtcNow:O}] Deleted {change.Path} (rollback create)");
                        break;
                    
                    case ChangeType.Modified:
                        // Rollback modification by restoring old content
                        if (change.OldContent != null) {
                            await _fileEditor.ModifyFileAsync(
                                containerId, 
                                change.Path, 
                                _ => change.OldContent, 
                                cancellationToken);
                            _transactionLog.Add($"[{DateTime.UtcNow:O}] Restored old content to {change.Path}");
                        }
                        break;
                    
                    case ChangeType.Deleted:
                        // Rollback deletion by recreating the file
                        if (change.OldContent != null) {
                            await _fileEditor.CreateFileAsync(containerId, change.Path, change.OldContent, cancellationToken);
                            _transactionLog.Add($"[{DateTime.UtcNow:O}] Recreated {change.Path} (rollback delete)");
                        }
                        break;
                }
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error rolling back change to {FilePath}", change.Path);
                _transactionLog.Add($"[{DateTime.UtcNow:O}] ERROR rolling back {change.Path}: {ex.Message}");
            }
        }

        _logger.LogInformation("Rollback completed");
    }

    public async Task<ChangesetValidationResult> VerifyChangesetAsync(string containerId, List<FileChange> changes, CancellationToken cancellationToken = default) {
        _logger.LogInformation("Verifying changeset with {ChangeCount} changes", changes.Count);
        
        var warnings = new List<string>();

        try {
            // Run build verification
            var buildResult = await _buildVerifier.VerifyBuildAsync(containerId, maxRetries: 1, cancellationToken);

            if (!buildResult.Success) {
                var errorMessages = buildResult.Errors.Select(e => $"{e.FilePath}:{e.LineNumber} - {e.Message}").ToList();
                var errorSummary = string.Join("; ", errorMessages.Take(5));
                
                return new ChangesetValidationResult {
                    IsValid = false,
                    AppliedChanges = changes,
                    BuildResult = buildResult,
                    Warnings = warnings,
                    Error = $"Build failed with {buildResult.Errors.Count} errors: {errorSummary}"
                };
            }

            // Check for orphaned references (basic check)
            foreach (var change in changes) {
                if (change.Type == ChangeType.Deleted && change.NewContent == null) {
                    warnings.Add($"File {change.Path} was deleted - ensure no orphaned references remain");
                }
            }

            _logger.LogInformation("Changeset validation passed");
            
            return new ChangesetValidationResult {
                IsValid = true,
                AppliedChanges = changes,
                BuildResult = buildResult,
                Warnings = warnings,
                Error = null
            };
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Error during changeset validation");
            
            return new ChangesetValidationResult {
                IsValid = false,
                AppliedChanges = changes,
                BuildResult = null,
                Warnings = warnings,
                Error = $"Validation error: {ex.Message}"
            };
        }
    }

    // Helper methods

    private async Task<List<string>> ExtractDependenciesAsync(string containerId, string filePath, CancellationToken cancellationToken) {
        var dependencies = new List<string>();

        try {
            var content = await _containerManager.ReadFileInContainerAsync(containerId, filePath, cancellationToken);
            
            // Extract imports/using statements based on file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            
            switch (extension) {
                case ".cs":
                    dependencies.AddRange(ExtractCSharpDependencies(content));
                    break;
                case ".java":
                    dependencies.AddRange(ExtractJavaDependencies(content));
                    break;
                case ".ts":
                case ".tsx":
                case ".js":
                case ".jsx":
                    dependencies.AddRange(ExtractTypeScriptDependencies(content));
                    break;
                case ".py":
                    dependencies.AddRange(ExtractPythonDependencies(content));
                    break;
                case ".go":
                    dependencies.AddRange(ExtractGoDependencies(content));
                    break;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "Error extracting dependencies from {FilePath}", filePath);
        }

        return dependencies;
    }

    private static List<string> ExtractCSharpDependencies(string content) {
        var dependencies = new List<string>();
        
        // Match using statements
        var matches = CSharpUsingPattern.Matches(content);
        
        foreach (Match match in matches) {
            dependencies.Add(match.Groups[1].Value);
        }

        return dependencies;
    }

    private static List<string> ExtractJavaDependencies(string content) {
        var dependencies = new List<string>();
        
        // Match import statements
        var matches = JavaImportPattern.Matches(content);
        
        foreach (Match match in matches) {
            dependencies.Add(match.Groups[1].Value);
        }

        return dependencies;
    }

    private static List<string> ExtractTypeScriptDependencies(string content) {
        var dependencies = new List<string>();
        
        // Match import statements
        var matches = TypeScriptImportPattern.Matches(content);
        
        foreach (Match match in matches) {
            var importPath = match.Groups[1].Value;
            // Only include relative imports
            if (importPath.StartsWith("./") || importPath.StartsWith("../")) {
                dependencies.Add(importPath);
            }
        }

        return dependencies;
    }

    private static List<string> ExtractPythonDependencies(string content) {
        var dependencies = new List<string>();
        
        // Match import statements
        var matches = PythonImportPattern.Matches(content);
        
        foreach (Match match in matches) {
            if (!string.IsNullOrEmpty(match.Groups[1].Value)) {
                dependencies.Add(match.Groups[1].Value);
            }
        }

        return dependencies;
    }

    private static List<string> ExtractGoDependencies(string content) {
        var dependencies = new List<string>();
        
        // Match import statements
        var matches = GoImportBlockPattern.Matches(content);
        
        foreach (Match match in matches) {
            var importBlock = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(importBlock)) {
                var imports = GoImportPattern.Matches(importBlock);
                foreach (Match imp in imports) {
                    dependencies.Add(imp.Groups[1].Value);
                }
            }
            else if (!string.IsNullOrEmpty(match.Groups[2].Value)) {
                dependencies.Add(match.Groups[2].Value);
            }
        }

        return dependencies;
    }

    private bool HasCircularDependency(string filePath, Dictionary<string, DependencyNode> nodes, HashSet<string> visited, List<string> path) {
        if (path.Contains(filePath)) {
            // Found a cycle
            path.Add(filePath);
            return true;
        }

        if (visited.Contains(filePath)) {
            return false;
        }

        visited.Add(filePath);
        path.Add(filePath);

        if (nodes.TryGetValue(filePath, out var node)) {
            foreach (var dependency in node.DependsOn) {
                if (HasCircularDependency(dependency, nodes, visited, path)) {
                    return true;
                }
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private List<string> TopologicalSort(DependencyGraph graph) {
        var sorted = new List<string>();
        var visited = new HashSet<string>();
        var temp = new HashSet<string>();

        foreach (var node in graph.Nodes.Values) {
            if (!visited.Contains(node.FilePath)) {
                TopologicalSortVisit(node.FilePath, graph.Nodes, visited, temp, sorted);
            }
        }

        // No reverse needed - dependencies are already added before dependents
        return sorted;
    }

    private void TopologicalSortVisit(string filePath, Dictionary<string, DependencyNode> nodes, HashSet<string> visited, HashSet<string> temp, List<string> sorted) {
        if (temp.Contains(filePath)) {
            // Circular dependency detected, but we'll continue (already logged in analysis)
            return;
        }

        if (visited.Contains(filePath)) {
            return;
        }

        temp.Add(filePath);

        if (nodes.TryGetValue(filePath, out var node)) {
            // Visit dependencies first
            foreach (var dependency in node.DependsOn) {
                TopologicalSortVisit(dependency, nodes, visited, temp, sorted);
            }
        }

        temp.Remove(filePath);
        visited.Add(filePath);
        sorted.Add(filePath);
    }

    /// <summary>
    /// Get transaction log for debugging (internal use only)
    /// </summary>
    internal IReadOnlyList<string> GetTransactionLog() => _transactionLog.ToList().AsReadOnly();
}
