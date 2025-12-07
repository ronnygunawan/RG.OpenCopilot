namespace RG.OpenCopilot.PRGenerationAgent.Execution.Services;

/// <summary>
/// Service for running tests, analyzing failures, and auto-fixing with LLM assistance
/// </summary>
public interface ITestValidator {
    Task<TestValidationResult> RunAndValidateTestsAsync(string containerId, int maxRetries = 2, CancellationToken cancellationToken = default);
    Task<TestExecutionResult> RunTestsAsync(string containerId, string? testFilter = null, CancellationToken cancellationToken = default);
    Task<List<TestFailure>> AnalyzeTestFailuresAsync(List<TestFailure> failures, CancellationToken cancellationToken = default);
    Task ApplyTestFixesAsync(string containerId, List<TestFix> fixes, CancellationToken cancellationToken = default);
    Task<CoverageReport?> GetCoverageAsync(string containerId, CancellationToken cancellationToken = default);
}
