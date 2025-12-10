namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

/// <summary>
/// Provides centralized management of correlation IDs for tracking related operations
/// </summary>
public interface ICorrelationIdProvider {
    /// <summary>
    /// Gets the current correlation ID for the execution context
    /// </summary>
    /// <returns>The current correlation ID, or null if none is set</returns>
    string? GetCorrelationId();
    
    /// <summary>
    /// Sets the correlation ID for the current execution context
    /// </summary>
    /// <param name="correlationId">The correlation ID to set</param>
    void SetCorrelationId(string correlationId);
    
    /// <summary>
    /// Generates a new correlation ID and sets it for the current execution context
    /// </summary>
    /// <returns>The generated correlation ID</returns>
    string GenerateCorrelationId();
}
