namespace RG.OpenCopilot.Agent.FileOperations.Models;

/// <summary>
/// Represents the file tree structure of a repository
/// </summary>
public sealed class FileTree {
    public FileTreeNode Root { get; init; } = new();
    public List<string> AllFiles { get; init; } = [];
}
