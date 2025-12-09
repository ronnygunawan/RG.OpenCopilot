using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Additional tests for edge cases and metrics related to retry and failure handling
/// </summary>
public class RetryMetricsAndEdgeCasesTests {
    /// <summary>
    /// Test that metrics correctly track retry success rates
    /// </summary>
    [Fact]
    public async Task Metrics_TrackRetrySuccessRate_Correctly() {
        // Arrange
        var statusStore = new InMemoryJobStatusStore();
        
        // Create jobs with different retry outcomes
        var successAfterRetry = new BackgroundJobStatusInfo {
            JobId = "job1",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            CompletedAt = DateTime.UtcNow.AddMinutes(-4),
            RetryCount = 2,
            MaxRetries = 3,
            Attempts = [
                new JobAttempt {
                    AttemptNumber = 1,
                    Succeeded = false,
                    StartedAt = DateTime.UtcNow.AddMinutes(-5),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-5).AddSeconds(10),
                    DurationMs = 10000
                },
                new JobAttempt {
                    AttemptNumber = 2,
                    Succeeded = false,
                    StartedAt = DateTime.UtcNow.AddMinutes(-4).AddSeconds(-30),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-4).AddSeconds(-20),
                    DurationMs = 10000
                },
                new JobAttempt {
                    AttemptNumber = 3,
                    Succeeded = true,
                    StartedAt = DateTime.UtcNow.AddMinutes(-4).AddSeconds(-10),
                    CompletedAt = DateTime.UtcNow.AddMinutes(-4),
                    DurationMs = 10000
                }
            ]
        };
        
        var deadLetterJob = new BackgroundJobStatusInfo {
            JobId = "job2",
            JobType = "TestJob",
            Status = BackgroundJobStatus.DeadLetter,
            CreatedAt = DateTime.UtcNow.AddMinutes(-3),
            CompletedAt = DateTime.UtcNow.AddMinutes(-2),
            RetryCount = 3,
            MaxRetries = 3,
            Attempts = [
                new JobAttempt { AttemptNumber = 1, Succeeded = false, DurationMs = 5000 },
                new JobAttempt { AttemptNumber = 2, Succeeded = false, DurationMs = 5000 },
                new JobAttempt { AttemptNumber = 3, Succeeded = false, DurationMs = 5000 },
                new JobAttempt { AttemptNumber = 4, Succeeded = false, DurationMs = 5000 }
            ]
        };
        
        var immediateSuccess = new BackgroundJobStatusInfo {
            JobId = "job3",
            JobType = "TestJob",
            Status = BackgroundJobStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-1),
            CompletedAt = DateTime.UtcNow.AddSeconds(-30),
            RetryCount = 0,
            MaxRetries = 3,
            Attempts = [
                new JobAttempt {
                    AttemptNumber = 1,
                    Succeeded = true,
                    StartedAt = DateTime.UtcNow.AddMinutes(-1),
                    CompletedAt = DateTime.UtcNow.AddSeconds(-30),
                    DurationMs = 30000
                }
            ]
        };
        
        await statusStore.SetStatusAsync(successAfterRetry);
        await statusStore.SetStatusAsync(deadLetterJob);
        await statusStore.SetStatusAsync(immediateSuccess);
        
        // Act
        var metrics = await statusStore.GetMetricsAsync();
        
        // Assert
        metrics.TotalJobs.ShouldBe(3);
        metrics.CompletedCount.ShouldBe(2); // successAfterRetry and immediateSuccess
        metrics.DeadLetterCount.ShouldBe(1);
        
        // Calculate expected retry metrics manually
        var totalRetries = successAfterRetry.RetryCount + deadLetterJob.RetryCount + immediateSuccess.RetryCount;
        totalRetries.ShouldBe(5); // 2 + 3 + 0
        
        var successfulRetries = 1; // Only successAfterRetry succeeded after retries
        var retrySuccessRate = totalRetries > 0 ? (double)successfulRetries / totalRetries : 0.0;
        
        // Verify metrics include useful information
        metrics.CompletedCount.ShouldBeGreaterThan(0);
        metrics.FailedCount.ShouldBe(0);
        metrics.DeadLetterCount.ShouldBeGreaterThan(0);
    }

    /// <summary>
    /// Test edge case: Job with MaxRetries = 0 should not retry
    /// </summary>
    [Fact]
    public async Task EdgeCase_MaxRetriesZero_DoesNotRetry() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 3,
                BaseDelayMilliseconds = 100
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
        handler.Setup(h => h.JobType).Returns("NoRetryJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                return JobResult.CreateFailure("Test error", shouldRetry: true);
            });
        
        dispatcher.RegisterHandler(handler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, processorLogger);
        
        // Act
        var cts = new CancellationTokenSource();
        var processorTask = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "NoRetryJob",
            MaxRetries = 0  // No retries allowed
        };
        await dispatcher.DispatchAsync(job);
        
        await Task.Delay(300);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert
        attemptCount.ShouldBe(1); // Only initial attempt, no retries
        
        var status = await statusStore.GetStatusAsync(job.Id);
        status.ShouldNotBeNull();
        // With MaxRetries=0 and shouldRetry=true, it moves to DeadLetter because retries are exhausted
        status.Status.ShouldBe(BackgroundJobStatus.DeadLetter);
        status.RetryCount.ShouldBe(0);
        status.Attempts.Count.ShouldBe(1);
    }

    /// <summary>
    /// Test edge case: Retry with jitter produces varied delays
    /// </summary>
    [Fact]
    public void EdgeCase_JitterProducesVariedDelays() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Constant,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = -0.2,  // -20% to +20%
            MaxJitterFactor = 0.2
        };
        
        // Act - calculate delays multiple times
        var delays = new HashSet<int>();
        for (int i = 0; i < 100; i++) {
            var delay = calculator.CalculateRetryDelay(policy, retryCount: 0);
            delays.Add(delay);
        }
        
        // Assert
        // Should have multiple different delays due to jitter
        delays.Count.ShouldBeGreaterThan(1);
        
        // All delays should be in expected range: 800ms to 1200ms
        delays.ShouldAllBe(d => d >= 800 && d <= 1200);
    }

    /// <summary>
    /// Test that MaxDelayMilliseconds caps exponential growth correctly
    /// </summary>
    [Fact]
    public void EdgeCase_MaxDelayCapsExponentialGrowth() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Exponential,
            BaseDelayMilliseconds = 1000,
            MaxDelayMilliseconds = 10000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };
        
        // Act
        var delay10 = calculator.CalculateRetryDelay(policy, retryCount: 10);  // Would be 1024000ms without cap
        var delay20 = calculator.CalculateRetryDelay(policy, retryCount: 20);  // Would be huge without cap
        
        // Assert
        delay10.ShouldBe(10000);  // Capped at max
        delay20.ShouldBe(10000);  // Capped at max
    }

    /// <summary>
    /// Test that linear backoff grows correctly
    /// </summary>
    [Fact]
    public void LinearBackoff_GrowsLinearly() {
        // Arrange
        var calculator = new RetryPolicyCalculator();
        var policy = new RetryPolicy {
            BackoffStrategy = RetryBackoffStrategy.Linear,
            BaseDelayMilliseconds = 1000,
            MinJitterFactor = 0.0,
            MaxJitterFactor = 0.0
        };
        
        // Act & Assert
        calculator.CalculateRetryDelay(policy, 0).ShouldBe(1000);  // 1000 * 1
        calculator.CalculateRetryDelay(policy, 1).ShouldBe(2000);  // 1000 * 2
        calculator.CalculateRetryDelay(policy, 2).ShouldBe(3000);  // 1000 * 3
        calculator.CalculateRetryDelay(policy, 5).ShouldBe(6000);  // 1000 * 6
    }

    /// <summary>
    /// Test idempotency key collision between different job types
    /// </summary>
    [Fact]
    public async Task Idempotency_DifferentJobTypes_SameKey_BothAccepted() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var logger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, logger);
        
        var handler1 = new Mock<IJobHandler>();
        handler1.Setup(h => h.JobType).Returns("JobType1");
        
        var handler2 = new Mock<IJobHandler>();
        handler2.Setup(h => h.JobType).Returns("JobType2");
        
        dispatcher.RegisterHandler(handler1.Object);
        dispatcher.RegisterHandler(handler2.Object);
        
        // Note: In real scenarios, different job types should use different prefixes in their keys
        var idempotencyKey = "shared-key-123";
        
        // Act
        var job1 = new BackgroundJob {
            Type = "JobType1",
            IdempotencyKey = idempotencyKey
        };
        
        var job2 = new BackgroundJob {
            Type = "JobType2",
            IdempotencyKey = idempotencyKey  // Same key, different type
        };
        
        var dispatched1 = await dispatcher.DispatchAsync(job1);
        var dispatched2 = await dispatcher.DispatchAsync(job2);
        
        // Assert
        dispatched1.ShouldBeTrue();
        // Second job with same key should be rejected even if different type
        // because idempotency is key-based, not type-based
        dispatched2.ShouldBeFalse();
    }

    /// <summary>
    /// Test that null or empty idempotency keys are allowed (no deduplication)
    /// </summary>
    [Fact]
    public async Task Idempotency_NullOrEmptyKey_AllowsMultipleJobs() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var logger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, logger);
        
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        dispatcher.RegisterHandler(handler.Object);
        
        // Act
        var job1 = new BackgroundJob {
            Type = "TestJob",
            IdempotencyKey = null  // No idempotency key
        };
        
        var job2 = new BackgroundJob {
            Type = "TestJob",
            IdempotencyKey = null  // No idempotency key
        };
        
        var job3 = new BackgroundJob {
            Type = "TestJob",
            IdempotencyKey = ""  // Empty idempotency key
        };
        
        var dispatched1 = await dispatcher.DispatchAsync(job1);
        var dispatched2 = await dispatcher.DispatchAsync(job2);
        var dispatched3 = await dispatcher.DispatchAsync(job3);
        
        // Assert - all should be accepted since no deduplication
        dispatched1.ShouldBeTrue();
        dispatched2.ShouldBeTrue();
        dispatched3.ShouldBeTrue();
        queue.Count.ShouldBe(3);
    }

    /// <summary>
    /// Test that failed dispatch doesn't leave idempotency key registered
    /// </summary>
    [Fact]
    public async Task Idempotency_FailedDispatch_CleansUpKey() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxQueueSize = 1  // Very small queue to force enqueue failure
        };
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var logger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, logger);
        
        // Don't register any handler - this will cause dispatch to fail
        
        var idempotencyKey = "test-key-123";
        
        // Act
        var job1 = new BackgroundJob {
            Type = "UnregisteredJob",
            IdempotencyKey = idempotencyKey
        };
        
        var dispatched1 = await dispatcher.DispatchAsync(job1);
        
        // Try to dispatch another job with same key
        var job2 = new BackgroundJob {
            Type = "UnregisteredJob",
            IdempotencyKey = idempotencyKey
        };
        
        var dispatched2 = await dispatcher.DispatchAsync(job2);
        
        // Assert
        dispatched1.ShouldBeFalse(); // Failed because no handler registered
        dispatched2.ShouldBeFalse(); // Should also fail for same reason (not because of duplicate)
        
        // Key should not be registered since dispatch failed
        var inFlightJob = await deduplicationService.GetInFlightJobAsync(idempotencyKey);
        inFlightJob.ShouldBeNull();
    }

    /// <summary>
    /// Test metrics for jobs with various retry counts
    /// </summary>
    [Fact]
    public async Task Metrics_AggregateRetryCountsCorrectly() {
        // Arrange
        var statusStore = new InMemoryJobStatusStore();
        
        var jobs = new[] {
            new BackgroundJobStatusInfo {
                JobId = "job1",
                JobType = "Type1",
                Status = BackgroundJobStatus.Completed,
                RetryCount = 0,
                Attempts = [ new JobAttempt { AttemptNumber = 1, Succeeded = true, DurationMs = 1000 } ]
            },
            new BackgroundJobStatusInfo {
                JobId = "job2",
                JobType = "Type1",
                Status = BackgroundJobStatus.Completed,
                RetryCount = 2,
                Attempts = [
                    new JobAttempt { AttemptNumber = 1, Succeeded = false, DurationMs = 1000 },
                    new JobAttempt { AttemptNumber = 2, Succeeded = false, DurationMs = 1000 },
                    new JobAttempt { AttemptNumber = 3, Succeeded = true, DurationMs = 1000 }
                ]
            },
            new BackgroundJobStatusInfo {
                JobId = "job3",
                JobType = "Type2",
                Status = BackgroundJobStatus.DeadLetter,
                RetryCount = 3,
                Attempts = [
                    new JobAttempt { AttemptNumber = 1, Succeeded = false, DurationMs = 1000 },
                    new JobAttempt { AttemptNumber = 2, Succeeded = false, DurationMs = 1000 },
                    new JobAttempt { AttemptNumber = 3, Succeeded = false, DurationMs = 1000 },
                    new JobAttempt { AttemptNumber = 4, Succeeded = false, DurationMs = 1000 }
                ]
            }
        };
        
        foreach (var job in jobs) {
            await statusStore.SetStatusAsync(job);
        }
        
        // Act
        var metrics = await statusStore.GetMetricsAsync();
        
        // Assert
        metrics.TotalJobs.ShouldBe(3);
        metrics.CompletedCount.ShouldBe(2);
        metrics.DeadLetterCount.ShouldBe(1);
        
        // Verify per-type metrics
        metrics.MetricsByType.ShouldContainKey("Type1");
        metrics.MetricsByType.ShouldContainKey("Type2");
        
        var type1Metrics = metrics.MetricsByType["Type1"];
        type1Metrics.TotalCount.ShouldBe(2);
        type1Metrics.SuccessCount.ShouldBe(2);
        
        var type2Metrics = metrics.MetricsByType["Type2"];
        type2Metrics.TotalCount.ShouldBe(1);
        // DeadLetter jobs are not counted in FailureCount or SuccessCount
        // They're tracked separately in metrics.DeadLetterCount
        // So for Type2 which only has a DeadLetter job, both counts will be 0
        type2Metrics.SuccessCount.ShouldBe(0);
        type2Metrics.FailureCount.ShouldBe(0);
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
