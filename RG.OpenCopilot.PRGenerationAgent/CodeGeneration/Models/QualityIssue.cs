namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

public sealed class QualityIssue {
    public string RuleId { get; init; } = "";
    public string Message { get; init; } = "";
    public string FilePath { get; init; } = "";
    public int LineNumber { get; init; }
    public IssueSeverity Severity { get; init; }
    public IssueCategory Category { get; init; }
    public bool AutoFixable { get; init; }
    public string? SuggestedFix { get; init; }
}
