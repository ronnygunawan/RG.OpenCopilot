namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Interface for audit log persistence
/// </summary>
public interface IAuditLogStore {
    /// <summary>
    /// Store an audit log entry
    /// </summary>
    /// <param name="auditLog">The audit log entry to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task StoreAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Query audit logs with optional filters
    /// </summary>
    /// <param name="eventType">Filter by event type</param>
    /// <param name="correlationId">Filter by correlation ID</param>
    /// <param name="startDate">Filter by start date (inclusive)</param>
    /// <param name="endDate">Filter by end date (inclusive)</param>
    /// <param name="limit">Maximum number of results to return (default: 100, max: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of audit log entries matching the filters</returns>
    Task<IReadOnlyList<AuditLog>> QueryAsync(
        AuditEventType? eventType = null,
        string? correlationId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete audit logs older than the specified retention period
    /// </summary>
    /// <param name="retentionPeriod">Time span to retain logs (e.g., 90 days)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of deleted audit log entries</returns>
    Task<int> DeleteOlderThanAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default);
}
