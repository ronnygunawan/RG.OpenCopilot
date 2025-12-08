using System.Collections.Concurrent;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Manages job dispatching and handler registration
/// </summary>
internal sealed class JobDispatcher : IJobDispatcher {
    private readonly IJobQueue _jobQueue;
    private readonly IJobStatusStore _jobStatusStore;
    private readonly ConcurrentDictionary<string, IJobHandler> _handlers;
    private readonly ConcurrentDictionary<string, BackgroundJob> _activeJobs;
    private readonly ILogger<JobDispatcher> _logger;

    public JobDispatcher(
        IJobQueue jobQueue,
        IJobStatusStore jobStatusStore,
        ILogger<JobDispatcher> logger) {
        _jobQueue = jobQueue;
        _jobStatusStore = jobStatusStore;
        _logger = logger;
        _handlers = new ConcurrentDictionary<string, IJobHandler>();
        _activeJobs = new ConcurrentDictionary<string, BackgroundJob>();
    }

    public void RegisterHandler(IJobHandler handler) {
        if (!_handlers.TryAdd(handler.JobType, handler)) {
            _logger.LogWarning("Handler for job type {JobType} is already registered", handler.JobType);
        }
        else {
            _logger.LogInformation("Registered handler for job type: {JobType}", handler.JobType);
        }
    }

    public async Task<bool> DispatchAsync(BackgroundJob job, CancellationToken cancellationToken = default) {
        if (!_handlers.ContainsKey(job.Type)) {
            _logger.LogError("No handler registered for job type: {JobType}", job.Type);
            return false;
        }

        _activeJobs.TryAdd(job.Id, job);
        var enqueued = await _jobQueue.EnqueueAsync(job, cancellationToken);

        if (enqueued) {
            _logger.LogInformation("Job {JobId} of type {JobType} transitioned to Queued", job.Id, job.Type);

            // Track job status as Queued
            var statusInfo = new BackgroundJobStatusInfo {
                JobId = job.Id,
                JobType = job.Type,
                Status = BackgroundJobStatus.Queued,
                CreatedAt = job.CreatedAt,
                Metadata = job.Metadata,
                RetryCount = job.RetryCount,
                MaxRetries = job.MaxRetries,
                Source = job.Metadata.GetValueOrDefault("Source", ""),
                ParentJobId = job.Metadata.GetValueOrDefault("ParentJobId"),
                CorrelationId = job.Metadata.GetValueOrDefault("CorrelationId")
            };

            await _jobStatusStore.SetStatusAsync(statusInfo, cancellationToken);
        }
        else {
            _activeJobs.TryRemove(job.Id, out _);
            _logger.LogWarning("Failed to dispatch job {JobId} of type {JobType}", job.Id, job.Type);
        }

        return enqueued;
    }

    public bool CancelJob(string jobId) {
        if (_activeJobs.TryGetValue(jobId, out var job) && job.CancellationTokenSource != null) {
            job.CancellationTokenSource.Cancel();
            _logger.LogInformation("Cancelled job {JobId}", jobId);
            return true;
        }

        _logger.LogWarning("Job {JobId} not found or already completed", jobId);
        return false;
    }

    public IJobHandler? GetHandler(string jobType) {
        _handlers.TryGetValue(jobType, out var handler);
        return handler;
    }

    public void RemoveActiveJob(string jobId) {
        _activeJobs.TryRemove(jobId, out _);
    }
}
