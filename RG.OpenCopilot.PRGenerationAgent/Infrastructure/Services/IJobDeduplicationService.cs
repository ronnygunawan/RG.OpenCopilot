namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Service for enforcing job idempotency and preventing duplicate executions
/// </summary>
public interface IJobDeduplicationService {
    /// <summary>
    /// Check if a job with the given idempotency key is already in-flight
    /// </summary>
    /// <param name="idempotencyKey">Idempotency key to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job ID if duplicate found, null otherwise</returns>
    Task<string?> GetInFlightJobAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Register a job as in-flight with its idempotency key
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="idempotencyKey">Idempotency key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RegisterJobAsync(string jobId, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a job from in-flight tracking (when completed or failed)
    /// </summary>
    /// <param name="jobId">Job ID to remove</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnregisterJobAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear all in-flight jobs (used for testing/maintenance)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}
