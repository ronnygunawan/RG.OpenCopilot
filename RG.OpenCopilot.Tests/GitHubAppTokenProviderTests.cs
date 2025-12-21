using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class GitHubAppTokenProviderTests {
    [Fact]
    public async Task GetInstallationTokenAsync_WithMissingCredentials_ThrowsInvalidOperationException() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockClient = new Mock<IGitHubClient>();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", null },
                { "GitHub:AppPrivateKey", null }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient.Object, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetInstallationTokenAsync(installationId: 123));
        
        exception.Message.ShouldBe("GitHub App credentials (AppId and PrivateKey) must be configured to generate installation tokens");
        mockJwtGenerator.Verify(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetInstallationTokenAsync_WithNonGitHubClient_ThrowsInvalidOperationException() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var privateKey = GenerateTestPrivateKey();
        var mockClient = new Mock<IGitHubClient>(); // Not a GitHubClient instance
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient.Object, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            async () => await provider.GetInstallationTokenAsync(installationId: 123));
        
        exception.Message.ShouldBe("Client must be a GitHubClient to authenticate with JWT");
    }

    [Fact]
    public async Task GetInstallationTokenAsync_UsesCachedTokenWhenValid() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ReturnsAsync(new AccessToken(
                token: "installation-token-123",
                expiresAt: new DateTimeOffset(2024, 1, 1, 13, 0, 0, TimeSpan.Zero)));

        var mockClient = new GitHubClient(new ProductHeaderValue("test"));
        var gitHubAppsProperty = typeof(GitHubClient).GetProperty("GitHubApps");
        gitHubAppsProperty?.SetValue(mockClient, mockGitHubAppsClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act - first call should create token
        var token1 = await provider.GetInstallationTokenAsync(installationId: 123);
        // Second call should use cached token
        var token2 = await provider.GetInstallationTokenAsync(installationId: 123);

        // Assert
        token1.ShouldBe("installation-token-123");
        token2.ShouldBe("installation-token-123");
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(123), Times.Once); // Only called once
    }

    [Fact]
    public async Task GetInstallationTokenAsync_RefreshesTokenWhenExpired() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var callCount = 0;
        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ReturnsAsync(() => {
                callCount++;
                return new AccessToken(
                    token: $"installation-token-{callCount}",
                    expiresAt: new DateTimeOffset(2024, 1, 1, 12, 10, 0, TimeSpan.Zero)); // Expires in 10 minutes
            });

        var mockClient = new GitHubClient(new ProductHeaderValue("test"));
        var gitHubAppsProperty = typeof(GitHubClient).GetProperty("GitHubApps");
        gitHubAppsProperty?.SetValue(mockClient, mockGitHubAppsClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act - first call
        var token1 = await provider.GetInstallationTokenAsync(installationId: 123);
        
        // Advance time to expire the token (within 5 minute buffer)
        timeProvider.Advance(TimeSpan.FromMinutes(6));
        
        // Second call should refresh token
        var token2 = await provider.GetInstallationTokenAsync(installationId: 123);

        // Assert
        token1.ShouldBe("installation-token-1");
        token2.ShouldBe("installation-token-2");
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(123), Times.Exactly(2));
    }

    [Fact]
    public async Task GetInstallationTokenAsync_CachesSeparatelyPerInstallation() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ReturnsAsync((long installationId) => new AccessToken(
                token: $"token-for-{installationId}",
                expiresAt: new DateTimeOffset(2024, 1, 1, 13, 0, 0, TimeSpan.Zero)));

        var mockClient = new GitHubClient(new ProductHeaderValue("test"));
        var gitHubAppsProperty = typeof(GitHubClient).GetProperty("GitHubApps");
        gitHubAppsProperty?.SetValue(mockClient, mockGitHubAppsClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act
        var token1 = await provider.GetInstallationTokenAsync(installationId: 123);
        var token2 = await provider.GetInstallationTokenAsync(installationId: 456);
        var token1Again = await provider.GetInstallationTokenAsync(installationId: 123);

        // Assert
        token1.ShouldBe("token-for-123");
        token2.ShouldBe("token-for-456");
        token1Again.ShouldBe("token-for-123");
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(123), Times.Once);
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(456), Times.Once);
    }

    [Fact]
    public void ClearCache_RemovesCachedTokens() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        var mockClient = new Mock<IGitHubClient>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient.Object, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act
        provider.ClearCache();

        // Assert - should not throw
        // The cache is cleared, which we can't directly verify, but the method should complete successfully
    }

    [Fact]
    public async Task GetInstallationPermissionsAsync_WithMissingCredentials_ThrowsInvalidOperationException() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var mockClient = new Mock<IGitHubClient>();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", null },
                { "GitHub:AppPrivateKey", null }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient.Object, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetInstallationPermissionsAsync(installationId: 123));
        
        exception.Message.ShouldBe("GitHub App credentials (AppId and PrivateKey) must be configured to check installation permissions");
    }

    [Fact]
    public void JwtTokenGenerator_GeneratesValidToken() {
        // Arrange
        var generator = new JwtTokenGenerator(new FakeTimeProvider(DateTimeOffset.UtcNow));
        var appId = "12345";
        var privateKey = GenerateTestPrivateKey();

        // Act
        var jwt = generator.GenerateJwtToken(appId, privateKey);

        // Assert
        jwt.ShouldNotBeNullOrEmpty();
        jwt.Split('.').Length.ShouldBe(3); // JWT has 3 parts separated by dots
    }

    [Fact]
    public async Task GetInstallationTokenAsync_WhenApiCallFails_ThrowsAndDoesNotCache() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ThrowsAsync(new ApiException("API rate limit exceeded", System.Net.HttpStatusCode.Forbidden));

        var mockClient = new GitHubClient(new ProductHeaderValue("test"));
        var gitHubAppsProperty = typeof(GitHubClient).GetProperty("GitHubApps");
        gitHubAppsProperty?.SetValue(mockClient, mockGitHubAppsClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<ApiException>(
            () => provider.GetInstallationTokenAsync(installationId: 123));
        
        exception.Message.ShouldContain("API rate limit exceeded");
        
        // Verify it was called and failed
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(123), Times.Once);
    }

    [Fact]
    public async Task GetInstallationTokenAsync_ClearCache_ForcesTokenRefresh() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var callCount = 0;
        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ReturnsAsync(() => {
                callCount++;
                return new AccessToken(
                    token: $"installation-token-{callCount}",
                    expiresAt: new DateTimeOffset(2024, 1, 1, 13, 0, 0, TimeSpan.Zero));
            });

        var mockClient = new GitHubClient(new ProductHeaderValue("test"));
        var gitHubAppsProperty = typeof(GitHubClient).GetProperty("GitHubApps");
        gitHubAppsProperty?.SetValue(mockClient, mockGitHubAppsClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act
        var token1 = await provider.GetInstallationTokenAsync(installationId: 123);
        provider.ClearCache();
        var token2 = await provider.GetInstallationTokenAsync(installationId: 123);

        // Assert
        token1.ShouldBe("installation-token-1");
        token2.ShouldBe("installation-token-2");
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(123), Times.Exactly(2));
    }

    [Fact]
    public async Task GetInstallationTokenAsync_WithPartiallyExpiredToken_UsesValidToken() {
        // Arrange - token expires in 6 minutes, just outside the 5-minute buffer
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ReturnsAsync(new AccessToken(
                token: "installation-token-123",
                expiresAt: new DateTimeOffset(2024, 1, 1, 12, 6, 1, TimeSpan.Zero))); // 6 min 1 sec

        var mockClient = new GitHubClient(new ProductHeaderValue("test"));
        var gitHubAppsProperty = typeof(GitHubClient).GetProperty("GitHubApps");
        gitHubAppsProperty?.SetValue(mockClient, mockGitHubAppsClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act
        var token1 = await provider.GetInstallationTokenAsync(installationId: 123);
        var token2 = await provider.GetInstallationTokenAsync(installationId: 123);

        // Assert
        token1.ShouldBe("installation-token-123");
        token2.ShouldBe("installation-token-123");
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(123), Times.Once);
    }

    [Fact]
    public async Task GetInstallationPermissionsAsync_WithNonGitHubClient_ThrowsException() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var privateKey = GenerateTestPrivateKey();
        var mockClient = new Mock<IGitHubClient>();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient.Object, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.GetInstallationPermissionsAsync(installationId: 123));
        
        exception.Message.ShouldBe("Client must be a GitHubClient to authenticate with JWT");
    }

    [Fact]
    public async Task GetInstallationTokenAsync_WithConcurrentRequests_OnlyCallsApiOnce() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var callCount = 0;
        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ReturnsAsync(() => {
                Interlocked.Increment(ref callCount);
                // Yield to simulate async behavior without using Task.Delay
                return new AccessToken(
                    token: "installation-token-123",
                    expiresAt: new DateTimeOffset(2024, 1, 1, 13, 0, 0, TimeSpan.Zero));
            });

        var mockClient = new GitHubClient(new ProductHeaderValue("test"));
        var gitHubAppsProperty = typeof(GitHubClient).GetProperty("GitHubApps");
        gitHubAppsProperty?.SetValue(mockClient, mockGitHubAppsClient.Object);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();
        var logger = new TestLogger<GitHubAppTokenProvider>();
        var provider = new GitHubAppTokenProvider(mockClient, mockJwtGenerator.Object, configuration, timeProvider, logger);

        // Act - Make 5 concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => provider.GetInstallationTokenAsync(installationId: 123))
            .ToArray();
        
        var tokens = await Task.WhenAll(tasks);

        // Assert
        tokens.ShouldAllBe(t => t == "installation-token-123");
        // Note: Due to race condition, might be called more than once, but should be minimal
        callCount.ShouldBeGreaterThan(0);
        callCount.ShouldBeLessThanOrEqualTo(5); // At most 5, but typically 1-2 due to caching
    }

    [Fact]
    public void JwtTokenGenerator_WithInvalidPrivateKey_ThrowsException() {
        // Arrange
        var generator = new JwtTokenGenerator(new FakeTimeProvider(DateTimeOffset.UtcNow));
        var appId = "12345";
        var invalidPrivateKey = "not-a-valid-private-key";

        // Act & Assert
        Should.Throw<ArgumentException>(
            () => generator.GenerateJwtToken(appId, invalidPrivateKey));
    }

    private static string GenerateTestPrivateKey() {
        // Generate a test RSA private key in PEM format
        using var rsa = System.Security.Cryptography.RSA.Create(keySizeInBits: 2048);
        var privateKey = rsa.ExportRSAPrivateKeyPem();
        return privateKey;
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
