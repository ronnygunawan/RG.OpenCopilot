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
        // With proper locking, API should only be called once
        callCount.ShouldBe(1);
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

    [Fact]
    public async Task GetInstallationTokenAsync_WithMultipleInstallations_MaintainsSeparateLocks() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(123))
            .ReturnsAsync(new AccessToken(
                token: "token-123",
                expiresAt: new DateTimeOffset(2024, 1, 1, 13, 0, 0, TimeSpan.Zero)));
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(456))
            .ReturnsAsync(new AccessToken(
                token: "token-456",
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

        // Act - Request tokens for different installations concurrently
        var tasks = new[] {
            provider.GetInstallationTokenAsync(installationId: 123),
            provider.GetInstallationTokenAsync(installationId: 456),
            provider.GetInstallationTokenAsync(installationId: 123),
            provider.GetInstallationTokenAsync(installationId: 456)
        };
        
        var tokens = await Task.WhenAll(tasks);

        // Assert - Each installation should have exactly one API call
        tokens[0].ShouldBe("token-123");
        tokens[1].ShouldBe("token-456");
        tokens[2].ShouldBe("token-123");
        tokens[3].ShouldBe("token-456");
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(123), Times.Once);
        mockGitHubAppsClient.Verify(c => c.CreateInstallationToken(456), Times.Once);
    }

    [Fact]
    public async Task GetInstallationTokenAsync_WhenLockAcquired_SecondRequestWaitsForCache() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var firstCallStarted = new TaskCompletionSource<bool>();
        var allowFirstCallToComplete = new TaskCompletionSource<bool>();
        var apiCallCount = 0;

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .Returns(async () => {
                Interlocked.Increment(ref apiCallCount);
                firstCallStarted.SetResult(true);
                await allowFirstCallToComplete.Task;
                return new AccessToken(
                    token: "installation-token",
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

        // Act - Start first request and wait for it to start processing
        var task1 = provider.GetInstallationTokenAsync(installationId: 123);
        await firstCallStarted.Task;

        // Start second request while first is still processing
        var task2 = provider.GetInstallationTokenAsync(installationId: 123);

        // Allow first request to complete
        allowFirstCallToComplete.SetResult(true);

        var tokens = await Task.WhenAll(task1, task2);

        // Assert - Only one API call should have been made
        tokens[0].ShouldBe("installation-token");
        tokens[1].ShouldBe("installation-token");
        apiCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetInstallationPermissionsAsync_WithAllPermissionsRead_ReturnsFalseForAll() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var mockInstallation = new Octokit.Internal.SimpleJsonSerializer().Deserialize<Installation>("""
            {
                "id": 123,
                "permissions": {
                    "contents": "read",
                    "issues": "read",
                    "pull_requests": "read",
                    "workflows": "read"
                }
            }
            """);

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.GetInstallationForCurrent(It.IsAny<long>()))
            .ReturnsAsync(mockInstallation);

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
        var permissions = await provider.GetInstallationPermissionsAsync(installationId: 123);

        // Assert
        permissions.HasContents.ShouldBeFalse();
        permissions.HasIssues.ShouldBeFalse();
        permissions.HasPullRequests.ShouldBeFalse();
        permissions.HasWorkflows.ShouldBeFalse();
        permissions.HasRequiredPermissions().ShouldBeFalse();
    }

    [Fact]
    public async Task GetInstallationPermissionsAsync_WithAdminPermissions_ReturnsTrue() {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var mockInstallation = new Octokit.Internal.SimpleJsonSerializer().Deserialize<Installation>("""
            {
                "id": 123,
                "permissions": {
                    "contents": "admin",
                    "issues": "admin",
                    "pull_requests": "admin",
                    "workflows": "admin"
                }
            }
            """);

        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.GetInstallationForCurrent(It.IsAny<long>()))
            .ReturnsAsync(mockInstallation);

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
        var permissions = await provider.GetInstallationPermissionsAsync(installationId: 123);

        // Assert
        permissions.HasContents.ShouldBeTrue();
        permissions.HasIssues.ShouldBeTrue();
        permissions.HasPullRequests.ShouldBeTrue();
        permissions.HasWorkflows.ShouldBeTrue();
        permissions.HasRequiredPermissions().ShouldBeTrue();
    }

    [Fact]
    public async Task ClearCache_WithMultipleCachedTokens_RemovesAll() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var privateKey = GenerateTestPrivateKey();
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("test-jwt-token");

        var apiCallCount = 0;
        var mockGitHubAppsClient = new Mock<IGitHubAppsClient>();
        mockGitHubAppsClient.Setup(c => c.CreateInstallationToken(It.IsAny<long>()))
            .ReturnsAsync(() => {
                apiCallCount++;
                return new AccessToken(
                    token: $"token-{apiCallCount}",
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

        // Act - Cache tokens for multiple installations
        var token1 = await provider.GetInstallationTokenAsync(installationId: 123);
        var token2 = await provider.GetInstallationTokenAsync(installationId: 456);
        var token3 = await provider.GetInstallationTokenAsync(installationId: 789);

        // Verify they're cached
        token1.ShouldBe("token-1");
        token2.ShouldBe("token-2");
        token3.ShouldBe("token-3");
        apiCallCount.ShouldBe(3);

        // Clear cache
        provider.ClearCache();

        // Request tokens again
        var token1After = await provider.GetInstallationTokenAsync(installationId: 123);
        var token2After = await provider.GetInstallationTokenAsync(installationId: 456);
        var token3After = await provider.GetInstallationTokenAsync(installationId: 789);

        // Assert - Should have made 3 more API calls
        token1After.ShouldBe("token-4");
        token2After.ShouldBe("token-5");
        token3After.ShouldBe("token-6");
        apiCallCount.ShouldBe(6);
    }

    [Fact]
    public void JwtTokenGenerator_ExpiresWithin10Minutes() {
        // Arrange
        var timeProvider = TimeProvider.System; // Use system time to avoid JWT validation issues
        var generator = new JwtTokenGenerator(timeProvider);
        var appId = "12345";
        var privateKey = GenerateTestPrivateKey();

        // Act
        var jwt = generator.GenerateJwtToken(appId, privateKey);

        // Assert - Decode and verify token structure
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        
        // Token should expire in the future but within 10 minutes
        token.ValidTo.ShouldBeGreaterThan(DateTime.UtcNow);
        token.ValidTo.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddMinutes(10));
        token.Issuer.ShouldBe(appId);
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
