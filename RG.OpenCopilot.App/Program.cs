using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Octokit;
using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;

public partial class Program {
    [ExcludeFromCodeCoverage]
    private static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Semantic Kernel
        var kernelBuilder = Kernel.CreateBuilder();

        // Configure LLM provider based on configuration
        var llmProvider = builder.Configuration["LLM:Provider"] ?? "OpenAI";
        var apiKey = builder.Configuration["LLM:ApiKey"];
        var modelId = builder.Configuration["LLM:ModelId"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(apiKey)) {
            // For development/testing, use SimplePlannerService if no API key is configured
            builder.Services.AddSingleton<IPlannerService, SimplePlannerService>();
        }
        else {
            // Configure the appropriate LLM provider
            switch (llmProvider.ToLowerInvariant()) {
                case "openai":
                    // Supports GPT-4o, GPT-5, GPT-5-Codex, GPT-5.1, GPT-5.1-Codex (when available)
                    kernelBuilder.AddOpenAIChatCompletion(
                        modelId: modelId,
                        apiKey: apiKey);
                    break;

                case "azureopenai":
                    var azureEndpoint = builder.Configuration["LLM:AzureEndpoint"];
                    var azureDeployment = builder.Configuration["LLM:AzureDeployment"];

                    if (string.IsNullOrEmpty(azureEndpoint) || string.IsNullOrEmpty(azureDeployment)) {
                        throw new InvalidOperationException(
                            "Azure OpenAI requires LLM:AzureEndpoint and LLM:AzureDeployment configuration");
                    }

                    kernelBuilder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureDeployment,
                        endpoint: azureEndpoint,
                        apiKey: apiKey);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported LLM provider: {llmProvider}. Supported providers: OpenAI, AzureOpenAI. " +
                        $"For Claude or Gemini models, use OpenAI-compatible endpoints or extend with custom connectors.");
            }

            var kernel = kernelBuilder.Build();
            builder.Services.AddSingleton(kernel);
            builder.Services.AddSingleton<IPlannerService, LlmPlannerService>();
        }

        // Configure other services
        builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        builder.Services.AddSingleton<IGitHubAppTokenProvider, GitHubAppTokenProvider>();
        builder.Services.AddSingleton<IContainerManager, DockerContainerManager>();
        builder.Services.AddSingleton<ICommandExecutor, ProcessCommandExecutor>();
        builder.Services.AddSingleton<IExecutorService, ContainerExecutorService>();
        builder.Services.AddSingleton<IAgentTaskStore, InMemoryAgentTaskStore>();
        builder.Services.AddSingleton<IWebhookHandler, WebhookHandler>();
        builder.Services.AddSingleton<IWebhookValidator, WebhookValidator>();
        builder.Services.AddSingleton<IRepositoryAnalyzer, RepositoryAnalyzer>();
        builder.Services.AddSingleton<IInstructionsLoader, InstructionsLoader>();
        builder.Services.AddSingleton<IFileAnalyzer, FileAnalyzer>();
        builder.Services.AddSingleton<IFileEditor, FileEditor>();

        // Configure GitHub client
        builder.Services.AddSingleton<IGitHubClient>(sp => {
            var client = new GitHubClient(new ProductHeaderValue("RG-OpenCopilot"));

            // For POC, use a personal access token if provided
            var token = builder.Configuration["GitHub:Token"];
            if (!string.IsNullOrEmpty(token)) {
                client.Credentials = new Credentials(token);
            }

            return client;
        });

        // Register GitHub API adapters
        builder.Services.AddSingleton<IGitHubRepositoryAdapter>(sp =>
            new GitHubRepositoryAdapter(sp.GetRequiredService<IGitHubClient>()));
        builder.Services.AddSingleton<IGitHubGitAdapter>(sp =>
            new GitHubGitAdapter(sp.GetRequiredService<IGitHubClient>()));
        builder.Services.AddSingleton<IGitHubPullRequestAdapter>(sp =>
            new GitHubPullRequestAdapter(sp.GetRequiredService<IGitHubClient>()));
        builder.Services.AddSingleton<IGitHubIssueAdapter>(sp =>
            new GitHubIssueAdapter(sp.GetRequiredService<IGitHubClient>()));

        builder.Services.AddSingleton<IGitHubService, GitHubService>();

        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok("ok"));

        app.MapPost("/github/webhook", async (HttpContext context, IWebhookHandler handler, IWebhookValidator validator, IConfiguration config, ILogger<WebhookEndpoint> logger) => {
            try {
                // Read the request body
                using var reader = new StreamReader(context.Request.Body);
                var body = await reader.ReadToEndAsync();

                // Validate signature if webhook secret is configured
                var webhookSecret = config["GitHub:WebhookSecret"];
                if (!string.IsNullOrEmpty(webhookSecret)) {
                    var signature = context.Request.Headers["X-Hub-Signature-256"].ToString();
                    if (!validator.ValidateSignature(body, signature, webhookSecret)) {
                        logger.LogWarning("Invalid webhook signature");
                        return Results.Unauthorized();
                    }
                }

                logger.LogInformation("Received webhook: {EventType}", context.Request.Headers["X-GitHub-Event"].ToString());

                // Check if this is an issues event
                var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
                if (eventType != "issues") {
                    logger.LogInformation("Ignoring non-issues event: {EventType}", eventType);
                    return Results.Ok();
                }

                // Parse the payload
                var payload = JsonSerializer.Deserialize<GitHubIssueEventPayload>(body, new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                });

                if (payload == null) {
                    logger.LogWarning("Failed to parse webhook payload");
                    return Results.BadRequest("Invalid payload");
                }

                // Handle the event
                await handler.HandleIssuesEventAsync(payload);

                return Results.Ok();
            }
            catch (Exception ex) {
                logger.LogError(ex, "Error processing webhook");
                return Results.StatusCode(500);
            }
        });

        app.Run();
    }
}

// Marker class for logging in webhook endpoint
internal sealed class WebhookEndpoint { }

