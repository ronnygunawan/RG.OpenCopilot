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
}
