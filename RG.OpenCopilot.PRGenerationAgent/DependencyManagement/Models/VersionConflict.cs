namespace RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Models;

public sealed class VersionConflict {
    public string PackageName { get; init; } = "";
    public string RequestedVersion { get; init; } = "";
    public string InstalledVersion { get; init; } = "";
    public ConflictSeverity Severity { get; init; }
}
