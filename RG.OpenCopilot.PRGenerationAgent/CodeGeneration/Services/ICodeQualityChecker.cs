namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Services;

public interface ICodeQualityChecker {
    Task<QualityResult> CheckAndFixAsync(string containerId, CancellationToken cancellationToken = default);
    Task<LintResult> RunLinterAsync(string containerId, CancellationToken cancellationToken = default);
    Task<FormatResult> RunFormatterAsync(string containerId, CancellationToken cancellationToken = default);
    Task<AnalysisResult> RunStaticAnalysisAsync(string containerId, CancellationToken cancellationToken = default);
    Task AutoFixIssuesAsync(string containerId, List<QualityIssue> issues, CancellationToken cancellationToken = default);
}
