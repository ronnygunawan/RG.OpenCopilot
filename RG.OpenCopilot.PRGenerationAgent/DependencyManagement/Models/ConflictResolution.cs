namespace RG.OpenCopilot.PRGenerationAgent.DependencyManagement.Models;

public sealed class ConflictResolution {
    public bool Success { get; init; }
    public List<Package> ResolvedPackages { get; init; } = [];
    public List<VersionConflict> RemainingConflicts { get; init; } = [];
    public string? Error { get; init; }
}
