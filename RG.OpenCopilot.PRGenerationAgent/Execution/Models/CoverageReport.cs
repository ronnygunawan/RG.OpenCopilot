namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public sealed class CoverageReport {
    public double LineCoverage { get; init; }
    public double BranchCoverage { get; init; }
    public int TotalLines { get; init; }
    public int CoveredLines { get; init; }
    public int TotalBranches { get; init; }
    public int CoveredBranches { get; init; }
    public string Summary { get; init; } = "";
}
