namespace RG.OpenCopilot.PRGenerationAgent.FileOperations.Models;

/// <summary>
/// Result of validating a changeset
/// </summary>
public sealed class ChangesetValidationResult {
    public bool IsValid { get; init; }
    public List<FileChange> AppliedChanges { get; init; } = [];
    public BuildResult? BuildResult { get; init; }
    public List<string> Warnings { get; init; } = [];
    public string? Error { get; init; }
}
