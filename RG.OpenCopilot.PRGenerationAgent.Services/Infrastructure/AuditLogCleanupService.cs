using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Service for cleaning up old audit logs based on retention policies
/// </summary>
internal sealed class AuditLogCleanupService : IAuditLogCleanupService {
    private readonly IAuditLogStore _auditLogStore;
    private readonly ILogger<AuditLogCleanupService> _logger;
    private readonly TimeSpan _retentionPeriod;

    public AuditLogCleanupService(
        IAuditLogStore auditLogStore,
        IConfiguration configuration,
        ILogger<AuditLogCleanupService> logger) {
        _auditLogStore = auditLogStore;
        _logger = logger;
        
        // Default retention period is 90 days
        var retentionDays = configuration.GetValue("AuditLog:RetentionDays", defaultValue: 90);
        _retentionPeriod = TimeSpan.FromDays(retentionDays);
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default) {
        try {
            _logger.LogInformation(
                "Starting audit log cleanup with retention period: {RetentionDays} days",
                _retentionPeriod.TotalDays);

            var deletedCount = await _auditLogStore.DeleteOlderThanAsync(
                retentionPeriod: _retentionPeriod,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Audit log cleanup completed. Deleted {DeletedCount} old audit logs",
                deletedCount);

            return deletedCount;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to cleanup audit logs");
            throw;
        }
    }
}
