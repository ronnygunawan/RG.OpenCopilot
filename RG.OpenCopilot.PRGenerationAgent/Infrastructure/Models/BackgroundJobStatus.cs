namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Status of a background job
/// </summary>
public enum BackgroundJobStatus {
    /// <summary>
    /// Job is queued and waiting to be processed
    /// </summary>
    Queued,
    
    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Processing,
    
    /// <summary>
    /// Job completed successfully
    /// </summary>
    Completed,
    
    /// <summary>
    /// Job failed
    /// </summary>
    Failed,
    
    /// <summary>
    /// Job was cancelled
    /// </summary>
    Cancelled,
    
    /// <summary>
    /// Job is being retried after a failure
    /// </summary>
    Retried,
    
    /// <summary>
    /// Job has exceeded max retries and is in dead-letter queue
    /// </summary>
    DeadLetter
}

/// <summary>
/// Detailed status information for a background job
/// </summary>
public sealed class BackgroundJobStatusInfo {
    /// <summary>
    /// Unique job identifier
    /// </summary>
    public string JobId { get; init; } = "";
    
    /// <summary>
    /// Job type
    /// </summary>
    public string JobType { get; init; } = "";
    
    /// <summary>
    /// Current status
    /// </summary>
    public BackgroundJobStatus Status { get; init; }
    
    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// When the job started processing
    /// </summary>
    public DateTime? StartedAt { get; init; }
    
    /// <summary>
    /// When the job completed (successfully or failed)
    /// </summary>
    public DateTime? CompletedAt { get; init; }
    
    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>
    /// Job metadata
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
    
    /// <summary>
    /// Result data from the job (JSON serialized)
    /// </summary>
    public string? ResultData { get; init; }
    
    /// <summary>
    /// Number of times this job has been retried
    /// </summary>
    public int RetryCount { get; init; } = 0;
    
    /// <summary>
    /// Maximum number of retry attempts allowed
    /// </summary>
    public int MaxRetries { get; init; } = 3;
    
    /// <summary>
    /// When the job was last retried
    /// </summary>
    public DateTime? LastRetryAt { get; init; }
    
    /// <summary>
    /// Source that triggered this job (e.g., "Webhook", "Scheduler", "Manual")
    /// </summary>
    public string Source { get; init; } = "";
    
    /// <summary>
    /// Parent job ID if this job was spawned by another job
    /// </summary>
    public string? ParentJobId { get; init; }
    
    /// <summary>
    /// Correlation ID for tracking related jobs
    /// </summary>
    public string? CorrelationId { get; init; }
    
    /// <summary>
    /// Processing duration in milliseconds (if completed)
    /// </summary>
    public long? ProcessingDurationMs { get; init; }
    
    /// <summary>
    /// Queue wait time in milliseconds (time between created and started)
    /// </summary>
    public long? QueueWaitTimeMs { get; init; }
}
