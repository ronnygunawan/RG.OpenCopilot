namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Interface for dispatching and managing background jobs
/// </summary>
public interface IJobDispatcher {
    /// <summary>
    /// Dispatch a job to be processed in the background
    /// </summary>
    /// <param name="job">The job to dispatch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the job was dispatched successfully</returns>
    Task<bool> DispatchAsync(BackgroundJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a job by ID
    /// </summary>
    /// <param name="jobId">The ID of the job to cancel</param>
    /// <returns>True if the job was cancelled successfully</returns>
    bool CancelJob(string jobId);

    /// <summary>
    /// Register a job handler
    /// </summary>
    /// <param name="handler">The handler to register</param>
    void RegisterHandler(IJobHandler handler);
}
