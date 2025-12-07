namespace RG.OpenCopilot.PRGenerationAgent.Execution.Services;

/// <summary>
/// Service for orchestrating complete plan step execution with LLM-driven code generation
/// </summary>
public interface ISmartStepExecutor {
    /// <summary>
    /// Executes a complete plan step from analysis through code generation, building, testing, and validation
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="step">Plan step to execute</param>
    /// <param name="context">Repository context information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive execution result with all changes and metrics</returns>
    Task<StepExecutionResult> ExecuteStepAsync(string containerId, PlanStep step, RepositoryContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a plan step with automatic retry on failure
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="step">Plan step to execute</param>
    /// <param name="context">Repository context information</param>
    /// <param name="maxRetries">Maximum number of retry attempts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive execution result with all changes and metrics</returns>
    Task<StepExecutionResult> ExecuteStepWithRetryAsync(string containerId, PlanStep step, RepositoryContext context, int maxRetries = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back changes from a failed step execution
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="failedResult">Failed execution result containing changes to rollback</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RollbackStepAsync(string containerId, StepExecutionResult failedResult, CancellationToken cancellationToken = default);
}
