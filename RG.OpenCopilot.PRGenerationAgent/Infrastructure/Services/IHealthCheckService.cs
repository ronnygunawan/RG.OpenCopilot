namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Interface for health check service
/// </summary>
public interface IHealthCheckService {
    /// <summary>
    /// Perform a comprehensive health check
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check response with component status</returns>
    Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken = default);
}
