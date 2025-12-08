using System.Collections.Concurrent;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// In-memory implementation of job status store
/// </summary>
internal sealed class InMemoryJobStatusStore : IJobStatusStore {
    private readonly ConcurrentDictionary<string, BackgroundJobStatusInfo> _statuses = new();

    public Task SetStatusAsync(BackgroundJobStatusInfo statusInfo, CancellationToken cancellationToken = default) {
        _statuses[statusInfo.JobId] = statusInfo;
        return Task.CompletedTask;
    }

    public Task<BackgroundJobStatusInfo?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default) {
        _statuses.TryGetValue(jobId, out var status);
        return Task.FromResult(status);
    }

    public Task DeleteStatusAsync(string jobId, CancellationToken cancellationToken = default) {
        _statuses.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    public Task<List<BackgroundJobStatusInfo>> GetJobsByStatusAsync(
        BackgroundJobStatus status,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default) {
        var jobs = _statuses.Values
            .Where(j => j.Status == status)
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(jobs);
    }

    public Task<List<BackgroundJobStatusInfo>> GetJobsByTypeAsync(
        string jobType,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default) {
        var jobs = _statuses.Values
            .Where(j => j.JobType == jobType)
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(jobs);
    }

    public Task<List<BackgroundJobStatusInfo>> GetJobsBySourceAsync(
        string source,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default) {
        var jobs = _statuses.Values
            .Where(j => j.Source == source)
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(jobs);
    }

    public Task<List<BackgroundJobStatusInfo>> GetJobsAsync(
        BackgroundJobStatus? status = null,
        string? jobType = null,
        string? source = null,
        int skip = 0,
        int take = 100,
        CancellationToken cancellationToken = default) {
        var query = _statuses.Values.AsEnumerable();

        if (status.HasValue) {
            query = query.Where(j => j.Status == status.Value);
        }

        if (!string.IsNullOrEmpty(jobType)) {
            query = query.Where(j => j.JobType == jobType);
        }

        if (!string.IsNullOrEmpty(source)) {
            query = query.Where(j => j.Source == source);
        }

        var jobs = query
            .OrderByDescending(j => j.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToList();

        return Task.FromResult(jobs);
    }

    public Task<JobMetrics> GetMetricsAsync(CancellationToken cancellationToken = default) {
        var allJobs = _statuses.Values.ToList();
        var totalJobs = allJobs.Count;

        var queuedJobs = allJobs.Where(j => j.Status == BackgroundJobStatus.Queued).ToList();
        var processingJobs = allJobs.Where(j => j.Status == BackgroundJobStatus.Processing).ToList();
        var completedJobs = allJobs.Where(j => j.Status == BackgroundJobStatus.Completed).ToList();
        var failedJobs = allJobs.Where(j => j.Status == BackgroundJobStatus.Failed).ToList();
        var cancelledJobs = allJobs.Where(j => j.Status == BackgroundJobStatus.Cancelled).ToList();
        var deadLetterJobs = allJobs.Where(j => j.Status == BackgroundJobStatus.DeadLetter).ToList();

        var jobsWithDuration = allJobs.Where(j => j.ProcessingDurationMs.HasValue).ToList();
        var jobsWithWaitTime = allJobs.Where(j => j.QueueWaitTimeMs.HasValue).ToList();

        var avgProcessingDuration = jobsWithDuration.Any()
            ? jobsWithDuration.Average(j => j.ProcessingDurationMs!.Value)
            : 0.0;

        var avgQueueWaitTime = jobsWithWaitTime.Any()
            ? jobsWithWaitTime.Average(j => j.QueueWaitTimeMs!.Value)
            : 0.0;

        var failureRate = totalJobs > 0
            ? (double)failedJobs.Count / totalJobs
            : 0.0;

        var metricsByType = allJobs
            .GroupBy(j => j.JobType)
            .Select(g => {
                var typeJobs = g.ToList();
                var typeCompleted = typeJobs.Count(j => j.Status == BackgroundJobStatus.Completed);
                var typeFailed = typeJobs.Count(j => j.Status == BackgroundJobStatus.Failed);
                var typeJobsWithDuration = typeJobs.Where(j => j.ProcessingDurationMs.HasValue).ToList();

                return new JobTypeMetrics {
                    JobType = g.Key,
                    TotalCount = typeJobs.Count,
                    SuccessCount = typeCompleted,
                    FailureCount = typeFailed,
                    AverageProcessingDurationMs = typeJobsWithDuration.Any()
                        ? typeJobsWithDuration.Average(j => j.ProcessingDurationMs!.Value)
                        : 0.0,
                    FailureRate = typeJobs.Count > 0
                        ? (double)typeFailed / typeJobs.Count
                        : 0.0
                };
            })
            .ToDictionary(m => m.JobType);

        var metrics = new JobMetrics {
            QueueDepth = queuedJobs.Count,
            ProcessingCount = processingJobs.Count,
            CompletedCount = completedJobs.Count,
            FailedCount = failedJobs.Count,
            CancelledCount = cancelledJobs.Count,
            DeadLetterCount = deadLetterJobs.Count,
            AverageProcessingDurationMs = avgProcessingDuration,
            AverageQueueWaitTimeMs = avgQueueWaitTime,
            FailureRate = failureRate,
            TotalJobs = totalJobs,
            MetricsByType = metricsByType
        };

        return Task.FromResult(metrics);
    }
}
