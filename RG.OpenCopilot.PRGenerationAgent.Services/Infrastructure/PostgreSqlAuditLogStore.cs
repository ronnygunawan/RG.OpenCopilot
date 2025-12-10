using Microsoft.EntityFrameworkCore;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// PostgreSQL implementation of audit log store
/// </summary>
internal sealed class PostgreSqlAuditLogStore : IAuditLogStore {
    private readonly AgentTaskDbContext _context;

    public PostgreSqlAuditLogStore(AgentTaskDbContext context) {
        _context = context;
    }

    public async Task StoreAsync(AuditLog auditLog, CancellationToken cancellationToken = default) {
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> QueryAsync(
        AuditEventType? eventType = null,
        string? correlationId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default) {
        
        // Enforce maximum limit
        var effectiveLimit = Math.Min(limit, 1000);
        
        var query = _context.AuditLogs.AsNoTracking();

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

        return await query
            .OrderByDescending(log => log.Timestamp)
            .Take(effectiveLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default) {
        var cutoffDate = DateTime.UtcNow - retentionPeriod;
        
        var logsToDelete = await _context.AuditLogs
            .Where(log => log.Timestamp < cutoffDate)
            .ToListAsync(cancellationToken);

        _context.AuditLogs.RemoveRange(logsToDelete);
        await _context.SaveChangesAsync(cancellationToken);

        return logsToDelete.Count;
    }
}
