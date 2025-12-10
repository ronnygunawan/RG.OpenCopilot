namespace RG.OpenCopilot.Tests;

/// <summary>
/// Test implementation of ICorrelationIdProvider with settable correlation ID
/// </summary>
internal sealed class TestCorrelationIdProvider : ICorrelationIdProvider {
    private string? _correlationId;

    public string? GetCorrelationId() => _correlationId;

    public void SetCorrelationId(string correlationId) {
        _correlationId = correlationId;
    }

    public string GenerateCorrelationId() {
        _correlationId = Guid.NewGuid().ToString();
        return _correlationId;
    }
}
