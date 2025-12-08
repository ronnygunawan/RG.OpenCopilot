namespace RG.OpenCopilot.PRGenerationAgent.FileOperations.Models;

/// <summary>
/// Node in a file dependency graph
/// </summary>
public sealed class DependencyNode {
    public string FilePath { get; init; } = "";
    public List<string> DependsOn { get; init; } = [];
    public List<string> DependedBy { get; init; } = [];
}
