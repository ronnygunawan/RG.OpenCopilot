using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Octokit;

namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Authentication;

/// <summary>
/// Provides GitHub App installation tokens for authentication
/// </summary>
public sealed class GitHubAppTokenProvider : IGitHubAppTokenProvider {
    private readonly IGitHubClient _client;
    private readonly IJwtTokenGenerator _jwtGenerator;
    private readonly string? _appId;
    private readonly string? _privateKey;
    private readonly ILogger<GitHubAppTokenProvider> _logger;

    public GitHubAppTokenProvider(
        IGitHubClient client,
        IConfiguration configuration,
        ILogger<GitHubAppTokenProvider> logger)
        : this(client, new JwtTokenGenerator(), configuration, logger) {
    }

    public GitHubAppTokenProvider(
        IGitHubClient client,
        IJwtTokenGenerator jwtGenerator,
        IConfiguration configuration,
        ILogger<GitHubAppTokenProvider> logger) {
        _client = client;
        _jwtGenerator = jwtGenerator;
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
            var jwt = _jwtGenerator.GenerateJwtToken(_appId, _privateKey);

            // Get installation token - we need to temporarily set credentials
            // Create a new client with the JWT credentials
            if (_client is GitHubClient concreteClient) {
                concreteClient.Credentials = new Credentials(token: jwt, authenticationType: AuthenticationType.Bearer);
                var response = await concreteClient.GitHubApps.CreateInstallationToken(installationId);
                _logger.LogInformation("Generated installation token for installation {InstallationId}", installationId);
                return response.Token;
            }

            throw new InvalidOperationException("Client must be a GitHubClient to authenticate with JWT");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get installation token for installation {InstallationId}", installationId);
            throw;
        }
    }
}

public interface IJwtTokenGenerator {
    string GenerateJwtToken(string appId, string privateKey);
}

public sealed class JwtTokenGenerator : IJwtTokenGenerator {
    public string GenerateJwtToken(string appId, string privateKey) {
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
