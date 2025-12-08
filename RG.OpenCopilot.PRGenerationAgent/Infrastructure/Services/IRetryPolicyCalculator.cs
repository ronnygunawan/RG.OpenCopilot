namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Service for calculating retry delays based on retry policy
/// </summary>
public interface IRetryPolicyCalculator {
    /// <summary>
    /// Calculate the delay before the next retry attempt
    /// </summary>
    /// <param name="policy">Retry policy configuration</param>
    /// <param name="retryCount">Current retry count (0-based)</param>
    /// <returns>Delay in milliseconds before next retry</returns>
    int CalculateRetryDelay(RetryPolicy policy, int retryCount);

    /// <summary>
    /// Determine if a job should be retried based on policy and current state
    /// </summary>
    /// <param name="policy">Retry policy configuration</param>
    /// <param name="retryCount">Current retry count</param>
    /// <param name="maxRetries">Maximum retries allowed for this specific job</param>
    /// <param name="shouldRetry">Whether the job result indicates retry is appropriate</param>
    /// <returns>True if job should be retried, false otherwise</returns>
    bool ShouldRetry(RetryPolicy policy, int retryCount, int maxRetries, bool shouldRetry);
}
