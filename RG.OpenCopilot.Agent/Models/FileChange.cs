namespace RG.OpenCopilot.Agent.Models;

/// <summary>
/// Represents a tracked file change with old and new content
/// </summary>
public sealed class FileChange {
    public ChangeType Type { get; init; }
    public string Path { get; init; } = "";
    public string? OldContent { get; init; }
    public string? NewContent { get; init; }
}
