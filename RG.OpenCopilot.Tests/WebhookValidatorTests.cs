using RG.OpenCopilot.App;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class WebhookValidatorTests
{
    [Fact]
    public void ValidateSignature_ReturnsTrueForValidSignature()
    {
        // Arrange
        var validator = new WebhookValidator();
        var payload = @"{""action"":""labeled"",""issue"":{""number"":1}}";
        var secret = "test-secret";
        
        // Generate a valid signature
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        // Act
        var isValid = validator.ValidateSignature(payload, signature, secret);

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidateSignature_ReturnsFalseForInvalidSignature()
    {
        // Arrange
        var validator = new WebhookValidator();
        var payload = @"{""action"":""labeled"",""issue"":{""number"":1}}";
        var secret = "test-secret";
        var signature = "sha256=invalid-signature";

        // Act
        var isValid = validator.ValidateSignature(payload, signature, secret);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSignature_ReturnsFalseForEmptySignature()
    {
        // Arrange
        var validator = new WebhookValidator();
        var payload = @"{""action"":""labeled""}";
        var secret = "test-secret";

        // Act
        var isValid = validator.ValidateSignature(payload, "", secret);

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSignature_ReturnsFalseForEmptySecret()
    {
        // Arrange
        var validator = new WebhookValidator();
        var payload = @"{""action"":""labeled""}";
        var signature = "sha256=somehash";

        // Act
        var isValid = validator.ValidateSignature(payload, signature, "");

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSignature_ReturnsFalseForWrongSecret()
    {
        // Arrange
        var validator = new WebhookValidator();
        var payload = @"{""action"":""labeled"",""issue"":{""number"":1}}";
        var secret = "test-secret";
        
        // Generate a signature with the correct secret
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload));
        var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        // Act - validate with a different secret
        var isValid = validator.ValidateSignature(payload, signature, "wrong-secret");

        // Assert
        isValid.ShouldBeFalse();
    }

    [Fact]
    public void ValidateSignature_HandlesDifferentPayloads()
    {
        // Arrange
        var validator = new WebhookValidator();
        var payload1 = @"{""action"":""opened""}";
        var payload2 = @"{""action"":""closed""}";
        var secret = "shared-secret";
        
        using var hmac1 = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash1 = hmac1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload1));
        var signature1 = "sha256=" + Convert.ToHexString(hash1).ToLowerInvariant();

        using var hmac2 = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash2 = hmac2.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payload2));
        var signature2 = "sha256=" + Convert.ToHexString(hash2).ToLowerInvariant();

        // Act & Assert
        validator.ValidateSignature(payload1, signature1, secret).ShouldBeTrue();
        validator.ValidateSignature(payload2, signature2, secret).ShouldBeTrue();
        validator.ValidateSignature(payload1, signature2, secret).ShouldBeFalse();
        validator.ValidateSignature(payload2, signature1, secret).ShouldBeFalse();
    }

    [Fact]
    public void ValidateSignature_ReturnsFalseForMalformedSignature()
    {
        // Arrange
        var validator = new WebhookValidator();
        var payload = @"{""action"":""labeled""}";
        var secret = "test-secret";

        // Act & Assert - various malformed signatures
        validator.ValidateSignature(payload, "notsha256=hash", secret).ShouldBeFalse();
        validator.ValidateSignature(payload, "sha256", secret).ShouldBeFalse();
        validator.ValidateSignature(payload, "sha256=", secret).ShouldBeFalse();
    }
}
