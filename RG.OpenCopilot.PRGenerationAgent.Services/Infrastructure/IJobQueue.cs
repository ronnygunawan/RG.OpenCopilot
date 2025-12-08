namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Interface for job queue operations
/// </summary>
public interface IJobQueue {
    /// <summary>
    /// Enqueue a job for processing
    /// </summary>
    /// <param name="job">The job to enqueue</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the job was enqueued successfully</returns>
    Task<bool> EnqueueAsync(BackgroundJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeue a job for processing
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The next job to process, or null if the queue is empty</returns>
    Task<BackgroundJob?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the number of jobs in the queue
    /// </summary>
    int Count { get; }
}
