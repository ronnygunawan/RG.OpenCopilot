namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Represents a single execution attempt of a background job
/// </summary>
public sealed class JobAttempt {
    /// <summary>
    /// Attempt number (1-based)
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// When the attempt started
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// When the attempt completed (successfully or failed)
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>
    /// Whether the attempt succeeded
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Error message if the attempt failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Exception type if the attempt failed
    /// </summary>
    public string? ExceptionType { get; init; }

    /// <summary>
    /// Duration of the attempt in milliseconds
    /// </summary>
    public long? DurationMs { get; init; }

    /// <summary>
    /// Delay before this attempt in milliseconds (0 for first attempt)
    /// </summary>
    public int DelayBeforeAttemptMs { get; init; }

    /// <summary>
    /// Backoff strategy used for this attempt
    /// </summary>
    public RetryBackoffStrategy? BackoffStrategy { get; init; }
}
