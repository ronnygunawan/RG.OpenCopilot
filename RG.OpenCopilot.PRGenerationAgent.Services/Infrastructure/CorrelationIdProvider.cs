namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Thread-safe correlation ID provider using AsyncLocal for context propagation
/// </summary>
internal sealed class CorrelationIdProvider : ICorrelationIdProvider {
    private static readonly AsyncLocal<string?> _correlationId = new();

    public string? GetCorrelationId() {
        return _correlationId.Value;
    }

    public void SetCorrelationId(string correlationId) {
        if (string.IsNullOrWhiteSpace(correlationId)) {
            throw new ArgumentException("Correlation ID cannot be null or whitespace", nameof(correlationId));
        }
        
        _correlationId.Value = correlationId;
    }

    public string GenerateCorrelationId() {
        var correlationId = Guid.NewGuid().ToString();
        SetCorrelationId(correlationId);
        return correlationId;
    }
}
