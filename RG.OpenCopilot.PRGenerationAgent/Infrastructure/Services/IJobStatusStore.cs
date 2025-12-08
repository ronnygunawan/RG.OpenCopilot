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
    
    /// <summary>
    /// Get jobs by status
    /// </summary>
    /// <param name="status">Job status to filter by</param>
    /// <param name="skip">Number of jobs to skip for pagination</param>
    /// <param name="take">Number of jobs to take for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of jobs with the specified status</returns>
    Task<List<BackgroundJobStatusInfo>> GetJobsByStatusAsync(
        BackgroundJobStatus status,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get jobs by type
    /// </summary>
    /// <param name="jobType">Job type to filter by</param>
    /// <param name="skip">Number of jobs to skip for pagination</param>
    /// <param name="take">Number of jobs to take for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of jobs with the specified type</returns>
    Task<List<BackgroundJobStatusInfo>> GetJobsByTypeAsync(
        string jobType,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get jobs by source
    /// </summary>
    /// <param name="source">Source to filter by</param>
    /// <param name="skip">Number of jobs to skip for pagination</param>
    /// <param name="take">Number of jobs to take for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of jobs from the specified source</returns>
    Task<List<BackgroundJobStatusInfo>> GetJobsBySourceAsync(
        string source,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all jobs with optional filtering
    /// </summary>
    /// <param name="status">Optional status filter</param>
    /// <param name="jobType">Optional job type filter</param>
    /// <param name="source">Optional source filter</param>
    /// <param name="skip">Number of jobs to skip for pagination</param>
    /// <param name="take">Number of jobs to take for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of jobs matching the filters</returns>
    Task<List<BackgroundJobStatusInfo>> GetJobsAsync(
        BackgroundJobStatus? status = null,
        string? jobType = null,
        string? source = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get aggregated metrics for jobs
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job metrics</returns>
    Task<JobMetrics> GetMetricsAsync(CancellationToken cancellationToken = default);
}
