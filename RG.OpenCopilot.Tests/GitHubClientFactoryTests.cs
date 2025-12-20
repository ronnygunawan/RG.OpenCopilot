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
