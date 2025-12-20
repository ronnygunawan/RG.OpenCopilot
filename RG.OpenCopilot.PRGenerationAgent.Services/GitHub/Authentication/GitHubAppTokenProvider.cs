using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Octokit;

namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Authentication;

/// <summary>
/// Provides GitHub App installation tokens for authentication with caching
/// </summary>
public sealed class GitHubAppTokenProvider : IGitHubAppTokenProvider {
    private readonly IGitHubClient _client;
    private readonly IJwtTokenGenerator _jwtGenerator;
    private readonly string? _appId;
    private readonly string? _privateKey;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GitHubAppTokenProvider> _logger;
    private readonly ConcurrentDictionary<long, InstallationToken> _tokenCache = new();

    public GitHubAppTokenProvider(
        IGitHubClient client,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<GitHubAppTokenProvider> logger)
        : this(client, new JwtTokenGenerator(timeProvider), configuration, timeProvider, logger) {
    }

    public GitHubAppTokenProvider(
        IGitHubClient client,
        IJwtTokenGenerator jwtGenerator,
        IConfiguration configuration,
        TimeProvider timeProvider,
        ILogger<GitHubAppTokenProvider> logger) {
        _client = client;
        _jwtGenerator = jwtGenerator;
        _appId = configuration["GitHub:AppId"];
        _privateKey = configuration["GitHub:AppPrivateKey"];
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default) {
        // Check cache first
        if (_tokenCache.TryGetValue(installationId, out var cachedToken) && cachedToken.IsValid(_timeProvider)) {
            _logger.LogDebug("Using cached installation token for installation {InstallationId}", installationId);
            return cachedToken.Token;
        }

        if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_privateKey)) {
            _logger.LogError("GitHub App credentials not configured. AppId and PrivateKey are required.");
            throw new InvalidOperationException("GitHub App credentials (AppId and PrivateKey) must be configured to generate installation tokens");
        }

        try {
            // Generate JWT for GitHub App authentication
            var jwt = _jwtGenerator.GenerateJwtToken(_appId, _privateKey);

            // Get installation token - we need to temporarily set credentials
            // Create a new client with the JWT credentials
            if (_client is GitHubClient concreteClient) {
                concreteClient.Credentials = new Credentials(token: jwt, authenticationType: AuthenticationType.Bearer);
                var response = await concreteClient.GitHubApps.CreateInstallationToken(installationId);
                
                // Cache the token
                var installationToken = new InstallationToken {
                    InstallationId = installationId,
                    Token = response.Token,
                    ExpiresAt = response.ExpiresAt.UtcDateTime
                };
                _tokenCache[installationId] = installationToken;
                
                _logger.LogInformation("Generated and cached installation token for installation {InstallationId}, expires at {ExpiresAt}", 
                    installationId, response.ExpiresAt);
                return response.Token;
            }

            throw new InvalidOperationException("Client must be a GitHubClient to authenticate with JWT");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get installation token for installation {InstallationId}", installationId);
            throw;
        }
    }

    public async Task<AppInstallationPermissions> GetInstallationPermissionsAsync(long installationId, CancellationToken cancellationToken = default) {
        if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_privateKey)) {
            _logger.LogError("GitHub App credentials not configured. AppId and PrivateKey are required.");
            throw new InvalidOperationException("GitHub App credentials (AppId and PrivateKey) must be configured to check installation permissions");
        }

        try {
            // Generate JWT for GitHub App authentication
            var jwt = _jwtGenerator.GenerateJwtToken(_appId, _privateKey);

            if (_client is GitHubClient concreteClient) {
                concreteClient.Credentials = new Credentials(token: jwt, authenticationType: AuthenticationType.Bearer);
                var installation = await concreteClient.GitHubApps.GetInstallation(installationId);
                
                // Check if installation has write or admin permissions for required resources
                var permissions = new AppInstallationPermissions {
                    HasContents = HasWritePermission(installation.Permissions.Contents?.StringValue),
                    HasIssues = HasWritePermission(installation.Permissions.Issues?.StringValue),
                    HasPullRequests = HasWritePermission(installation.Permissions.PullRequests?.StringValue),
                    HasWorkflows = HasWritePermission(installation.Permissions.Workflows?.StringValue)
                };
                
                _logger.LogInformation("Retrieved permissions for installation {InstallationId}: Contents={Contents}, Issues={Issues}, PullRequests={PullRequests}, Workflows={Workflows}",
                    installationId, permissions.HasContents, permissions.HasIssues, permissions.HasPullRequests, permissions.HasWorkflows);
                
                return permissions;
            }

            throw new InvalidOperationException("Client must be a GitHubClient to authenticate with JWT");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to get installation permissions for installation {InstallationId}", installationId);
            throw;
        }
    }

    private static bool HasWritePermission(string? permission) {
        if (string.IsNullOrEmpty(permission)) {
            return false;
        }
        
        return string.Equals(permission, "write", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(permission, "admin", StringComparison.OrdinalIgnoreCase);
    }

    public void ClearCache() {
        _logger.LogInformation("Clearing installation token cache");
        _tokenCache.Clear();
    }
}

public interface IJwtTokenGenerator {
    string GenerateJwtToken(string appId, string privateKey);
}

public sealed class JwtTokenGenerator : IJwtTokenGenerator {
    private readonly TimeProvider _timeProvider;

    public JwtTokenGenerator(TimeProvider timeProvider) {
        _timeProvider = timeProvider;
    }

    public string GenerateJwtToken(string appId, string privateKey) {
        // GitHub requires the JWT to be signed with RS256
        // The token should expire within 10 minutes

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey);

        var signingCredentials = new SigningCredentials(
            key: new RsaSecurityKey(rsa),
            algorithm: SecurityAlgorithms.RsaSha256);

        var now = _timeProvider.GetUtcNow();
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
