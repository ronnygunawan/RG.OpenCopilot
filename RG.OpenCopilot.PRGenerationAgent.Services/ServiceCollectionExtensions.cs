using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;
using RG.OpenCopilot.PRGenerationAgent.Services.DependencyManagement;
using RG.OpenCopilot.PRGenerationAgent.Services.Docker;
using RG.OpenCopilot.PRGenerationAgent.Services.Executor;
using RG.OpenCopilot.PRGenerationAgent.Services.FileOperations;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Authentication;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Git.Adapters;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Git.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Repository;
using RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;
using RG.OpenCopilot.PRGenerationAgent.Services.Planner;

namespace RG.OpenCopilot.PRGenerationAgent.Services;

/// <summary>
/// Extension methods for configuring PR Generation Agent services
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds all PR Generation Agent services to the service collection
    /// </summary>
    public static IServiceCollection AddPRGenerationAgentServices(
        this IServiceCollection services,
        IConfiguration configuration) {
        
        // Configure Semantic Kernel
        var kernelBuilder = Kernel.CreateBuilder();

        // Configure LLM provider based on configuration
        var llmProvider = configuration["LLM:Provider"] ?? "OpenAI";
        var apiKey = configuration["LLM:ApiKey"] ?? throw new InvalidProgramException("Unconfigured LLM:ApiKey");
        var modelId = configuration["LLM:ModelId"] ?? "gpt-4o";

        // Configure the appropriate LLM provider
        switch (llmProvider.ToLowerInvariant()) {
            case "openai":
                // Supports GPT-4o, GPT-5, GPT-5-Codex, GPT-5.1, GPT-5.1-Codex (when available)
                kernelBuilder.AddOpenAIChatCompletion(
                    modelId: modelId,
                    apiKey: apiKey);
                break;

            case "azureopenai":
                var azureEndpoint = configuration["LLM:AzureEndpoint"];
                var azureDeployment = configuration["LLM:AzureDeployment"];

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
        services.AddSingleton(kernel);
        
        // Configure Database
        var connectionString = configuration.GetConnectionString("AgentTaskDatabase");
        var usePostgreSQL = !string.IsNullOrEmpty(connectionString);
        
        if (usePostgreSQL) {
            services.AddDbContext<AgentTaskDbContext>(options =>
                options.UseNpgsql(connectionString));
            services.AddScoped<IAgentTaskStore, PostgreSqlAgentTaskStore>();
        } else {
            services.AddSingleton<IAgentTaskStore, InMemoryAgentTaskStore>();
        }
        
        // Register services
        services.AddSingleton<IPlannerService, LlmPlannerService>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IGitHubAppTokenProvider, GitHubAppTokenProvider>();
        services.AddSingleton<IContainerManager, DockerContainerManager>();
        services.AddSingleton<ICommandExecutor, ProcessCommandExecutor>();
        services.AddSingleton<IExecutorService, ContainerExecutorService>();
        services.AddSingleton<IWebhookHandler, WebhookHandler>();
        services.AddSingleton<IWebhookValidator, WebhookValidator>();
        services.AddSingleton<IRepositoryAnalyzer, RepositoryAnalyzer>();
        services.AddSingleton<IInstructionsLoader, InstructionsLoader>();
        services.AddSingleton<IFileAnalyzer, FileAnalyzer>();
        services.AddSingleton<IFileEditor, FileEditor>();
        services.AddSingleton<IMultiFileRefactoringCoordinator, MultiFileRefactoringCoordinator>();
        services.AddSingleton<IStepAnalyzer, StepAnalyzer>();
        services.AddSingleton<IBuildVerifier, BuildVerifier>();
        services.AddSingleton<ITestValidator, TestValidator>();
        services.AddSingleton<ICodeQualityChecker, CodeQualityChecker>();
        services.AddSingleton<ISmartStepExecutor, SmartStepExecutor>();
        services.AddSingleton<ITestGenerator, TestGenerator>();
        services.AddSingleton<IProgressReporter, ProgressReporter>();
        services.AddSingleton<IDependencyManager, DependencyManager>();
        services.AddSingleton<IDocumentationGenerator, DocumentationGenerator>();

        // Configure GitHub client
        services.AddSingleton<IGitHubClient>(sp => {
            var client = new GitHubClient(new ProductHeaderValue("RG-OpenCopilot"));

            // For POC, use a personal access token if provided
            var token = configuration["GitHub:Token"];
            if (!string.IsNullOrEmpty(token)) {
                client.Credentials = new Credentials(token);
            }

            return client;
        });

        // Register GitHub API adapters
        services.AddSingleton<IGitHubRepositoryAdapter>(sp =>
            new GitHubRepositoryAdapter(sp.GetRequiredService<IGitHubClient>()));
        services.AddSingleton<IGitHubGitAdapter>(sp =>
            new GitHubGitAdapter(sp.GetRequiredService<IGitHubClient>()));
        services.AddSingleton<IGitHubPullRequestAdapter>(sp =>
            new GitHubPullRequestAdapter(sp.GetRequiredService<IGitHubClient>()));
        services.AddSingleton<IGitHubIssueAdapter>(sp =>
            new GitHubIssueAdapter(sp.GetRequiredService<IGitHubClient>()));

        services.AddSingleton<IGitHubService, GitHubService>();

        // Configure background job processing
        var jobOptions = new BackgroundJobOptions();
        configuration.GetSection("BackgroundJobs").Bind(jobOptions);
        services.AddSingleton(jobOptions);

        // Register job infrastructure
        services.AddSingleton<IJobQueue>(sp => new ChannelJobQueue(jobOptions));
        services.AddSingleton<IJobStatusStore, InMemoryJobStatusStore>();
        services.AddSingleton<IRetryPolicyCalculator, RetryPolicyCalculator>();
        services.AddSingleton<IJobDeduplicationService, InMemoryJobDeduplicationService>();
        services.AddSingleton<IJobDispatcher>(sp => {
            var queue = sp.GetRequiredService<IJobQueue>();
            var statusStore = sp.GetRequiredService<IJobStatusStore>();
            var deduplicationService = sp.GetRequiredService<IJobDeduplicationService>();
            var logger = sp.GetRequiredService<ILogger<JobDispatcher>>();
            return new JobDispatcher(queue, statusStore, deduplicationService, logger);
        });
        
        // Register job handlers - they will be auto-registered when dispatcher is first resolved
        services.AddSingleton<IJobHandler, GeneratePlanJobHandler>();
        services.AddSingleton<IJobHandler, ExecutePlanJobHandler>();

        // Register background job processor with handler initialization
        services.AddHostedService<BackgroundJobProcessor>(sp => {
            var dispatcher = sp.GetRequiredService<IJobDispatcher>();
            var handlers = sp.GetServices<IJobHandler>();
            
            // Register all handlers with the dispatcher
            foreach (var handler in handlers) {
                dispatcher.RegisterHandler(handler);
            }
            
            var queue = sp.GetRequiredService<IJobQueue>();
            var jobStatusStore = sp.GetRequiredService<IJobStatusStore>();
            var retryPolicyCalculator = sp.GetRequiredService<IRetryPolicyCalculator>();
            var deduplicationService = sp.GetRequiredService<IJobDeduplicationService>();
            var logger = sp.GetRequiredService<ILogger<BackgroundJobProcessor>>();
            return new BackgroundJobProcessor(queue, dispatcher, jobStatusStore, retryPolicyCalculator, deduplicationService, jobOptions, logger);
        });

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations to the database if PostgreSQL is configured
    /// </summary>
    public static void ApplyDatabaseMigrations(this IServiceProvider serviceProvider) {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetService<AgentTaskDbContext>();
        
        if (context != null) {
            // Only apply migrations if PostgreSQL is configured
            context.Database.Migrate();
        }
    }
}
