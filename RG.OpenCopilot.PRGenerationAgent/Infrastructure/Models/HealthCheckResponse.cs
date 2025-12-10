namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Overall health status
/// </summary>
public enum HealthStatus {
    /// <summary>
    /// All systems healthy
    /// </summary>
    Healthy,
    
    /// <summary>
    /// System is degraded but operational
    /// </summary>
    Degraded,
    
    /// <summary>
    /// System is unhealthy
    /// </summary>
    Unhealthy
}

/// <summary>
/// Health check response with component details
/// </summary>
public sealed class HealthCheckResponse {
    /// <summary>
    /// Overall health status
    /// </summary>
    public HealthStatus Status { get; init; }
    
    /// <summary>
    /// Timestamp of the health check
    /// </summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// Health status of individual components
    /// </summary>
    public Dictionary<string, ComponentHealth> Components { get; init; } = [];
    
    /// <summary>
    /// Application metrics
    /// </summary>
    public Dictionary<string, object> Metrics { get; init; } = [];
}

/// <summary>
/// Health status of an individual component
/// </summary>
public sealed class ComponentHealth {
    /// <summary>
    /// Component health status
    /// </summary>
    public HealthStatus Status { get; init; }
    
    /// <summary>
    /// Health check description
    /// </summary>
    public string Description { get; init; } = "";
    
    /// <summary>
    /// Additional component details
    /// </summary>
    public Dictionary<string, object> Details { get; init; } = [];
}
