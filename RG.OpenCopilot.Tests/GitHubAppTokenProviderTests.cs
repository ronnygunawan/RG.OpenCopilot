using Microsoft.Extensions.Configuration;
using Moq;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services;
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
