using Moq;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for retry and failure handling scenarios
/// Tests transient vs persistent failures, retry eligibility, and dead-letter handling
/// </summary>
public class RetryFailureHandlingIntegrationTests {
    /// <summary>
    /// Test that transient failures (network timeouts, temporary service unavailability) 
    /// are retried with exponential backoff
    /// </summary>
    [Fact]
    public async Task TransientFailure_NetworkTimeout_RetriesWithExponentialBackoff() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 3,
                BackoffStrategy = RetryBackoffStrategy.Exponential,
                BaseDelayMilliseconds = 100,
                MinJitterFactor = 0.0,
                MaxJitterFactor = 0.0
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
        var finalAttemptTcs = new TaskCompletionSource<bool>();
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TransientFailureJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                
                // Fail first 2 attempts with transient error, succeed on 3rd
                if (attemptCount < 3) {
                    return JobResult.CreateFailure(
                        errorMessage: "Network timeout - temporary failure",
                        shouldRetry: true);
                }
                finalAttemptTcs.SetResult(true);
                return JobResult.CreateSuccess();
            });
        
        dispatcher.RegisterHandler(handler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, TimeProvider.System, processorLogger);
        
        // Act
        using var cts = new CancellationTokenSource();
        _ = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TransientFailureJob",
            Payload = "test",
            MaxRetries = 3
        };
        await dispatcher.DispatchAsync(job);
        
        // Wait for all retries to complete
        await finalAttemptTcs.Task;
        
        // Wait for the processor to finish updating status with all attempts
        // Poll status until all 3 attempts are recorded
        var maxWaitIterations = 100;
        BackgroundJobStatusInfo? finalStatus = null;
        for (int i = 0; i < maxWaitIterations; i++) {
            finalStatus = await statusStore.GetStatusAsync(job.Id);
            if (finalStatus?.Attempts.Count == 3) {
                break;
            }
            // Yield to allow the processor task to complete
            await Task.Yield();
        }
        
        // Verify the polling succeeded and all 3 attempts are recorded
        finalStatus.ShouldNotBeNull("Polling timed out waiting for job status to be updated");
        finalStatus.Attempts.Count.ShouldBe(3, "Polling completed but not all attempts were recorded");
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert
        attemptCount.ShouldBe(3);
        
        // Verify final status
        finalStatus.ShouldNotBeNull();
        finalStatus.Status.ShouldBe(BackgroundJobStatus.Completed);
        finalStatus.Attempts.Count.ShouldBe(3);
        finalStatus.Attempts[0].Succeeded.ShouldBeFalse();
        finalStatus.Attempts[1].Succeeded.ShouldBeFalse();
        finalStatus.Attempts[2].Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// Test that persistent failures (invalid data, permanent errors) 
    /// do NOT retry and fail immediately
    /// </summary>
    [Fact]
    public async Task PersistentFailure_InvalidData_DoesNotRetry() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 3,
                BackoffStrategy = RetryBackoffStrategy.Constant,
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
        
        var attemptTcs = new TaskCompletionSource<bool>();
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("PersistentFailureJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                // Permanent failure - invalid payload format
                var result = JobResult.CreateFailure(
                    errorMessage: "Invalid payload format - cannot be retried",
                    shouldRetry: false); // Explicitly mark as non-retryable
                attemptTcs.SetResult(true);
                return result;
            });
        
        dispatcher.RegisterHandler(handler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, TimeProvider.System, processorLogger);
        
        // Act
        using var cts = new CancellationTokenSource();
        _ = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "PersistentFailureJob",
            Payload = "invalid-data",
            MaxRetries = 3
        };
        await dispatcher.DispatchAsync(job);
        
        // Wait for processing
        await attemptTcs.Task;
        
        // Wait for the processor to finish updating status
        // Poll status until it's marked as failed
        var maxWaitIterations = 100;
        BackgroundJobStatusInfo? finalStatus = null;
        for (int i = 0; i < maxWaitIterations; i++) {
            finalStatus = await statusStore.GetStatusAsync(job.Id);
            if (finalStatus?.Status == BackgroundJobStatus.Failed) {
                break;
            }
            // Yield to allow the processor task to complete
            await Task.Yield();
        }
        
        // Verify the polling succeeded and job status is Failed
        finalStatus.ShouldNotBeNull("Polling timed out waiting for job status to be updated");
        finalStatus.Status.ShouldBe(BackgroundJobStatus.Failed, "Polling completed but job status was not marked as Failed");
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - should only execute once (no retries)
        handler.Verify(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()), Times.Once);
        
        finalStatus.ShouldNotBeNull();
        finalStatus.Status.ShouldBe(BackgroundJobStatus.Failed);
        finalStatus.RetryCount.ShouldBe(0);
        finalStatus.Attempts.Count.ShouldBe(1);
        finalStatus.ErrorMessage.ShouldContain("Invalid payload");
    }

    /// <summary>
    /// Test that jobs exceeding max retries move to dead-letter queue
    /// with complete attempt history
    /// </summary>
    [Fact]
    public async Task ExhaustedRetries_MovesToDeadLetterQueue_WithCompleteHistory() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 3,
                BackoffStrategy = RetryBackoffStrategy.Linear,
                BaseDelayMilliseconds = 50,
                MinJitterFactor = 0.0,
                MaxJitterFactor = 0.0
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
        var finalAttemptTcs = new TaskCompletionSource<bool>();
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("ExhaustRetriesJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackgroundJob job, CancellationToken ct) => {
                attemptCount++;
                var errorMsg = $"Attempt {job.RetryCount + 1} failed";
                if (attemptCount >= 3) {
                    finalAttemptTcs.SetResult(true);
                }
                return JobResult.CreateFailure(
                    errorMessage: errorMsg,
                    shouldRetry: true);
            });
        
        dispatcher.RegisterHandler(handler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, TimeProvider.System, processorLogger);
        
        // Act
        using var cts = new CancellationTokenSource();
        _ = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "ExhaustRetriesJob",
            Payload = "test",
            MaxRetries = 2  // 1 initial + 2 retries = 3 total attempts
        };
        await dispatcher.DispatchAsync(job);
        
        // Wait for all retries
        await finalAttemptTcs.Task;
        
        // Wait for the processor to finish updating status
        // Poll status until we see all 3 attempts recorded
        var maxWaitIterations = 100;
        BackgroundJobStatusInfo? finalStatus = null;
        for (int i = 0; i < maxWaitIterations; i++) {
            finalStatus = await statusStore.GetStatusAsync(job.Id);
            if (finalStatus?.Attempts.Count >= 3) {
                break;
            }
            // Yield to allow the processor task to complete
            await Task.Yield();
        }
        
        // Verify the polling succeeded
        finalStatus.ShouldNotBeNull("Polling timed out waiting for job status to be updated");
        finalStatus.Attempts.Count.ShouldBeGreaterThanOrEqualTo(3, "Polling completed but expected attempts were not recorded");
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert
        attemptCount.ShouldBe(3); // Initial + 2 retries
        
        // finalStatus was already retrieved in the polling loop above
        finalStatus.ShouldNotBeNull();
        finalStatus.Status.ShouldBe(BackgroundJobStatus.DeadLetter);
        finalStatus.RetryCount.ShouldBe(2);
        
        // Verify complete attempt history
        finalStatus.Attempts.Count.ShouldBe(3);
        for (int i = 0; i < 3; i++) {
            var attempt = finalStatus.Attempts[i];
            attempt.AttemptNumber.ShouldBe(i + 1);
            attempt.Succeeded.ShouldBeFalse();
            attempt.ErrorMessage.ShouldContain($"Attempt {i + 1}");
            attempt.StartedAt.ShouldNotBe(default(DateTime));
            attempt.CompletedAt.ShouldNotBe(null);
            attempt.DurationMs.ShouldNotBeNull();
            attempt.DurationMs.Value.ShouldBeGreaterThanOrEqualTo(0);
        }
        
        // Verify can query dead-letter jobs
        var deadLetterJobs = await statusStore.GetJobsByStatusAsync(BackgroundJobStatus.DeadLetter);
        deadLetterJobs.Count.ShouldBeGreaterThan(0);
        deadLetterJobs.Any(j => j.JobId == job.Id).ShouldBeTrue();
    }

    /// <summary>
    /// Test retry eligibility based on different error types
    /// </summary>
    [Fact]
    public async Task RetryEligibility_VariousErrorTypes_HandledCorrectly() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 3,
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
        
        var validationComplete = new TaskCompletionSource<bool>();
        var timeoutAttempts = 0;
        var networkAttempts = 0;
        var allJobsComplete = new TaskCompletionSource<bool>();
        
        // Test case 1: ArgumentException - should NOT retry (validation error)
        var validationHandler = new Mock<IJobHandler>();
        validationHandler.Setup(h => h.JobType).Returns("ValidationJob");
        validationHandler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                validationComplete.SetResult(true);
                return JobResult.CreateFailure(
                    errorMessage: "Validation error: missing required field",
                    exception: new ArgumentException("Missing field"),
                    shouldRetry: false);
            });
        
        // Test case 2: TimeoutException - should retry (transient)
        var timeoutHandler = new Mock<IJobHandler>();
        timeoutHandler.Setup(h => h.JobType).Returns("TimeoutJob");
        timeoutHandler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                timeoutAttempts++;
                if (timeoutAttempts >= 4 && networkAttempts >= 4) {
                    allJobsComplete.TrySetResult(true);
                }
                return JobResult.CreateFailure(
                    errorMessage: "Request timeout",
                    exception: new TimeoutException("Timeout"),
                    shouldRetry: true);
            });
        
        // Test case 3: HttpRequestException - should retry (transient)
        var networkHandler = new Mock<IJobHandler>();
        networkHandler.Setup(h => h.JobType).Returns("NetworkJob");
        networkHandler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                networkAttempts++;
                if (timeoutAttempts >= 4 && networkAttempts >= 4) {
                    allJobsComplete.TrySetResult(true);
                }
                return JobResult.CreateFailure(
                    errorMessage: "Network error",
                    exception: new HttpRequestException("Connection failed"),
                    shouldRetry: true);
            });
        
        dispatcher.RegisterHandler(validationHandler.Object);
        dispatcher.RegisterHandler(timeoutHandler.Object);
        dispatcher.RegisterHandler(networkHandler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, TimeProvider.System, processorLogger);
        
        // Act
        using var cts = new CancellationTokenSource();
        _ = processor.StartAsync(cts.Token);
        
        var validationJob = new BackgroundJob { Type = "ValidationJob", MaxRetries = 3 };
        var timeoutJob = new BackgroundJob { Type = "TimeoutJob", MaxRetries = 3 };
        var networkJob = new BackgroundJob { Type = "NetworkJob", MaxRetries = 3 };
        
        await dispatcher.DispatchAsync(validationJob);
        await dispatcher.DispatchAsync(timeoutJob);
        await dispatcher.DispatchAsync(networkJob);
        
        await allJobsComplete.Task;
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert
        var validationStatus = await statusStore.GetStatusAsync(validationJob.Id);
        validationStatus.ShouldNotBeNull();
        validationStatus.Status.ShouldBe(BackgroundJobStatus.Failed);
        validationStatus.RetryCount.ShouldBe(0); // No retries for validation error
        validationStatus.Attempts.Count.ShouldBe(1);
        
        var timeoutStatus = await statusStore.GetStatusAsync(timeoutJob.Id);
        timeoutStatus.ShouldNotBeNull();
        timeoutStatus.Status.ShouldBe(BackgroundJobStatus.DeadLetter);
        timeoutStatus.RetryCount.ShouldBeGreaterThan(0); // Retried multiple times
        
        var networkStatus = await statusStore.GetStatusAsync(networkJob.Id);
        networkStatus.ShouldNotBeNull();
        networkStatus.Status.ShouldBe(BackgroundJobStatus.DeadLetter);
        networkStatus.RetryCount.ShouldBeGreaterThan(0); // Retried multiple times
    }

    /// <summary>
    /// Test idempotency - duplicate jobs with same key are rejected
    /// </summary>
    [Fact]
    public async Task Idempotency_DuplicateJobsWithSameKey_SecondIsRejected() {
        // Arrange
        var options = new BackgroundJobOptions();
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var logger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, logger);
        
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("IdempotentJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JobResult.CreateSuccess());
        dispatcher.RegisterHandler(handler.Object);
        
        var idempotencyKey = "order:12345:process-payment";
        
        // Act
        var job1 = new BackgroundJob {
            Type = "IdempotentJob",
            Payload = "payment-data",
            IdempotencyKey = idempotencyKey
        };
        
        var job2 = new BackgroundJob {
            Type = "IdempotentJob",
            Payload = "payment-data",
            IdempotencyKey = idempotencyKey  // Same key
        };
        
        var dispatched1 = await dispatcher.DispatchAsync(job1);
        var dispatched2 = await dispatcher.DispatchAsync(job2);
        
        // Assert
        dispatched1.ShouldBeTrue();
        dispatched2.ShouldBeFalse(); // Duplicate rejected
        queue.Count.ShouldBe(1); // Only first job in queue
    }

    /// <summary>
    /// Test idempotency cleanup - after job completes, key is released
    /// </summary>
    [Fact]
    public async Task Idempotency_AfterJobCompletes_KeyIsReleased() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = false
            }
        };
        var queue = new ChannelJobQueue(options);
        var statusStore = new InMemoryJobStatusStore();
        var deduplicationService = new InMemoryJobDeduplicationService();
        var retryCalculator = new RetryPolicyCalculator();
        var dispatcherLogger = new TestLogger<JobDispatcher>();
        var dispatcher = new JobDispatcher(queue, statusStore, deduplicationService, dispatcherLogger);
        var processorLogger = new TestLogger<BackgroundJobProcessor>();
        
        var jobCompleteTcs = new TaskCompletionSource<bool>();
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("IdempotentJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                jobCompleteTcs.SetResult(true);
                return JobResult.CreateSuccess();
            });
        dispatcher.RegisterHandler(handler.Object);
        
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, TimeProvider.System, processorLogger);
        
        var idempotencyKey = "order:12345:process-payment";
        
        // Act
        using var cts = new CancellationTokenSource();
        _ = processor.StartAsync(cts.Token);
        
        var job1 = new BackgroundJob {
            Type = "IdempotentJob",
            IdempotencyKey = idempotencyKey
        };
        await dispatcher.DispatchAsync(job1);
        
        // Wait for job to complete
        await jobCompleteTcs.Task;
        
        // Wait for the processor to finish updating job status and releasing idempotency key
        // Poll status until it's marked as completed
        var maxWaitIterations = 100;
        BackgroundJobStatusInfo? job1Status = null;
        for (int i = 0; i < maxWaitIterations; i++) {
            job1Status = await statusStore.GetStatusAsync(job1.Id);
            if (job1Status?.Status == BackgroundJobStatus.Completed) {
                break;
            }
            // Yield to allow the processor task to complete
            await Task.Yield();
        }
        
        // Verify the polling succeeded and job status is completed
        job1Status.ShouldNotBeNull("Polling timed out waiting for job status to be updated");
        job1Status.Status.ShouldBe(BackgroundJobStatus.Completed, "Polling completed but job status was not marked as Completed");
        
        // Try to dispatch another job with same key
        var job2 = new BackgroundJob {
            Type = "IdempotentJob",
            IdempotencyKey = idempotencyKey
        };
        var dispatched2 = await dispatcher.DispatchAsync(job2);
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert - second job should be accepted since first completed
        dispatched2.ShouldBeTrue();
    }

    /// <summary>
    /// Test attempt history includes correct backoff strategy information
    /// </summary>
    [Fact]
    public async Task AttemptHistory_IncludesBackoffStrategyInformation() {
        // Arrange
        var options = new BackgroundJobOptions {
            MaxConcurrency = 1,
            RetryPolicy = new RetryPolicy {
                Enabled = true,
                MaxRetries = 2,
                BackoffStrategy = RetryBackoffStrategy.Linear,
                BaseDelayMilliseconds = 100,
                MinJitterFactor = 0.0,
                MaxJitterFactor = 0.0
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
        var finalAttemptTcs = new TaskCompletionSource<bool>();
        var handler = new Mock<IJobHandler>();
        handler.Setup(h => h.JobType).Returns("TestJob");
        handler.Setup(h => h.ExecuteAsync(It.IsAny<BackgroundJob>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => {
                attemptCount++;
                if (attemptCount >= 3) {
                    finalAttemptTcs.SetResult(true);
                }
                return JobResult.CreateFailure("Test error", shouldRetry: true);
            });
        
        dispatcher.RegisterHandler(handler.Object);
        var processor = new BackgroundJobProcessor(queue, dispatcher, statusStore, retryCalculator, deduplicationService, options, TimeProvider.System, processorLogger);
        
        // Act
        using var cts = new CancellationTokenSource();
        _ = processor.StartAsync(cts.Token);
        
        var job = new BackgroundJob {
            Type = "TestJob",
            MaxRetries = 2
        };
        await dispatcher.DispatchAsync(job);
        
        await finalAttemptTcs.Task;
        
        cts.Cancel();
        await processor.StopAsync(CancellationToken.None);
        
        // Assert
        var status = await statusStore.GetStatusAsync(job.Id);
        status.ShouldNotBeNull();
        status.Attempts.Count.ShouldBe(3);
        
        foreach (var attempt in status.Attempts) {
            attempt.BackoffStrategy.ShouldBe(RetryBackoffStrategy.Linear);
            attempt.StartedAt.ShouldNotBe(default(DateTime));
            attempt.CompletedAt.ShouldNotBe(null);
            attempt.DurationMs.ShouldNotBeNull();
            attempt.DurationMs.Value.ShouldBeGreaterThanOrEqualTo(0);
        }
    }

    private class TestLogger<T> : ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
