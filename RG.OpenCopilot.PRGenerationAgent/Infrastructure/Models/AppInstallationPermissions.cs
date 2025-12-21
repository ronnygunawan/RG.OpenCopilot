namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;

/// <summary>
/// Represents GitHub App installation permissions
/// </summary>
public sealed class AppInstallationPermissions {
    public bool HasContents { get; init; }
    public bool HasIssues { get; init; }
    public bool HasPullRequests { get; init; }
    public bool HasWorkflows { get; init; }
    
    /// <summary>
    /// Check if installation has all required permissions
    /// </summary>
    public bool HasRequiredPermissions() {
        return HasContents && HasIssues && HasPullRequests;
    }
}
