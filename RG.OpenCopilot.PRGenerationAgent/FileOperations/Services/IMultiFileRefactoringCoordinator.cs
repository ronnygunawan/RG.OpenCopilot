namespace RG.OpenCopilot.PRGenerationAgent.FileOperations.Services;

/// <summary>
/// Service for coordinating multi-file refactoring operations
/// </summary>
public interface IMultiFileRefactoringCoordinator {
    /// <summary>
    /// Execute a complete refactoring operation
    /// </summary>
    Task RefactorAsync(string containerId, RefactoringPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze dependencies between files
    /// </summary>
    Task<DependencyGraph> AnalyzeDependenciesAsync(string containerId, List<string> filePaths, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plan the order of changes based on dependencies
    /// </summary>
    Task<List<FileChange>> PlanChangeOrderAsync(List<FileChange> changes, DependencyGraph graph, CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply changes atomically with rollback on failure
    /// </summary>
    Task ApplyAtomicChangesAsync(string containerId, List<FileChange> orderedChanges, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback applied changes
    /// </summary>
    Task RollbackChangesAsync(string containerId, List<FileChange> appliedChanges, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify the changeset compiles and passes validation
    /// </summary>
    Task<ChangesetValidationResult> VerifyChangesetAsync(string containerId, List<FileChange> changes, CancellationToken cancellationToken = default);
}
