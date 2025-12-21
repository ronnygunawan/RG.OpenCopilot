using Octokit;

namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Authentication;

/// <summary>
/// Factory for creating GitHub clients with installation-specific credentials
/// </summary>
public interface IGitHubClientFactory {
    /// <summary>
    /// Get a GitHub client authenticated for the specified installation
    /// </summary>
    Task<IGitHubClient> GetClientForInstallationAsync(long installationId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a GitHub client with JWT authentication (for app-level operations)
    /// </summary>
    IGitHubClient GetClientWithJwt();
}

public sealed class GitHubClientFactory : IGitHubClientFactory {
    private readonly IGitHubAppTokenProvider _tokenProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GitHubClientFactory> _logger;
    private readonly IJwtTokenGenerator _jwtGenerator;

    public GitHubClientFactory(
        IGitHubAppTokenProvider tokenProvider,
        IConfiguration configuration,
        ILogger<GitHubClientFactory> logger)
        : this(tokenProvider, configuration, new JwtTokenGenerator(TimeProvider.System), logger) {
    }

    public GitHubClientFactory(
        IGitHubAppTokenProvider tokenProvider,
        IConfiguration configuration,
        IJwtTokenGenerator jwtGenerator,
        ILogger<GitHubClientFactory> logger) {
        _tokenProvider = tokenProvider;
        _configuration = configuration;
        _jwtGenerator = jwtGenerator;
        _logger = logger;
    }

    public async Task<IGitHubClient> GetClientForInstallationAsync(long installationId, CancellationToken cancellationToken = default) {
        var client = new GitHubClient(new ProductHeaderValue("RG-OpenCopilot"));
        
        try {
            // Try to get installation token
            var token = await _tokenProvider.GetInstallationTokenAsync(installationId, cancellationToken);
            client.Credentials = new Credentials(token);
            _logger.LogDebug("Created GitHub client with installation token for installation {InstallationId}", installationId);
            return client;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("GitHub App credentials")) {
            // Fall back to PAT if GitHub App credentials not configured
            var pat = _configuration["GitHub:Token"];
            if (!string.IsNullOrEmpty(pat)) {
                client.Credentials = new Credentials(pat);
                _logger.LogWarning("GitHub App credentials not configured, using personal access token as fallback for installation {InstallationId}", installationId);
                return client;
            }
            
            _logger.LogError("No authentication method available for installation {InstallationId}", installationId);
            throw new InvalidOperationException("GitHub App credentials or personal access token must be configured");
        }
    }

    public IGitHubClient GetClientWithJwt() {
        var appId = _configuration["GitHub:AppId"];
        var privateKey = _configuration["GitHub:AppPrivateKey"];
        
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(privateKey)) {
            throw new InvalidOperationException("GitHub App credentials (AppId and PrivateKey) must be configured to use JWT authentication");
        }
        
        var client = new GitHubClient(new ProductHeaderValue("RG-OpenCopilot"));
        var jwt = _jwtGenerator.GenerateJwtToken(appId, privateKey);
        client.Credentials = new Credentials(token: jwt, authenticationType: AuthenticationType.Bearer);
        
        _logger.LogDebug("Created GitHub client with JWT authentication");
        return client;
    }
}
