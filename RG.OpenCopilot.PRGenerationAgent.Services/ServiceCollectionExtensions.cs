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
        
        // Load LLM configurations
        var llmConfigurations = new LlmConfigurations();
        configuration.GetSection("LLM").Bind(llmConfigurations);

        // Create and register Planner Kernel
        if (!llmConfigurations.Planner.IsValid()) {
            // Provide specific error message based on what's missing
            if (string.IsNullOrWhiteSpace(llmConfigurations.Planner.Provider) || 
                string.IsNullOrWhiteSpace(llmConfigurations.Planner.ApiKey)) {
                throw new InvalidOperationException(
                    "Planner AI configuration is required. Set LLM:Planner:Provider and LLM:Planner:ApiKey.");
            }
            if (llmConfigurations.Planner.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException(
                    "Planner Azure OpenAI configuration requires AzureEndpoint and AzureDeployment to be set.");
            }
            throw new InvalidOperationException(
                "Planner AI configuration is invalid. Ensure Provider, ApiKey, and ModelId are properly set.");
        }
        var plannerKernel = KernelFactory.CreateKernel(llmConfigurations.Planner, "Planner");
        services.AddSingleton(new PlannerKernel(plannerKernel));

        // Create and register Executor Kernel
        if (!llmConfigurations.Executor.IsValid()) {
            // Provide specific error message based on what's missing
            if (string.IsNullOrWhiteSpace(llmConfigurations.Executor.Provider) || 
                string.IsNullOrWhiteSpace(llmConfigurations.Executor.ApiKey)) {
                throw new InvalidOperationException(
                    "Executor AI configuration is required. Set LLM:Executor:Provider and LLM:Executor:ApiKey.");
            }
            if (llmConfigurations.Executor.Provider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException(
                    "Executor Azure OpenAI configuration requires AzureEndpoint and AzureDeployment to be set.");
            }
            throw new InvalidOperationException(
                "Executor AI configuration is invalid. Ensure Provider, ApiKey, and ModelId are properly set.");
        }
        var executorKernel = KernelFactory.CreateKernel(llmConfigurations.Executor, "Executor");
        services.AddSingleton(new ExecutorKernel(executorKernel));

        // Create and register Thinker Kernel (optional for now, as Research Agent doesn't exist yet)
        if (llmConfigurations.Thinker.IsValid()) {
            var thinkerKernel = KernelFactory.CreateKernel(llmConfigurations.Thinker, "Thinker");
            services.AddSingleton(new ThinkerKernel(thinkerKernel));
        }
        // Note: Thinker is optional since Research Agent doesn't exist yet
        
        // Register the configuration itself for reference
        services.AddSingleton(llmConfigurations);
        
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
        
        // Register infrastructure services
        services.AddSingleton<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddSingleton<IAuditLogger, AuditLogger>();
        services.AddSingleton<IHealthCheckService, HealthCheckService>();
        
        // Register services
        services.AddSingleton<IPlannerService, LlmPlannerService>();
        services.AddSingleton<ICodeGenerator, CodeGenerator>();
        services.AddSingleton<IJwtTokenGenerator>(sp => 
            new JwtTokenGenerator(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IGitHubAppTokenProvider, GitHubAppTokenProvider>();
        services.AddSingleton<IGitHubClientFactory, GitHubClientFactory>();
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
            var timeProvider = sp.GetRequiredService<TimeProvider>();
            var logger = sp.GetRequiredService<ILogger<BackgroundJobProcessor>>();
            return new BackgroundJobProcessor(queue, dispatcher, jobStatusStore, retryPolicyCalculator, deduplicationService, jobOptions, timeProvider, logger);
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
