using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Octokit;

namespace RG.OpenCopilot.App;

/// <summary>
/// Provides GitHub App installation tokens for authentication
/// </summary>
public sealed class GitHubAppTokenProvider : IGitHubAppTokenProvider {
    private readonly GitHubClient _client;
    private readonly string? _appId;
    private readonly string? _privateKey;
    private readonly ILogger<GitHubAppTokenProvider> _logger;

    public GitHubAppTokenProvider(
        IGitHubClient client,
        IConfiguration configuration,
        ILogger<GitHubAppTokenProvider> logger) {
        // Note: We cast to GitHubClient to set credentials, which is needed for GitHub App authentication.
        // This is a limitation of the Octokit API design. In production, consider creating a custom
        // GitHubClient factory that handles authentication internally.
        _client = (GitHubClient)client;
        _appId = configuration["GitHub:AppId"];
        _privateKey = configuration["GitHub:AppPrivateKey"];
        _logger = logger;
    }

    public async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default) {
        // For POC/testing, fall back to personal access token if GitHub App credentials are not configured
        if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_privateKey)) {
            _logger.LogWarning("GitHub App credentials not configured, using fallback authentication");

            // Return empty string - the caller should handle this
            // In a real implementation, this would throw an exception
            return "";
        }

        try {
            // Generate JWT for GitHub App authentication
            var jwt = GenerateJwtToken(_appId, _privateKey);

            // Authenticate as GitHub App
            _client.Credentials = new Credentials(token: jwt, authenticationType: AuthenticationType.Bearer);

            // Get installation token
            var response = await _client.GitHubApps.CreateInstallationToken(installationId);

            _logger.LogInformation("Generated installation token for installation {InstallationId}", installationId);

            return response.Token;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get installation token for installation {InstallationId}", installationId);
            throw;
        }
    }

    private string GenerateJwtToken(string appId, string privateKey) {
        // GitHub requires the JWT to be signed with RS256
        // The token should expire within 10 minutes

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);

        var signingCredentials = new SigningCredentials(
            key: new RsaSecurityKey(rsa),
            algorithm: SecurityAlgorithms.RsaSha256);

        var now = DateTimeOffset.UtcNow;
        var tokenDescriptor = new SecurityTokenDescriptor {
            Issuer = appId,
            IssuedAt = now.DateTime,
            Expires = now.AddMinutes(9).DateTime, // GitHub recommends expiring within 10 minutes
            SigningCredentials = signingCredentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
