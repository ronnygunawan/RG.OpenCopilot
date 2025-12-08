namespace RG.OpenCopilot.PRGenerationAgent.FileOperations.Models;

/// <summary>
/// Graph representing file dependencies
/// </summary>
public sealed class DependencyGraph {
    public Dictionary<string, DependencyNode> Nodes { get; init; } = new();
    public List<string> CircularDependencies { get; init; } = [];
}
