namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Configuration for background job processing
/// </summary>
public sealed class BackgroundJobOptions {
    /// <summary>
    /// Maximum number of concurrent jobs to process
    /// </summary>
    public int MaxConcurrency { get; init; } = 2;

    /// <summary>
    /// Maximum queue size (0 for unlimited)
    /// </summary>
    public int MaxQueueSize { get; init; } = 100;

    /// <summary>
    /// Whether to enable job prioritization
    /// </summary>
    public bool EnablePrioritization { get; init; } = true;

    /// <summary>
    /// Shutdown timeout in seconds
    /// </summary>
    public int ShutdownTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Whether to retry failed jobs (deprecated, use RetryPolicy.Enabled)
    /// </summary>
    public bool EnableRetry { get; init; } = true;

    /// <summary>
    /// Delay in milliseconds between retry attempts (deprecated, use RetryPolicy.BaseDelayMilliseconds)
    /// </summary>
    public int RetryDelayMilliseconds { get; init; } = 5000;

    /// <summary>
    /// Retry policy configuration for failed jobs
    /// </summary>
    public RetryPolicy RetryPolicy { get; init; } = RetryPolicy.Default;
}
