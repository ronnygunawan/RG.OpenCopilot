using System.Security.Cryptography;
using System.Text;

namespace RG.OpenCopilot.App;

public interface IWebhookValidator
{
    bool ValidateSignature(string payload, string signature, string secret);
}

public sealed class WebhookValidator : IWebhookValidator
{
    public bool ValidateSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        // GitHub signature format is "sha256=<hash>"
        if (!signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hashValue = signature[7..];
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        var computedHash = Convert.ToHexString(hash).ToLowerInvariant();

        return hashValue.Equals(computedHash, StringComparison.OrdinalIgnoreCase);
    }
}
