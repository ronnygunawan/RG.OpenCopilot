namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Default implementation of retry policy calculator
/// </summary>
internal sealed class RetryPolicyCalculator : IRetryPolicyCalculator {
    private readonly Random _random = new();

    /// <inheritdoc />
    public int CalculateRetryDelay(RetryPolicy policy, int retryCount) {
        if (!policy.Enabled) {
            return 0;
        }

        // Calculate base delay based on strategy
        var baseDelay = policy.BackoffStrategy switch {
            RetryBackoffStrategy.Constant => policy.BaseDelayMilliseconds,
            RetryBackoffStrategy.Linear => policy.BaseDelayMilliseconds * (retryCount + 1),
            RetryBackoffStrategy.Exponential => policy.BaseDelayMilliseconds * (int)Math.Pow(2, retryCount),
            _ => policy.BaseDelayMilliseconds
        };

        // Cap at max delay
        var cappedDelay = Math.Min(baseDelay, policy.MaxDelayMilliseconds);

        // Apply jitter
        var delayWithJitter = ApplyJitter(cappedDelay, policy.MinJitterFactor, policy.MaxJitterFactor);

        return delayWithJitter;
    }

    /// <inheritdoc />
    public bool ShouldRetry(RetryPolicy policy, int retryCount, int maxRetries, bool shouldRetry) {
        if (!policy.Enabled) {
            return false;
        }

        if (!shouldRetry) {
            return false;
        }

        return retryCount < maxRetries;
    }

    private int ApplyJitter(int delay, double minJitterFactor, double maxJitterFactor) {
        // Generate random jitter between min and max factors
        // Jitter can reduce delay (negative) or increase it (positive)
        var jitterRange = maxJitterFactor - minJitterFactor;
        var randomJitter = minJitterFactor + (_random.NextDouble() * jitterRange);
        
        // Apply jitter as a percentage
        var jitteredDelay = delay * (1.0 + randomJitter);
        
        return (int)jitteredDelay;
    }
}
