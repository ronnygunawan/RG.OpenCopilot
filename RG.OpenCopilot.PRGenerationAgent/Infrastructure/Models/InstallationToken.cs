namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Represents a cached GitHub App installation token
/// </summary>
public sealed class InstallationToken {
    public long InstallationId { get; init; }
    public string Token { get; init; } = "";
    public DateTime ExpiresAt { get; init; }
    
    /// <summary>
    /// Check if the token is still valid (with 5 minute buffer)
    /// </summary>
    public bool IsValid(TimeProvider timeProvider) {
        var now = timeProvider.GetUtcNow().DateTime;
        return now.AddMinutes(5) < ExpiresAt;
    }
}
