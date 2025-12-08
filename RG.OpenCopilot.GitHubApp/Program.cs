using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Models;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Services;

public partial class Program {
    [ExcludeFromCodeCoverage]
    private static void Main(string[] args) {
        var builder = WebApplication.CreateBuilder(args);

        // Add PR Generation Agent services
        builder.Services.AddPRGenerationAgentServices(builder.Configuration);

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

                var eventTypeSanitized = context.Request.Headers["X-GitHub-Event"].ToString().Replace("\r", "").Replace("\n", "");
                logger.LogInformation("Received webhook: {EventType}", eventTypeSanitized);

                // Check if this is an issues event
                var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
                var eventTypeSanitizedForLog = eventType.Replace("\r", "").Replace("\n", "");
                if (eventType != "issues") {
                    logger.LogInformation("Ignoring non-issues event: {EventType}", eventTypeSanitizedForLog);
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

