namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

/// <summary>
/// Represents a single changelog entry
/// </summary>
public sealed class ChangelogEntry {
    /// <summary>
    /// Version number for this entry
    /// </summary>
    public string Version { get; init; } = "";

    /// <summary>
    /// Description of the change
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Type of change (Added, Changed, Fixed, Removed, etc.)
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// Date of the change
    /// </summary>
    public DateTimeOffset Date { get; init; }
}
