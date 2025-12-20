using Microsoft.Extensions.Time.Testing;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class InstallationTokenTests {
    [Fact]
    public void IsValid_WithFutureExpiration_ReturnsTrue() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var token = new InstallationToken {
            InstallationId = 123,
            Token = "test-token",
            ExpiresAt = new DateTime(2024, 1, 1, 13, 0, 0, DateTimeKind.Utc) // 1 hour in future
        };

        // Act
        var isValid = token.IsValid(timeProvider);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void IsValid_WithExpirationWithin5Minutes_ReturnsFalse() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var token = new InstallationToken {
            InstallationId = 123,
            Token = "test-token",
            ExpiresAt = new DateTime(2024, 1, 1, 12, 4, 0, DateTimeKind.Utc) // 4 minutes in future
        };

        // Act
        var isValid = token.IsValid(timeProvider);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithPastExpiration_ReturnsFalse() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var token = new InstallationToken {
            InstallationId = 123,
            Token = "test-token",
            ExpiresAt = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc) // 1 hour in past
        };

        // Act
        var isValid = token.IsValid(timeProvider);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithExpirationExactly5MinutesInFuture_ReturnsFalse() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var token = new InstallationToken {
            InstallationId = 123,
            Token = "test-token",
            ExpiresAt = new DateTime(2024, 1, 1, 12, 5, 0, DateTimeKind.Utc) // exactly 5 minutes in future
        };

        // Act
        var isValid = token.IsValid(timeProvider);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void IsValid_WithExpirationJustOver5MinutesInFuture_ReturnsTrue() {
        // Arrange
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var token = new InstallationToken {
            InstallationId = 123,
            Token = "test-token",
            ExpiresAt = new DateTime(2024, 1, 1, 12, 5, 1, DateTimeKind.Utc) // 5 minutes and 1 second
        };

        // Act
        var isValid = token.IsValid(timeProvider);

        // Assert
        isValid.ShouldBeTrue();
    }
}
