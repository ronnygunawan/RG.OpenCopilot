namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Authentication;

public interface IGitHubAppTokenProvider {
    Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default);
    Task<AppInstallationPermissions> GetInstallationPermissionsAsync(long installationId, CancellationToken cancellationToken = default);
    void ClearCache();
}
