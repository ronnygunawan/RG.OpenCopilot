namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Retry policy configuration for background jobs
/// </summary>
public sealed class RetryPolicy {
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Backoff strategy to use for retries
    /// </summary>
    public RetryBackoffStrategy BackoffStrategy { get; init; } = RetryBackoffStrategy.Exponential;

    /// <summary>
    /// Base delay in milliseconds for retry calculations
    /// </summary>
    public int BaseDelayMilliseconds { get; init; } = 5000;

    /// <summary>
    /// Maximum delay in milliseconds (caps exponential/linear growth)
    /// </summary>
    public int MaxDelayMilliseconds { get; init; } = 300000; // 5 minutes

    /// <summary>
    /// Minimum jitter percentage (0.0 to 1.0)
    /// Example: 0.0 means no jitter reduction, 0.1 means up to 10% reduction
    /// </summary>
    public double MinJitterFactor { get; init; } = 0.0;

    /// <summary>
    /// Maximum jitter percentage (0.0 to 1.0)
    /// Example: 0.0 means no jitter addition, 0.1 means up to 10% addition
    /// </summary>
    public double MaxJitterFactor { get; init; } = 0.2;

    /// <summary>
    /// Whether to enable retry for failed jobs
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Creates a default retry policy with exponential backoff
    /// </summary>
    public static RetryPolicy Default => new() {
        MaxRetries = 3,
        BackoffStrategy = RetryBackoffStrategy.Exponential,
        BaseDelayMilliseconds = 5000,
        MaxDelayMilliseconds = 300000,
        MinJitterFactor = 0.0,
        MaxJitterFactor = 0.2,
        Enabled = true
    };

    /// <summary>
    /// Creates a retry policy with no retries
    /// </summary>
    public static RetryPolicy NoRetry => new() {
        Enabled = false,
        MaxRetries = 0
    };
}
