namespace RG.OpenCopilot.PRGenerationAgent.Configuration.Models;

/// <summary>
/// Configuration for an AI/LLM provider
/// </summary>
public sealed class AiConfiguration {
    /// <summary>
    /// Provider type (OpenAI, AzureOpenAI, Claude, Gemini)
    /// </summary>
    public string Provider { get; init; } = "";

    /// <summary>
    /// API key for the LLM provider
    /// </summary>
    public string ApiKey { get; init; } = "";

    /// <summary>
    /// Model identifier (e.g., gpt-4o, gpt-3.5-turbo, claude-opus-4-5)
    /// </summary>
    public string ModelId { get; init; } = "";

    /// <summary>
    /// Azure OpenAI endpoint URL (only for AzureOpenAI provider)
    /// </summary>
    public string? AzureEndpoint { get; init; }

    /// <summary>
    /// Azure OpenAI deployment name (only for AzureOpenAI provider)
    /// </summary>
    public string? AzureDeployment { get; init; }

    /// <summary>
    /// Validates that the configuration is properly set
    /// </summary>
    public bool IsValid() {
        if (string.IsNullOrWhiteSpace(Provider)) {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiKey)) {
            return false;
        }

        if (Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)) {
            if (string.IsNullOrWhiteSpace(AzureEndpoint) || string.IsNullOrWhiteSpace(AzureDeployment)) {
                return false;
            }
        } else {
            if (string.IsNullOrWhiteSpace(ModelId)) {
                return false;
            }
        }

        return true;
    }
}
