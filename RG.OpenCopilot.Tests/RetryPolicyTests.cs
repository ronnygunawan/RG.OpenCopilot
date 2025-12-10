using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Tests for retry policy, backoff strategies, and idempotency features
/// </summary>
public class RetryPolicyTests {
    [Fact]
    public void RetryPolicyCalculator_ConstantBackoff_ReturnsConstantDelay() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Constant,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0 // No jitter for predictable test
        };

        // Act
        var delay1 = calculator.CalculateRetryDelay(policy, retryCount: 0);
        var delay2 = calculator.CalculateRetryDelay(policy, retryCount: 1);
        var delay3 = calculator.CalculateRetryDelay(policy, retryCount: 2);

        // Assert
        delay1.ShouldBe(1000);
        delay2.ShouldBe(1000);
        delay3.ShouldBe(1000);
    }

    [Fact]
    public void RetryPolicyCalculator_LinearBackoff_IncreasesLinearly() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Linear,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0 // No jitter for predictable test
        };

        // Act
        var delay0 = calculator.CalculateRetryDelay(policy, retryCount: 0);
        var delay1 = calculator.CalculateRetryDelay(policy, retryCount: 1);
        var delay2 = calculator.CalculateRetryDelay(policy, retryCount: 2);

        // Assert
        delay0.ShouldBe(1000);  // 1000 * (0 + 1) = 1000
        delay1.ShouldBe(2000);  // 1000 * (1 + 1) = 2000
        delay2.ShouldBe(3000);  // 1000 * (2 + 1) = 3000
    }

    [Fact]
    public void RetryPolicyCalculator_ExponentialBackoff_IncreasesExponentially() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Exponential,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0 // No jitter for predictable test
        };

        // Act
        var delay0 = calculator.CalculateRetryDelay(policy, retryCount: 0);
        var delay1 = calculator.CalculateRetryDelay(policy, retryCount: 1);
        var delay2 = calculator.CalculateRetryDelay(policy, retryCount: 2);
        var delay3 = calculator.CalculateRetryDelay(policy, retryCount: 3);

        // Assert
        delay0.ShouldBe(1000);   // 1000 * 2^0 = 1000
        delay1.ShouldBe(2000);   // 1000 * 2^1 = 2000
        delay2.ShouldBe(4000);   // 1000 * 2^2 = 4000
        delay3.ShouldBe(8000);   // 1000 * 2^3 = 8000
    }

    [Fact]
    public void RetryPolicyCalculator_ExponentialBackoff_RespectsMaxDelay() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Exponential,
            BaseDelayMilliseconds = 1000,
            MaxDelayMilliseconds = 5000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0 // No jitter for predictable test
        };

        // Act
        var delay5 = calculator.CalculateRetryDelay(policy, retryCount: 5);  // Would be 32000 without cap
        var delay10 = calculator.CalculateRetryDelay(policy, retryCount: 10); // Would be very large without cap

        // Assert
        delay5.ShouldBe(5000);  // Capped at MaxDelayMilliseconds
        delay10.ShouldBe(5000); // Capped at MaxDelayMilliseconds
    }

    [Fact]
    public void RetryPolicyCalculator_WithJitter_ReturnsDelayInExpectedRange() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Constant,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = -0.1,  // 10% reduction
            MaxJitterFactor = 0.2    // 20% addition
        };

        // Act - run multiple times to test jitter randomness
        var delays = new List<int>();
        for (int i = 0; i < 100; i++) {
            delays.Add(calculator.CalculateRetryDelay(policy, retryCount: 0));
        }

        // Assert
        // All delays should be between 900 (1000 * 0.9) and 1200 (1000 * 1.2)
        delays.ShouldAllBe(d => d >= 900 && d <= 1200);
        
        // Should have some variation (not all the same)
        var uniqueDelays = delays.Distinct().Count();
        uniqueDelays.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void RetryPolicyCalculator_DisabledPolicy_ReturnsZeroDelay() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            Enabled = false,
            BaseDelayMilliseconds = 1000
        };

        // Act
        var delay = calculator.CalculateRetryDelay(policy, retryCount: 0);

        // Assert
        delay.ShouldBe(0);
    }

    [Fact]
    public void RetryPolicyCalculator_ShouldRetry_ReturnsTrueWhenConditionsMet() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            Enabled = true,
            MaxRetries = 3
        };

        // Act & Assert
        calculator.ShouldRetry(policy, retryCount: 0, maxRetries: 3, shouldRetry: true).ShouldBeTrue();
        calculator.ShouldRetry(policy, retryCount: 1, maxRetries: 3, shouldRetry: true).ShouldBeTrue();
        calculator.ShouldRetry(policy, retryCount: 2, maxRetries: 3, shouldRetry: true).ShouldBeTrue();
    }

    [Fact]
    public void RetryPolicyCalculator_ShouldRetry_ReturnsFalseWhenMaxRetriesReached() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            Enabled = true,
            MaxRetries = 3
        };

        // Act & Assert - at max retries, should not retry
        calculator.ShouldRetry(policy, retryCount: 3, maxRetries: 3, shouldRetry: true).ShouldBeFalse();
        calculator.ShouldRetry(policy, retryCount: 4, maxRetries: 3, shouldRetry: true).ShouldBeFalse();
    }

    [Fact]
    public void RetryPolicyCalculator_ShouldRetry_ReturnsFalseWhenPolicyDisabled() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            Enabled = false,
            MaxRetries = 3
        };

        // Act & Assert
        calculator.ShouldRetry(policy, retryCount: 0, maxRetries: 3, shouldRetry: true).ShouldBeFalse();
    }

    [Fact]
    public void RetryPolicyCalculator_ShouldRetry_ReturnsFalseWhenShouldRetryIsFalse() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            Enabled = true,
            MaxRetries = 3
        };

        // Act & Assert - even with retries available, if shouldRetry is false, return false
        calculator.ShouldRetry(policy, retryCount: 0, maxRetries: 3, shouldRetry: false).ShouldBeFalse();
    }

    [Fact]
    public async Task JobDeduplicationService_DetectsDuplicateJob() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();
        var idempotencyKey = "test-key-123";

        // Act
        await service.RegisterJobAsync(jobId: "job-1", idempotencyKey);
        var inFlightJobId = await service.GetInFlightJobAsync(idempotencyKey);

        // Assert
        inFlightJobId.ShouldBe("job-1");
    }

    [Fact]
    public async Task JobDeduplicationService_ReturnsNullForNewIdempotencyKey() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();

        // Act
        var inFlightJobId = await service.GetInFlightJobAsync("new-key");

        // Assert
        inFlightJobId.ShouldBeNull();
    }

    [Fact]
    public async Task JobDeduplicationService_UnregisterRemovesJob() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();
        var idempotencyKey = "test-key-123";

        // Act
        await service.RegisterJobAsync(jobId: "job-1", idempotencyKey);
        await service.UnregisterJobAsync(jobId: "job-1");
        var inFlightJobId = await service.GetInFlightJobAsync(idempotencyKey);

        // Assert
        inFlightJobId.ShouldBeNull();
    }

    [Fact]
    public async Task JobDeduplicationService_ClearAllRemovesAllJobs() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();
        await service.RegisterJobAsync(jobId: "job-1", idempotencyKey: "key-1");
        await service.RegisterJobAsync(jobId: "job-2", idempotencyKey: "key-2");

        // Act
        await service.ClearAllAsync();

        // Assert
        var job1 = await service.GetInFlightJobAsync("key-1");
        var job2 = await service.GetInFlightJobAsync("key-2");
        job1.ShouldBeNull();
        job2.ShouldBeNull();
    }

    [Fact]
    public void RetryPolicy_Default_HasExpectedValues() {
        // Arrange & Act
        var policy = RetryPolicy.Default;

        // Assert
        policy.Enabled.ShouldBeTrue();
        policy.MaxRetries.ShouldBe(3);
        policy.BackoffStrategy.ShouldBe(RetryBackoffStrategy.Exponential);
        policy.BaseDelayMilliseconds.ShouldBe(5000);
        policy.MaxDelayMilliseconds.ShouldBe(300000);
        policy.MinJitterFactor.ShouldBe(0.0);
        policy.MaxJitterFactor.ShouldBe(0.2);
    }

    [Fact]
    public void RetryPolicy_NoRetry_DisablesRetry() {
        // Arrange & Act
        var policy = RetryPolicy.NoRetry;

        // Assert
        policy.Enabled.ShouldBeFalse();
        policy.MaxRetries.ShouldBe(0);
    }

    [Fact]
    public void JobAttempt_StoresAttemptInformation() {
        // Arrange & Act
        var timeProvider = new FakeTimeProvider();
        var attempt = new JobAttempt {
            AttemptNumber = 1,
            StartedAt = timeProvider.GetUtcNow().DateTime.AddMinutes(-1),
            CompletedAt = timeProvider.GetUtcNow().DateTime,
            Succeeded = false,
            ErrorMessage = "Test error",
            ExceptionType = "TestException",
            DurationMs = 1000,
            DelayBeforeAttemptMs = 0,
            BackoffStrategy = RetryBackoffStrategy.Exponential
        };

        // Assert
        attempt.AttemptNumber.ShouldBe(1);
        attempt.Succeeded.ShouldBeFalse();
        attempt.ErrorMessage.ShouldBe("Test error");
        attempt.ExceptionType.ShouldBe("TestException");
        attempt.DurationMs.ShouldBe(1000);
        attempt.BackoffStrategy.ShouldBe(RetryBackoffStrategy.Exponential);
    }

    [Fact]
    public void BackgroundJobOptions_BackwardCompatibility_WithDeprecatedFields() {
        // Arrange & Act
        var options = new BackgroundJobOptions {
            EnableRetry = true,
            RetryDelayMilliseconds = 2000
        };

        var retryPolicy = options.RetryPolicy;

        // Assert - should create policy from deprecated fields
        retryPolicy.Enabled.ShouldBeTrue();
        retryPolicy.BaseDelayMilliseconds.ShouldBe(2000);
        retryPolicy.BackoffStrategy.ShouldBe(RetryBackoffStrategy.Constant);
        retryPolicy.MinJitterFactor.ShouldBe(0.0);
        retryPolicy.MaxJitterFactor.ShouldBe(0.0);
    }

    [Fact]
    public void BackgroundJobOptions_UsesExplicitRetryPolicy_WhenProvided() {
        // Arrange & Act
        var explicitPolicy = new RetryPolicy {
            Enabled = true,
            BackoffStrategy = RetryBackoffStrategy.Linear,
            BaseDelayMilliseconds = 3000,
            MaxRetries = 5
        };

        var options = new BackgroundJobOptions {
            EnableRetry = false,  // This should be ignored
            RetryDelayMilliseconds = 1000,  // This should be ignored
            RetryPolicy = explicitPolicy
        };

        var retryPolicy = options.RetryPolicy;

        // Assert - should use explicit policy, not deprecated fields
        retryPolicy.Enabled.ShouldBeTrue();
        retryPolicy.BackoffStrategy.ShouldBe(RetryBackoffStrategy.Linear);
        retryPolicy.BaseDelayMilliseconds.ShouldBe(3000);
        retryPolicy.MaxRetries.ShouldBe(5);
    }
}
