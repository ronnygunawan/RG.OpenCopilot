using System.Collections.Concurrent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// In-memory implementation of job deduplication service
/// </summary>
internal sealed class InMemoryJobDeduplicationService : IJobDeduplicationService {
    private readonly ConcurrentDictionary<string, string> _idempotencyKeyToJobId = new();
    private readonly ConcurrentDictionary<string, string> _jobIdToIdempotencyKey = new();

    /// <inheritdoc />
    public Task<string?> GetInFlightJobAsync(string idempotencyKey, CancellationToken cancellationToken = default) {
        _idempotencyKeyToJobId.TryGetValue(idempotencyKey, out var jobId);
        return Task.FromResult(jobId);
    }

    /// <inheritdoc />
    public Task RegisterJobAsync(string jobId, string idempotencyKey, CancellationToken cancellationToken = default) {
        _idempotencyKeyToJobId[idempotencyKey] = jobId;
        _jobIdToIdempotencyKey[jobId] = idempotencyKey;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UnregisterJobAsync(string jobId, CancellationToken cancellationToken = default) {
        if (_jobIdToIdempotencyKey.TryRemove(jobId, out var idempotencyKey)) {
            _idempotencyKeyToJobId.TryRemove(idempotencyKey, out _);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAllAsync(CancellationToken cancellationToken = default) {
        _idempotencyKeyToJobId.Clear();
        _jobIdToIdempotencyKey.Clear();
        return Task.CompletedTask;
    }
}
