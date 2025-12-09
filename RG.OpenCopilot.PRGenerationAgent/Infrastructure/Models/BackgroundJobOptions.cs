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
    /// Timeout in seconds for plan generation phase (0 for no timeout)
    /// </summary>
    public int PlanTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Timeout in seconds for plan execution phase (0 for no timeout)
    /// </summary>
    public int ExecutionTimeoutSeconds { get; init; } = 1800;

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
    /// If not explicitly set, it will be derived from the deprecated EnableRetry and RetryDelayMilliseconds properties
    /// </summary>
    public RetryPolicy RetryPolicy {
        get {
            // If explicitly set (non-default), use it
            if (_retryPolicy != null) {
                return _retryPolicy;
            }

            // Cache synthesized policy to ensure consistent reference equality
            if (_synthesizedRetryPolicy == null) {
                _synthesizedRetryPolicy = new RetryPolicy {
                    Enabled = EnableRetry,
                    MaxRetries = 3,
                    BackoffStrategy = RetryBackoffStrategy.Constant,
                    BaseDelayMilliseconds = RetryDelayMilliseconds,
                    MaxDelayMilliseconds = 300000,
                    MinJitterFactor = 0.0,
                    MaxJitterFactor = 0.0 // No jitter for backward compatibility
                };
            }
            return _synthesizedRetryPolicy;
        }
        init {
            _retryPolicy = value;
            _synthesizedRetryPolicy = null;
        }
    }

    private readonly RetryPolicy? _retryPolicy;
    private RetryPolicy? _synthesizedRetryPolicy;
}
