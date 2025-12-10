using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;
using System.Collections.Concurrent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// In-memory implementation of audit log store for development and testing
/// </summary>
internal sealed class InMemoryAuditLogStore : IAuditLogStore {
    private readonly ConcurrentBag<AuditLog> _logs = [];

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
        
        // Enforce maximum limit
        var effectiveLimit = Math.Min(limit, 1000);
        
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
            .Take(effectiveLimit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AuditLog>>(results);
    }

    public Task<int> DeleteOlderThanAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default) {
        var cutoffDate = DateTime.UtcNow - retentionPeriod;
        
        var logsToKeep = _logs.Where(log => log.Timestamp >= cutoffDate).ToList();
        var deletedCount = _logs.Count - logsToKeep.Count;
        
        _logs.Clear();
        foreach (var log in logsToKeep) {
            _logs.Add(log);
        }

        return Task.FromResult(deletedCount);
    }
}
