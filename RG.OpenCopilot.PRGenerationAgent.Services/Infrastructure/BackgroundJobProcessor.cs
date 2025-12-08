using Microsoft.Extensions.Hosting;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Background service that processes jobs from the queue
/// </summary>
internal sealed class BackgroundJobProcessor : BackgroundService {
    private readonly IJobQueue _jobQueue;
    private readonly JobDispatcher _jobDispatcher;
    private readonly IJobStatusStore _jobStatusStore;
    private readonly IRetryPolicyCalculator _retryPolicyCalculator;
    private readonly IJobDeduplicationService _deduplicationService;
    private readonly BackgroundJobOptions _options;
    private readonly ILogger<BackgroundJobProcessor> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly List<Task> _processingTasks;

    public BackgroundJobProcessor(
        IJobQueue jobQueue,
        IJobDispatcher jobDispatcher,
        IJobStatusStore jobStatusStore,
        IRetryPolicyCalculator retryPolicyCalculator,
        IJobDeduplicationService deduplicationService,
        BackgroundJobOptions options,
        ILogger<BackgroundJobProcessor> logger) {
        _jobQueue = jobQueue;
        _jobDispatcher = (JobDispatcher)jobDispatcher;
        _jobStatusStore = jobStatusStore;
        _retryPolicyCalculator = retryPolicyCalculator;
        _deduplicationService = deduplicationService;
        _options = options;
        _logger = logger;
        _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrency, _options.MaxConcurrency);
        _processingTasks = [];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("Background job processor starting with max concurrency: {MaxConcurrency}", _options.MaxConcurrency);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                // Wait for available concurrency slot
                await _concurrencySemaphore.WaitAsync(stoppingToken);

                // Dequeue next job
                var job = await _jobQueue.DequeueAsync(stoppingToken);
                if (job == null) {
                    _concurrencySemaphore.Release();
                    continue;
                }

                // Process job in background
                var processingTask = ProcessJobAsync(job, stoppingToken);
                _processingTasks.Add(processingTask);

