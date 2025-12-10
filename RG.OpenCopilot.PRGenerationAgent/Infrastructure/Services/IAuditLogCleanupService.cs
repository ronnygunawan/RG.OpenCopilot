namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Service for cleaning up old audit logs based on retention policies
/// </summary>
public interface IAuditLogCleanupService {
    /// <summary>
    /// Execute cleanup based on configured retention policy
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of deleted audit log entries</returns>
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}
