using Microsoft.Extensions.Configuration;
using Moq;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Authentication;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class GitHubClientFactoryTests {
    [Fact]
    public async Task GetClientForInstallationAsync_WithValidToken_ReturnsAuthenticatedClient() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-installation-token");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "test-key" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act
        var client = await factory.GetClientForInstallationAsync(installationId: 123);

        // Assert
        client.ShouldNotBeNull();
        // Can't directly assert on credentials since IGitHubClient doesn't expose them
        mockTokenProvider.Verify(p => p.GetInstallationTokenAsync(123, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithMultipleInstallations_ReturnsCorrectClients() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-for-123");
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(456, It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-for-456");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "test-key" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act
        var client1 = await factory.GetClientForInstallationAsync(installationId: 123);
        var client2 = await factory.GetClientForInstallationAsync(installationId: 456);

        // Assert
        client1.ShouldNotBeNull();
        client2.ShouldNotBeNull();
        mockTokenProvider.Verify(p => p.GetInstallationTokenAsync(123, It.IsAny<CancellationToken>()), Times.Once);
        mockTokenProvider.Verify(p => p.GetInstallationTokenAsync(456, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithMissingAppCredentials_FallsBackToPAT() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub App credentials not configured"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:Token", "ghp_test_personal_access_token" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act
        var client = await factory.GetClientForInstallationAsync(installationId: 123);

        // Assert
        client.ShouldNotBeNull();
        // Can't directly assert on credentials since IGitHubClient doesn't expose them
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithNoAuthenticationMethod_ThrowsException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub App credentials not configured"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:Token", null }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => factory.GetClientForInstallationAsync(installationId: 123));
        
        exception.Message.ShouldBe("GitHub App credentials or personal access token must be configured");
    }

    [Fact]
    public void GetClientWithJwt_WithValidCredentials_ReturnsAuthenticatedClient() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        var privateKey = GenerateTestPrivateKey();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act
        var client = factory.GetClientWithJwt();

        // Assert
        client.ShouldNotBeNull();
        // Can't directly assert on credentials since IGitHubClient doesn't expose them
    }

    [Fact]
    public void GetClientWithJwt_WithMissingCredentials_ThrowsException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", null },
                { "GitHub:AppPrivateKey", null }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.GetClientWithJwt());
        
        exception.Message.ShouldBe("GitHub App credentials (AppId and PrivateKey) must be configured to use JWT authentication");
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithTokenProviderException_PropagatesException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiException("GitHub API error", System.Net.HttpStatusCode.InternalServerError));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "test-key" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<ApiException>(
            () => factory.GetClientForInstallationAsync(installationId: 123));
        
        exception.Message.ShouldContain("GitHub API error");
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithEmptyPAT_ThrowsException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub App credentials not configured"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:Token", "" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => factory.GetClientForInstallationAsync(installationId: 123));
        
        exception.Message.ShouldBe("GitHub App credentials or personal access token must be configured");
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithCancellationToken_PassesToProvider() {
        // Arrange
        using var cts = new CancellationTokenSource();
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-token");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "test-key" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act
        await factory.GetClientForInstallationAsync(installationId: 123, cancellationToken: cts.Token);

        // Assert
        mockTokenProvider.Verify(p => p.GetInstallationTokenAsync(123, cts.Token), Times.Once);
    }

    [Fact]
    public void GetClientWithJwt_WithOnlyAppId_ThrowsException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", null }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.GetClientWithJwt());
        
        exception.Message.ShouldBe("GitHub App credentials (AppId and PrivateKey) must be configured to use JWT authentication");
    }

    [Fact]
    public void GetClientWithJwt_WithOnlyPrivateKey_ThrowsException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        var privateKey = GenerateTestPrivateKey();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", null },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.GetClientWithJwt());
        
        exception.Message.ShouldBe("GitHub App credentials (AppId and PrivateKey) must be configured to use JWT authentication");
    }

    [Fact]
    public async Task GetClientForInstallationAsync_CalledMultipleTimes_CreatesNewClientEachTime() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-installation-token");

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "test-key" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act
        var client1 = await factory.GetClientForInstallationAsync(installationId: 123);
        var client2 = await factory.GetClientForInstallationAsync(installationId: 123);

        // Assert
        client1.ShouldNotBeNull();
        client2.ShouldNotBeNull();
        client1.ShouldNotBe(client2); // Different instances
        mockTokenProvider.Verify(p => p.GetInstallationTokenAsync(123, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithDifferentExceptionMessage_PropagatesException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error occurred"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "test-key" },
                { "GitHub:Token", "ghp_fallback" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert - Should propagate exception, not fall back to PAT
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => factory.GetClientForInstallationAsync(installationId: 123));
        
        exception.Message.ShouldBe("Unexpected error occurred");
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithOperationCanceledException_Propagates() {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "test-key" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => factory.GetClientForInstallationAsync(installationId: 123, cancellationToken: cts.Token));
    }

    [Fact]
    public void GetClientWithJwt_WithEmptyAppId_ThrowsException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        var privateKey = GenerateTestPrivateKey();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.GetClientWithJwt());
        
        exception.Message.ShouldBe("GitHub App credentials (AppId and PrivateKey) must be configured to use JWT authentication");
    }

    [Fact]
    public void GetClientWithJwt_WithEmptyPrivateKey_ThrowsException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", "" }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(
            () => factory.GetClientWithJwt());
        
        exception.Message.ShouldBe("GitHub App credentials (AppId and PrivateKey) must be configured to use JWT authentication");
    }

    [Fact]
    public void GetClientWithJwt_CalledMultipleTimes_CreatesNewClientsWithFreshJwt() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        var privateKey = GenerateTestPrivateKey();
        
        var jwtCallCount = 0;
        var mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        mockJwtGenerator.Setup(j => j.GenerateJwtToken(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(() => {
                jwtCallCount++;
                return $"jwt-token-{jwtCallCount}";
            });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:AppId", "12345" },
                { "GitHub:AppPrivateKey", privateKey }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, mockJwtGenerator.Object, logger);

        // Act
        var client1 = factory.GetClientWithJwt();
        var client2 = factory.GetClientWithJwt();
        var client3 = factory.GetClientWithJwt();

        // Assert - Each call should generate a new JWT
        client1.ShouldNotBeNull();
        client2.ShouldNotBeNull();
        client3.ShouldNotBeNull();
        client1.ShouldNotBe(client2);
        client2.ShouldNotBe(client3);
        jwtCallCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetClientForInstallationAsync_WithNullPAT_ThrowsProperException() {
        // Arrange
        var mockTokenProvider = new Mock<IGitHubAppTokenProvider>();
        mockTokenProvider.Setup(p => p.GetInstallationTokenAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("GitHub App credentials not configured"));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                { "GitHub:Token", null }
            })
            .Build();

        var logger = new TestLogger<GitHubClientFactory>();
        var factory = new GitHubClientFactory(mockTokenProvider.Object, configuration, logger);

        // Act & Assert
        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => factory.GetClientForInstallationAsync(installationId: 123));
        
        exception.Message.ShouldBe("GitHub App credentials or personal access token must be configured");
    }

    private static string GenerateTestPrivateKey() {
        using var rsa = System.Security.Cryptography.RSA.Create(keySizeInBits: 2048);
        return rsa.ExportRSAPrivateKeyPem();
    }

    private class TestLogger<T> : Microsoft.Extensions.Logging.ILogger<T> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
