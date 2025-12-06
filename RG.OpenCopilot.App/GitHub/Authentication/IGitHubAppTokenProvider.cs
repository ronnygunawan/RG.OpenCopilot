namespace RG.OpenCopilot.App.GitHub.Authentication;

public interface IGitHubAppTokenProvider {
    Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default);
}
