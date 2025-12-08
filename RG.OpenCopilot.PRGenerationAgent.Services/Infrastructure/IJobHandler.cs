namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Interface for handling specific job types
/// </summary>
public interface IJobHandler {
    /// <summary>
    /// The job type this handler can process
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Execute the job
    /// </summary>
    /// <param name="job">The job to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the job execution</returns>
    Task<JobResult> ExecuteAsync(BackgroundJob job, CancellationToken cancellationToken = default);
}
