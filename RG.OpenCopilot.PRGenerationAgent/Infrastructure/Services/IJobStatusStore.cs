namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Interface for storing and retrieving background job status information
/// </summary>
public interface IJobStatusStore {
    /// <summary>
    /// Store or update job status
    /// </summary>
    /// <param name="statusInfo">Job status information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetStatusAsync(BackgroundJobStatusInfo statusInfo, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get job status by ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job status information or null if not found</returns>
    Task<BackgroundJobStatusInfo?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete job status by ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteStatusAsync(string jobId, CancellationToken cancellationToken = default);
}
