namespace RG.OpenCopilot.PRGenerationAgent.Planning.Models;

public sealed class AgentTaskContext {
    public string IssueTitle { get; init; } = "";
    public string IssueBody { get; init; } = "";
    public string? InstructionsMarkdown { get; init; }
    public string? RepositorySummary { get; init; }
}
