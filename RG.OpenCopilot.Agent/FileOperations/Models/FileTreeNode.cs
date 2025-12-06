namespace RG.OpenCopilot.Agent.FileOperations.Models;

/// <summary>
/// Represents a node in the file tree
/// </summary>
public sealed class FileTreeNode {
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool IsDirectory { get; init; }
    public List<FileTreeNode> Children { get; init; } = [];
}
