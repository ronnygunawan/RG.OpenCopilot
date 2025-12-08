namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Retry backoff strategy for failed jobs
/// </summary>
public enum RetryBackoffStrategy {
    /// <summary>
    /// Constant delay between retries (use configured RetryDelayMilliseconds)
    /// </summary>
    Constant,

    /// <summary>
    /// Linear backoff: delay increases linearly with each retry
    /// delay = baseDelay * (retryCount + 1)
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential backoff: delay increases exponentially with each retry
    /// delay = baseDelay * (2 ^ retryCount)
    /// </summary>
    Exponential
}
