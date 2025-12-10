namespace RG.OpenCopilot.Tests;

/// <summary>
/// Test implementation of IAuditLogStore that stores logs in memory
/// </summary>
internal sealed class TestAuditLogStore : IAuditLogStore {
    private readonly List<AuditLog> _logs = [];

    public IReadOnlyList<AuditLog> StoredLogs => _logs.AsReadOnly();

    public Task StoreAsync(AuditLog auditLog, CancellationToken cancellationToken = default) {
        _logs.Add(auditLog);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLog>> QueryAsync(
        AuditEventType? eventType = null,
        string? correlationId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default) {
        
        var query = _logs.AsEnumerable();

        if (eventType.HasValue) {
            query = query.Where(log => log.EventType == eventType.Value);
        }

        if (!string.IsNullOrEmpty(correlationId)) {
            query = query.Where(log => log.CorrelationId == correlationId);
        }

        if (startDate.HasValue) {
            query = query.Where(log => log.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue) {
            query = query.Where(log => log.Timestamp <= endDate.Value);
        }

        var results = query
            .OrderByDescending(log => log.Timestamp)
            .Take(Math.Min(limit, 1000))
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditLog>>(results);
    }

    public Task<int> DeleteOlderThanAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default) {
        var cutoffDate = DateTime.UtcNow - retentionPeriod;
        var deletedCount = _logs.RemoveAll(log => log.Timestamp < cutoffDate);
        return Task.FromResult(deletedCount);
    }

    public void Clear() {
        _logs.Clear();
    }
}
