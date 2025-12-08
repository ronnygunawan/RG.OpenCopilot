using Microsoft.Extensions.Hosting;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Background service that processes jobs from the queue
/// </summary>
internal sealed class BackgroundJobProcessor : BackgroundService {
    private readonly IJobQueue _jobQueue;
    private readonly JobDispatcher _jobDispatcher;
    private readonly BackgroundJobOptions _options;
    private readonly ILogger<BackgroundJobProcessor> _logger;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly List<Task> _processingTasks;

    public BackgroundJobProcessor(
        IJobQueue jobQueue,
        IJobDispatcher jobDispatcher,
        BackgroundJobOptions options,
        ILogger<BackgroundJobProcessor> logger) {
        _jobQueue = jobQueue;
        _jobDispatcher = (JobDispatcher)jobDispatcher;
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
        try {
            // Create a combined cancellation token for the job
            job.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var jobToken = job.CancellationTokenSource.Token;

            _logger.LogInformation("Processing job {JobId} of type {JobType}", job.Id, job.Type);

            // Get handler for job type
            var handler = _jobDispatcher.GetHandler(job.Type);
            if (handler == null) {
                _logger.LogError("No handler found for job type {JobType}", job.Type);
                return;
            }

            // Execute job
            JobResult result;
            try {
                result = await handler.ExecuteAsync(job, jobToken);
            }
            catch (OperationCanceledException) {
                _logger.LogWarning("Job {JobId} was cancelled", job.Id);
                return;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Job {JobId} failed with exception", job.Id);
                result = JobResult.CreateFailure(ex.Message, exception: ex, shouldRetry: true);
            }

            // Handle result
            if (result.Success) {
                _logger.LogInformation("Job {JobId} completed successfully", job.Id);
            }
            else {
                _logger.LogWarning("Job {JobId} failed: {ErrorMessage}", job.Id, result.ErrorMessage);

                // Retry if enabled and job should be retried
                if (_options.EnableRetry && result.ShouldRetry && job.RetryCount < job.MaxRetries) {
                    _logger.LogInformation("Retrying job {JobId} (attempt {RetryCount}/{MaxRetries})",
                        job.Id, job.RetryCount + 1, job.MaxRetries);

                    // Wait before retry
                    await Task.Delay(_options.RetryDelayMilliseconds, stoppingToken);

                    // Re-enqueue with incremented retry count
                    var retryJob = job.CreateRetryJob();
                    await _jobQueue.EnqueueAsync(retryJob, stoppingToken);
                }
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Unexpected error processing job {JobId}", job.Id);
        }
        finally {
            // Release semaphore and cleanup
            _concurrencySemaphore.Release();
            _jobDispatcher.RemoveActiveJob(job.Id);
            job.CancellationTokenSource?.Dispose();
        }
    }

    public override void Dispose() {
        _concurrencySemaphore.Dispose();
        base.Dispose();
    }
}
