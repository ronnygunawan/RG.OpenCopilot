using Shouldly;

namespace RG.OpenCopilot.Tests;

public class CorrelationIdProviderTests {
    [Fact]
    public void GetCorrelationId_WhenNotSet_ReturnsNull() {
        // Arrange
        var provider = new TestCorrelationIdProvider();

        // Act
        var correlationId = provider.GetCorrelationId();

        // Assert
        correlationId.ShouldBeNull();
    }

    [Fact]
    public void SetCorrelationId_WithValidId_SetsValue() {
        // Arrange
        var provider = new TestCorrelationIdProvider();
        var expectedId = "test-correlation-id-123";

        // Act
        provider.SetCorrelationId(expectedId);

        // Assert
        provider.GetCorrelationId().ShouldBe(expectedId);
    }

    [Fact]
    public void GenerateCorrelationId_CreatesNewId() {
        // Arrange
        var provider = new TestCorrelationIdProvider();

        // Act
        var correlationId = provider.GenerateCorrelationId();

        // Assert
        correlationId.ShouldNotBeNullOrWhiteSpace();
        provider.GetCorrelationId().ShouldBe(correlationId);
    }

    [Fact]
    public void GenerateCorrelationId_GeneratesUniqueIds() {
        // Arrange
        var provider = new TestCorrelationIdProvider();

        // Act
        var id1 = provider.GenerateCorrelationId();
        var id2 = provider.GenerateCorrelationId();

        // Assert
        id1.ShouldNotBe(id2);
    }

    [Fact]
    public void SetCorrelationId_OverwritesPreviousValue() {
        // Arrange
        var provider = new TestCorrelationIdProvider();
        provider.SetCorrelationId("first-id");

        // Act
        provider.SetCorrelationId("second-id");

        // Assert
        provider.GetCorrelationId().ShouldBe("second-id");
    }

    [Fact]
    public void SetCorrelationId_WithWhitespace_ThrowsException() {
        // Arrange
        var provider = new CorrelationIdProvider();

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => provider.SetCorrelationId("   "));
        exception.Message.ShouldContain("Correlation ID cannot be null or whitespace");
        exception.ParamName.ShouldBe("correlationId");
    }

    [Fact]
    public void SetCorrelationId_WithEmptyString_ThrowsException() {
        // Arrange
        var provider = new CorrelationIdProvider();

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() => provider.SetCorrelationId(""));
        exception.Message.ShouldContain("Correlation ID cannot be null or whitespace");
        exception.ParamName.ShouldBe("correlationId");
    }
}
