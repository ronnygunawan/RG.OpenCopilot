using Microsoft.SemanticKernel;
using RG.OpenCopilot.PRGenerationAgent;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Factory for creating Semantic Kernel instances from AI configurations
/// </summary>
internal static class KernelFactory {
    /// <summary>
    /// Creates a Kernel instance from an AI configuration
    /// </summary>
    /// <param name="config">The AI configuration</param>
    /// <param name="configName">Name of the configuration (for error messages)</param>
    /// <returns>Configured Kernel instance</returns>
    /// <exception cref="InvalidOperationException">Thrown when configuration is invalid or provider is unsupported</exception>
    public static Kernel CreateKernel(AiConfiguration config, string configName) {
        if (!config.IsValid()) {
            throw new InvalidOperationException(
                $"{configName} AI configuration is invalid. Ensure Provider and ApiKey are set.");
        }

        var kernelBuilder = Kernel.CreateBuilder();

        switch (config.Provider.ToLowerInvariant()) {
            case "openai":
                if (string.IsNullOrWhiteSpace(config.ModelId)) {
                    throw new InvalidOperationException(
                        $"{configName} OpenAI configuration requires ModelId to be set.");
                }
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: config.ModelId,
                    apiKey: config.ApiKey);
                break;

            case "azureopenai":
                if (string.IsNullOrWhiteSpace(config.AzureEndpoint) || 
                    string.IsNullOrWhiteSpace(config.AzureDeployment)) {
                    throw new InvalidOperationException(
                        $"{configName} Azure OpenAI configuration requires AzureEndpoint and AzureDeployment to be set.");
                }
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: config.AzureDeployment,
                    endpoint: config.AzureEndpoint,
                    apiKey: config.ApiKey);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported LLM provider for {configName}: {config.Provider}. " +
                    $"Supported providers: OpenAI, AzureOpenAI. " +
                    $"For Claude or Gemini models, use OpenAI-compatible endpoints or extend with custom connectors.");
        }

        return kernelBuilder.Build();
    }
}
