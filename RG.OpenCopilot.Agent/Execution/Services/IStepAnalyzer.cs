namespace RG.OpenCopilot.Agent.Execution.Services;

/// <summary>
/// Service for analyzing plan steps and generating actionable code change plans
/// </summary>
public interface IStepAnalyzer {
    /// <summary>
    /// Analyzes a plan step and generates a detailed action plan
    /// </summary>
    /// <param name="step">The plan step to analyze</param>
    /// <param name="context">Repository context information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A detailed action plan with specific code changes</returns>
    Task<StepActionPlan> AnalyzeStepAsync(PlanStep step, RepositoryContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds repository context by analyzing files in a container
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Repository context with language, files, and framework information</returns>
    Task<RepositoryContext> BuildContextAsync(string containerId, CancellationToken cancellationToken = default);
}
