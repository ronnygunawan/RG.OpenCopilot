using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Negative test cases for retry and failure handling
/// Tests invalid inputs, error conditions, boundary violations, and failure scenarios
/// </summary>
public class RetryFailureHandlingNegativeTests {
    /// <summary>
    /// Test that negative retry count returns 0 delay (defensive programming)
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_NegativeRetryCount_ReturnsBaseDelay() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Exponential,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };

        // Act
        var delay = calculator.CalculateRetryDelay(policy, retryCount: -1);

        // Assert - negative retry count should still work (defensive)
        // Math.Pow(2, -1) = 0.5, so 1000 * 0.5 = 500
        delay.ShouldBe(500);
    }

    /// <summary>
    /// Test that very large retry counts don't cause overflow
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_VeryLargeRetryCount_DoesNotOverflow() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Exponential,
            BaseDelayMilliseconds = 1000,
            MaxDelayMilliseconds = 10000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };

        // Act - very large retry count that would overflow without capping
        var delay = calculator.CalculateRetryDelay(policy, retryCount: 100);

        // Assert - should be capped at MaxDelayMilliseconds
        delay.ShouldBe(10000);
    }

    /// <summary>
    /// Test that zero base delay works correctly
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_ZeroBaseDelay_ReturnsZero() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Linear,
            BaseDelayMilliseconds = 0,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };

        // Act
        var delay = calculator.CalculateRetryDelay(policy, retryCount: 5);

        // Assert
        delay.ShouldBe(0);
    }

    /// <summary>
    /// Test that negative base delay is handled (should return negative or zero)
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_NegativeBaseDelay_HandledCorrectly() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Constant,
            BaseDelayMilliseconds = -1000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };

        // Act
        var delay = calculator.CalculateRetryDelay(policy, retryCount: 0);

        // Assert - negative delay should be returned as-is (caller's responsibility to validate)
        delay.ShouldBe(-1000);
    }

    /// <summary>
    /// Test extreme jitter factors (beyond 100% reduction/addition)
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_ExtremeJitterFactors_WorksCorrectly() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Constant,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = -2.0,  // 200% reduction
            MaxJitterFactor = 3.0    // 300% addition
        };

        // Act - run multiple times to test range
        var delays = new List<int>();
        for (int i = 0; i < 50; i++) {
            delays.Add(calculator.CalculateRetryDelay(policy, retryCount: 0));
        }

        // Assert - all delays should be in expected range
        // Delay can be from -1000 (1000 * -1) to 4000 (1000 * 4)
        delays.ShouldAllBe(d => d >= -1000 && d <= 4000);
    }

    /// <summary>
    /// Test ShouldRetry with negative maxRetries
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_NegativeMaxRetries_ReturnsFalse() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy { Enabled = true };

        // Act
        var shouldRetry = calculator.ShouldRetry(policy, retryCount: 0, maxRetries: -1, shouldRetry: true);

        // Assert - negative maxRetries means no retries
        shouldRetry.ShouldBeFalse();
    }

    /// <summary>
    /// Test ShouldRetry with negative retry count
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_NegativeRetryCount_ShouldRetryIfValid() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy { Enabled = true };

        // Act
        var shouldRetry = calculator.ShouldRetry(policy, retryCount: -1, maxRetries: 3, shouldRetry: true);

        // Assert - negative retry count is less than maxRetries
        shouldRetry.ShouldBeTrue();
    }

    /// <summary>
    /// Test idempotency service with null/empty keys behaves correctly
    /// The ConcurrentDictionary will throw ArgumentNullException for null keys
    /// </summary>
    [Fact]
    public async Task JobDeduplicationService_NullIdempotencyKey_ThrowsException() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();

        // Act & Assert - null idempotencyKey should cause ArgumentNullException from ConcurrentDictionary
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.RegisterJobAsync(jobId: "job-123", idempotencyKey: null!));
    }

    /// <summary>
    /// Test idempotency service get with null key
    /// </summary>
    [Fact]
    public async Task JobDeduplicationService_GetWithNullKey_ThrowsException() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();

        // Act & Assert - null key should throw
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await service.GetInFlightJobAsync(null!));
    }

    /// <summary>
    /// Test concurrent registration of same idempotency key
    /// Last writer wins behavior
    /// </summary>
    [Fact]
    public async Task JobDeduplicationService_ConcurrentRegistration_LastWriterWins() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();
        var idempotencyKey = "concurrent-key";

        // Act - register multiple jobs with same key concurrently
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++) {
            var jobId = $"job-{i}";
            tasks.Add(service.RegisterJobAsync(jobId, idempotencyKey));
        }
        await Task.WhenAll(tasks);

        // Get the final registered job
        var registeredJobId = await service.GetInFlightJobAsync(idempotencyKey);

        // Assert - one of the jobs should be registered (last writer wins)
        registeredJobId.ShouldNotBeNull();
        registeredJobId.ShouldStartWith("job-");
    }

    /// <summary>
    /// Test unregister non-existent job (should not throw)
    /// </summary>
    [Fact]
    public async Task JobDeduplicationService_UnregisterNonExistent_NoError() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();

        // Act & Assert - should not throw
        await service.UnregisterJobAsync("non-existent-job");
        
        // Verify no side effects
        var result = await service.GetInFlightJobAsync("any-key");
        result.ShouldBeNull();
    }

    /// <summary>
    /// Test get with empty string idempotency key
    /// </summary>
    [Fact]
    public async Task JobDeduplicationService_EmptyStringKey_ReturnsNull() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();

        // Act
        var result = await service.GetInFlightJobAsync("");

        // Assert
        result.ShouldBeNull();
    }

    /// <summary>
    /// Test job dispatch with handler that throws on first call
    /// </summary>
    [Fact]
    public async Task BackgroundJobProcessor_HandlerThrowsException_MovesToRetryOrFailed() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 1,
                BaseDelayMilliseconds = 50
            }
        };
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var retryCalculator = new RetryPolicyCalculator();
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("ThrowingJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Handler failed"));
        
        dispatcher.RegisterHandler(handler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "ThrowingJob",
            MaxRetries = 1
        };
        await dispatcher.DispatchAsync(job);
        
        await Task.Delay(200);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should have attempted retry due to exception
        var status = await statusStore.GetStatusAsync(job.Id);
        status.ShouldNotBeNull();
        // Status should be either Retried or DeadLetter depending on retry exhaustion
        (status.Status == BackgroundJobStatus.Retried || 
         status.Status == BackgroundJobStatus.DeadLetter).ShouldBeTrue();
    }

    /// <summary>
    /// Test queue full scenario - enqueue should fail
    /// </summary>
    [Fact]
    public async Task JobQueue_QueueFull_EnqueueFails() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 1,
            EnablePrioritization = false
        };
        var queue = new ChannelJobQueue(options);

        // Act - fill the queue
        var job1 = new BackgroundJob { Type = "Job1" };
        var enqueued1 = await queue.EnqueueAsync(job1);

        // Try to add one more (should fail as queue is full)
        var job2 = new BackgroundJob { Type = "Job2" };
        var enqueued2 = await queue.EnqueueAsync(job2);

        // Assert
        enqueued1.ShouldBeTrue();
        enqueued2.ShouldBeFalse(); // Queue is full
    }

    /// <summary>
    /// Test job with both null IdempotencyKey and empty Payload
    /// </summary>
    [Fact]
    public async Task BackgroundJob_NullIdempotencyKeyAndEmptyPayload_DispatchesSuccessfully() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var logger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, logger);
        
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("EmptyJob");
        dispatcher.RegisterHandler(handler.Object);

        // Act
        var job = new BackgroundJob {
            Type = "EmptyJob",
            Payload = "",
            IdempotencyKey = null
        };
        var dispatched = await dispatcher.DispatchAsync(job);

        // Assert
        dispatched.ShouldBeTrue();
    }

    /// <summary>
    /// Test RetryPolicy with MinJitter > MaxJitter (invalid configuration)
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_MinJitterGreaterThanMax_ProducesValidDelay() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Constant,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = 0.5,   // Min > Max (invalid)
            MaxJitterFactor = 0.2
        };

        // Act - calculator should handle this gracefully
        var delays = new List<int>();
        for (int i = 0; i < 20; i++) {
            delays.Add(calculator.CalculateRetryDelay(policy, retryCount: 0));
        }

        // Assert - all delays should be in the "inverted" range
        // Since min > max, jitterRange will be negative
        // Delays will be between 1000 * (1 + 0.2) and 1000 * (1 + 0.5)
        delays.ShouldAllBe(d => d >= 1200 && d <= 1500);
    }

    /// <summary>
    /// Test job cancellation - verify attempt count is limited
    /// </summary>
    [Fact]
    public async Task BackgroundJobProcessor_LimitedRetries_StopsAtMax() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 2,  // Limited retries
                BaseDelayMilliseconds = 50,
                BackoffStrategy = RetryBackoffStrategy.Constant
            }
        };
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var retryCalculator = new RetryPolicyCalculator();
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var attemptCount = 0;
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("LimitedRetryJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                return JobResult.CreateFailure("Test failure", shouldRetry: true);
            });
        
        dispatcher.RegisterHandler(handler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "LimitedRetryJob",
            MaxRetries = 2
        };
        await dispatcher.DispatchAsync(job);
        
        // Wait for all retries
        await Task.Delay(200);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should have 3 attempts (1 initial + 2 retries)
        attemptCount.ShouldBe(3);
    }

    /// <summary>
    /// Test MaxDelayMilliseconds smaller than BaseDelayMilliseconds
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_MaxDelaySmallerThanBase_CapsAtMax() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Constant,
            BaseDelayMilliseconds = 5000,
            MaxDelayMilliseconds = 1000,  // Max < Base
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };

        // Act
        var delay = calculator.CalculateRetryDelay(policy, retryCount: 0);

        // Assert - should cap at MaxDelayMilliseconds
        delay.ShouldBe(1000);
    }

    /// <summary>
    /// Test dispatch with unregistered handler type
    /// </summary>
    [Fact]
    public async Task JobDispatcher_UnregisteredHandlerType_DispatchFails() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var logger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, logger);

        // Act - dispatch job without registering handler
        var job = new BackgroundJob {
            Type = "UnregisteredType",
            Payload = "test"
        };
        var dispatched = await dispatcher.DispatchAsync(job);

        // Assert
        dispatched.ShouldBeFalse();
        queue.Count.ShouldBe(0);
    }

    /// <summary>
    /// Test integer overflow in linear backoff
    /// </summary>
    [Fact]
    public void RetryPolicyCalculator_LinearBackoffOverflow_CapsCorrectly() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Linear,
            BaseDelayMilliseconds = int.MaxValue / 2,
            MaxDelayMilliseconds = int.MaxValue,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };

        // Act - large retry count that would overflow
        var delay = calculator.CalculateRetryDelay(policy, retryCount: 10);

        // Assert - should not throw, value may overflow but should be capped
        delay.ShouldBeGreaterThanOrEqualTo(0);
        delay.ShouldBeLessThanOrEqualTo(int.MaxValue);
    }

    /// <summary>
    /// Test retrying job with same ID overwrites previous registration
    /// </summary>
    [Fact]
    public async Task JobDeduplicationService_SameJobIdDifferentKeys_OverwritesPrevious() {
        // Arrange
        var service = new InMemoryJobDeduplicationService();
        var jobId = "job-123";

        // Act - register same job ID with different keys
        await service.RegisterJobAsync(jobId, "key-1");
        await service.RegisterJobAsync(jobId, "key-2");

        // Assert - key-1 should no longer be registered
        var job1 = await service.GetInFlightJobAsync("key-1");
        var job2 = await service.GetInFlightJobAsync("key-2");

        job1.ShouldBeNull(); // Old key cleaned up
        job2.ShouldBe(jobId); // New key registered
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
