namespace RG.OpenCopilot.PRGenerationAgent.FileOperations.Models;

/// <summary>
/// Plan for a multi-file refactoring operation
/// </summary>
public sealed class RefactoringPlan {
    public RefactoringType Type { get; init; }
    public string Description { get; init; } = "";
    public List<string> AffectedFiles { get; init; } = [];
    public Dictionary<string, FileChange> Changes { get; init; } = new();
}
