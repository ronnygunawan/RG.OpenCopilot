namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Default implementation of retry policy calculator
/// </summary>
internal sealed class RetryPolicyCalculator : IRetryPolicyCalculator {

    /// <inheritdoc />
    public int CalculateRetryDelay(RetryPolicy policy, int retryCount) {
        if (!policy.Enabled) {
            return 0;
        }

        // Calculate base delay based on strategy, using long to prevent overflow
        long baseDelay = policy.BackoffStrategy switch {
            RetryBackoffStrategy.Constant => policy.BaseDelayMilliseconds,
            RetryBackoffStrategy.Linear => (long)policy.BaseDelayMilliseconds * (retryCount + 1),
            RetryBackoffStrategy.Exponential => CalculateExponentialDelay(policy.BaseDelayMilliseconds, retryCount),
            _ => policy.BaseDelayMilliseconds
        };

        // Cap at max delay before converting to int
        var cappedDelay = (int)Math.Min(baseDelay, policy.MaxDelayMilliseconds);

        // Apply jitter
        var delayWithJitter = ApplyJitter(cappedDelay, policy.MinJitterFactor, policy.MaxJitterFactor);

        return delayWithJitter;
    }

    private static long CalculateExponentialDelay(int baseDelay, int retryCount) {
        // Handle negative retry counts (defensive programming)
        if (retryCount < 0) {
            // 2^-1 = 0.5, 2^-2 = 0.25, etc.
            return (long)(baseDelay * Math.Pow(2, retryCount));
        }

        // Calculate 2^retryCount safely
        // If retryCount is too large, just return a very large value that will be capped
        if (retryCount >= 31) {
            // 2^31 or larger would overflow int, return max to trigger capping
            return long.MaxValue;
        }

        // Safe to calculate now
        long multiplier = 1L << retryCount; // Equivalent to (long)Math.Pow(2, retryCount) but faster and exact
        return (long)baseDelay * multiplier;
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
        var randomJitter = minJitterFactor + (Random.Shared.NextDouble() * jitterRange);
        
        // Apply jitter as a percentage
        var jitteredDelay = delay * (1.0 + randomJitter);
        
        // Prevent integer overflow when casting from double to int
        return (int)Math.Min(jitteredDelay, int.MaxValue);
    }
}
