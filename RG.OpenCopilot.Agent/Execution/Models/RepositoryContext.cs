namespace RG.OpenCopilot.Agent.Execution.Models;

/// <summary>
/// Context about the repository structure and configuration
/// </summary>
public sealed class RepositoryContext {
    public string Language { get; set; } = "";
    public List<string> Files { get; set; } = [];
    public string? TestFramework { get; set; }
    public string? BuildTool { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = [];
}