                // Clean up completed tasks
                _processingTasks.RemoveAll(t => t.IsCompleted);
            }
            catch (OperationCanceledException) {
                // Expected during shutdown
                break;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error in job processor main loop");
                _concurrencySemaphore.Release();
            }
        }

        // Wait for all processing tasks to complete during shutdown
        _logger.LogInformation("Waiting for {Count} jobs to complete...", _processingTasks.Count);
        var shutdownTimeout = TimeSpan.FromSeconds(_options.ShutdownTimeoutSeconds);
        var shutdownCts = new CancellationTokenSource(shutdownTimeout);
        
        try {
            await Task.WhenAll(_processingTasks).WaitAsync(shutdownCts.Token);
            _logger.LogInformation("All jobs completed successfully");
        }
        catch (OperationCanceledException) {
            _logger.LogWarning("Shutdown timeout reached, some jobs may not have completed");
        }
    }

    private async Task ProcessJobAsync(BackgroundJob job, CancellationToken stoppingToken) {
        var startedAt = DateTime.UtcNow;
        var queueWaitTimeMs = (startedAt - job.CreatedAt).TotalMilliseconds;

        try {
            // Create a combined cancellation token for the job
            job.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var jobToken = job.CancellationTokenSource.Token;

            // Update status to Processing
            await UpdateJobStatusAsync(job, BackgroundJobStatus.Processing, startedAt: startedAt, queueWaitTimeMs: (long)queueWaitTimeMs);
            _logger.LogInformation("Job {JobId} of type {JobType} transitioned to Processing (waited {WaitTimeMs}ms in queue)",
                job.Id, job.Type, (long)queueWaitTimeMs);

            // Get handler for job type
            var handler = _jobDispatcher.GetHandler(job.Type);
            if (handler == null) {
                _logger.LogError("No handler found for job type {JobType}", job.Type);
                await UpdateJobStatusAsync(
                    job,
                    BackgroundJobStatus.Failed,
                    completedAt: DateTime.UtcNow,
                    errorMessage: $"No handler found for job type {job.Type}",
                    startedAt: startedAt,
                    queueWaitTimeMs: (long)queueWaitTimeMs);
                return;
            }

            // Execute job
            JobResult result;
            try {
                result = await handler.ExecuteAsync(job, jobToken);
            }
            catch (OperationCanceledException) {
                _logger.LogWarning("Job {JobId} was cancelled", job.Id);
                await UpdateJobStatusAsync(job, BackgroundJobStatus.Cancelled, completedAt: DateTime.UtcNow, startedAt: startedAt, queueWaitTimeMs: (long)queueWaitTimeMs);
                return;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Job {JobId} failed with exception", job.Id);
                result = JobResult.CreateFailure(ex.Message, exception: ex, shouldRetry: true);
            }

            var completedAt = DateTime.UtcNow;
            var processingDurationMs = (completedAt - startedAt).TotalMilliseconds;

            // Create attempt record
            var attempt = new JobAttempt {
                AttemptNumber = job.RetryCount + 1,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Succeeded = result.Success,
                ErrorMessage = result.ErrorMessage,
                ExceptionType = result.Exception?.GetType().Name,
                DurationMs = (long)processingDurationMs,
                DelayBeforeAttemptMs = 0, // Will be set for retries
                BackoffStrategy = _options.RetryPolicy.BackoffStrategy
            };

            // Handle result
            if (result.Success) {
                _logger.LogInformation("Job {JobId} completed successfully (processing took {DurationMs}ms)", job.Id, (long)processingDurationMs);
                await UpdateJobStatusAsync(
                    job,
                    BackgroundJobStatus.Completed,
                    completedAt: completedAt,
                    startedAt: startedAt,
                    processingDurationMs: (long)processingDurationMs,
                    queueWaitTimeMs: (long)queueWaitTimeMs,
                    attempt: attempt);
            }
            else {
                _logger.LogWarning("Job {JobId} failed: {ErrorMessage}", job.Id, result.ErrorMessage);

                // Use retry policy to determine if job should be retried
                var retryPolicy = _options.RetryPolicy;
                var shouldRetry = _retryPolicyCalculator.ShouldRetry(retryPolicy, job.RetryCount, job.MaxRetries, result.ShouldRetry);

                if (shouldRetry) {
                    // Calculate retry delay using policy calculator
                    var retryDelayMs = _retryPolicyCalculator.CalculateRetryDelay(retryPolicy, job.RetryCount);
                    
                    _logger.LogInformation("Job {JobId} transitioned to Retried (attempt {RetryCount}/{MaxRetries}), delay: {DelayMs}ms",
                        job.Id, job.RetryCount + 1, job.MaxRetries, retryDelayMs);

                    await UpdateJobStatusAsync(
                        job,
                        BackgroundJobStatus.Retried,
                        errorMessage: result.ErrorMessage,
                        lastRetryAt: DateTime.UtcNow,
                        startedAt: startedAt,
                        processingDurationMs: (long)processingDurationMs,
                        queueWaitTimeMs: (long)queueWaitTimeMs,
                        attempt: attempt);

                    // Wait before retry
                    await Task.Delay(retryDelayMs, stoppingToken);

                    // Re-enqueue with incremented retry count
                    var retryJob = job.CreateRetryJob();
                    await _jobQueue.EnqueueAsync(retryJob, stoppingToken);
                }
                else {
                    // Job has exceeded max retries or should not retry - move to dead-letter
                    var status = job.RetryCount >= job.MaxRetries && result.ShouldRetry
                        ? BackgroundJobStatus.DeadLetter
                        : BackgroundJobStatus.Failed;

                    if (status == BackgroundJobStatus.DeadLetter) {
                        _logger.LogError("Job {JobId} moved to DeadLetter queue after {RetryCount} retries", job.Id, job.RetryCount);
                    }

                    await UpdateJobStatusAsync(
                        job,
                        status,
                        completedAt: completedAt,
                        errorMessage: result.ErrorMessage,
                        startedAt: startedAt,
                        processingDurationMs: (long)processingDurationMs,
                        queueWaitTimeMs: (long)queueWaitTimeMs,
                        attempt: attempt);
                }
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error processing job {JobId}", job.Id);
            
            // Create failed attempt record
            var failedAttempt = new JobAttempt {
                AttemptNumber = job.RetryCount + 1,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                Succeeded = false,
                ErrorMessage = ex.Message,
                ExceptionType = ex.GetType().Name,
                DurationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds,
                DelayBeforeAttemptMs = 0,
                BackoffStrategy = _options.RetryPolicy.BackoffStrategy
            };
            
            await UpdateJobStatusAsync(
                job,
                BackgroundJobStatus.Failed,
                completedAt: DateTime.UtcNow,
                errorMessage: ex.Message,
                startedAt: startedAt,
                queueWaitTimeMs: (long)queueWaitTimeMs,
                attempt: failedAttempt);
        }
        finally {
            // Release semaphore and cleanup
            _concurrencySemaphore.Release();
            _jobDispatcher.RemoveActiveJob(job.Id);
            job.CancellationTokenSource?.Dispose();
            
            // Unregister from deduplication if job is complete
            if (!string.IsNullOrEmpty(job.IdempotencyKey)) {
                await _deduplicationService.UnregisterJobAsync(job.Id);
            }
        }
    }

    private async Task UpdateJobStatusAsync(
        BackgroundJob job,
        BackgroundJobStatus status,
        DateTime? startedAt = null,
        DateTime? completedAt = null,
        string? errorMessage = null,
        DateTime? lastRetryAt = null,
        long? processingDurationMs = null,
        long? queueWaitTimeMs = null,
        JobAttempt? attempt = null) {
        try {
            var existingStatus = await _jobStatusStore.GetStatusAsync(job.Id);

            // Merge attempts - keep existing attempts and add new one
            var attempts = existingStatus?.Attempts ?? [];
            if (attempt != null) {
                attempts = [..attempts, attempt];
            }

            var statusInfo = new BackgroundJobStatusInfo {
                JobId = job.Id,
                JobType = job.Type,
                Status = status,
                CreatedAt = job.CreatedAt,
                StartedAt = startedAt ?? existingStatus?.StartedAt,
                CompletedAt = completedAt,
                ErrorMessage = errorMessage,
                Metadata = job.Metadata,
                RetryCount = job.RetryCount,
                MaxRetries = job.MaxRetries,
                LastRetryAt = lastRetryAt ?? existingStatus?.LastRetryAt,
                Source = job.Metadata.GetValueOrDefault("Source", ""),
                ParentJobId = job.Metadata.GetValueOrDefault("ParentJobId"),
                CorrelationId = job.Metadata.GetValueOrDefault("CorrelationId"),
                ProcessingDurationMs = processingDurationMs,
                QueueWaitTimeMs = queueWaitTimeMs,
                IdempotencyKey = job.IdempotencyKey,
                Attempts = attempts
            };

            await _jobStatusStore.SetStatusAsync(statusInfo);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to update job status for {JobId}", job.Id);
        }
    }

    public override void Dispose() {
        _concurrencySemaphore.Dispose();
        base.Dispose();
    }
}
