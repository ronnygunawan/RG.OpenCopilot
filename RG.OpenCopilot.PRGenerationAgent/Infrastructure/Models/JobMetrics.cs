namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Aggregated metrics for background jobs
/// </summary>
public sealed class JobMetrics {
    /// <summary>
    /// Total number of jobs in the queue (queued status)
    /// </summary>
    public int QueueDepth { get; init; }
    
    /// <summary>
    /// Number of jobs currently being processed
    /// </summary>
    public int ProcessingCount { get; init; }
    
    /// <summary>
    /// Number of jobs that completed successfully
    /// </summary>
    public int CompletedCount { get; init; }
    
    /// <summary>
    /// Number of jobs that failed
    /// </summary>
    public int FailedCount { get; init; }
    
    /// <summary>
    /// Number of jobs that were cancelled
    /// </summary>
    public int CancelledCount { get; init; }
    
    /// <summary>
    /// Number of jobs in dead-letter queue
    /// </summary>
    public int DeadLetterCount { get; init; }
    
    /// <summary>
    /// Average processing duration in milliseconds
    /// </summary>
    public double AverageProcessingDurationMs { get; init; }
    
    /// <summary>
    /// Average queue wait time in milliseconds
    /// </summary>
    public double AverageQueueWaitTimeMs { get; init; }
    
    /// <summary>
    /// Failure rate (failed jobs / total jobs)
    /// </summary>
    public double FailureRate { get; init; }
    
    /// <summary>
    /// Total number of jobs
    /// </summary>
    public int TotalJobs { get; init; }
    
    /// <summary>
    /// Metrics by job type
    /// </summary>
    public Dictionary<string, JobTypeMetrics> MetricsByType { get; init; } = [];
}

/// <summary>
/// Metrics for a specific job type
/// </summary>
public sealed class JobTypeMetrics {
    /// <summary>
    /// Job type name
    /// </summary>
    public string JobType { get; init; } = "";
    
    /// <summary>
    /// Total number of jobs of this type
    /// </summary>
    public int TotalCount { get; init; }
    
    /// <summary>
    /// Number of successful jobs
    /// </summary>
    public int SuccessCount { get; init; }
    
    /// <summary>
    /// Number of failed jobs
    /// </summary>
    public int FailureCount { get; init; }
    
    /// <summary>
    /// Average processing duration in milliseconds
    /// </summary>
    public double AverageProcessingDurationMs { get; init; }
    
    /// <summary>
    /// Failure rate for this job type
    /// </summary>
    public double FailureRate { get; init; }
}
