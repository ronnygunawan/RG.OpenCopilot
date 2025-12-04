using RG.OpenCopilot.Agent;
using RG.OpenCopilot.App;
using Octokit;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddSingleton<IPlannerService, SimplePlannerService>();
builder.Services.AddSingleton<IExecutorService, StubExecutorService>();
builder.Services.AddSingleton<IAgentTaskStore, InMemoryAgentTaskStore>();
builder.Services.AddSingleton<IWebhookHandler, WebhookHandler>();
builder.Services.AddSingleton<IWebhookValidator, WebhookValidator>();

// Configure GitHub client
builder.Services.AddSingleton<IGitHubClient>(sp =>
{
    var client = new GitHubClient(new ProductHeaderValue("RG-OpenCopilot"));
    
    // For POC, use a personal access token if provided
    var token = builder.Configuration["GitHub:Token"];
    if (!string.IsNullOrEmpty(token))
    {
        client.Credentials = new Credentials(token);
    }
    
    return client;
});

builder.Services.AddSingleton<IGitHubService, GitHubService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

app.MapPost("/github/webhook", async (HttpContext context, IWebhookHandler handler, IWebhookValidator validator, IConfiguration config, ILogger<Program> logger) =>
{
    try
    {
        // Read the request body
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();
        
        // Validate signature if webhook secret is configured
        var webhookSecret = config["GitHub:WebhookSecret"];
        if (!string.IsNullOrEmpty(webhookSecret))
        {
            var signature = context.Request.Headers["X-Hub-Signature-256"].ToString();
            if (!validator.ValidateSignature(body, signature, webhookSecret))
            {
                logger.LogWarning("Invalid webhook signature");
                return Results.Unauthorized();
            }
        }
        
        logger.LogInformation("Received webhook: {EventType}", context.Request.Headers["X-GitHub-Event"].ToString());
        
        // Check if this is an issues event
        var eventType = context.Request.Headers["X-GitHub-Event"].ToString();
        if (eventType != "issues")
        {
            logger.LogInformation("Ignoring non-issues event: {EventType}", eventType);
            return Results.Ok();
        }
        
        // Parse the payload
        var payload = JsonSerializer.Deserialize<GitHubIssueEventPayload>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        
        if (payload == null)
        {
            logger.LogWarning("Failed to parse webhook payload");
            return Results.BadRequest("Invalid payload");
        }
        
        // Handle the event
        await handler.HandleIssuesEventAsync(payload);
        
        return Results.Ok();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing webhook");
        return Results.StatusCode(500);
    }
});

app.Run();

// Temporary stub executor service (will be implemented in later phases)
file sealed class StubExecutorService : IExecutorService
{
    public Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default)
    {
        task.Status = AgentTaskStatus.Completed;
        return Task.CompletedTask;
    }
}

