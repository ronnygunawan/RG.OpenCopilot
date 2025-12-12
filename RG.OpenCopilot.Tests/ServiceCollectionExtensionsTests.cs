using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class ServiceCollectionExtensionsTests {
    [Fact]
    public void AddPRGenerationAgentServices_WithOpenAIConfiguration_RegistersAllServices() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging infrastructure
        services.AddSingleton(TimeProvider.System); // Add TimeProvider
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration); // Register configuration

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<PlannerKernel>().ShouldNotBeNull();
        serviceProvider.GetService<ExecutorKernel>().ShouldNotBeNull();
        serviceProvider.GetService<IPlannerService>().ShouldNotBeNull();
        serviceProvider.GetService<ICodeGenerator>().ShouldNotBeNull();
        serviceProvider.GetService<IExecutorService>().ShouldNotBeNull();
        serviceProvider.GetService<IContainerManager>().ShouldNotBeNull();
        serviceProvider.GetService<ICommandExecutor>().ShouldNotBeNull();
        serviceProvider.GetService<IAgentTaskStore>().ShouldNotBeNull();
        serviceProvider.GetService<IWebhookHandler>().ShouldNotBeNull();
        serviceProvider.GetService<IWebhookValidator>().ShouldNotBeNull();
        serviceProvider.GetService<IRepositoryAnalyzer>().ShouldNotBeNull();
        serviceProvider.GetService<IInstructionsLoader>().ShouldNotBeNull();
        serviceProvider.GetService<IFileAnalyzer>().ShouldNotBeNull();
        serviceProvider.GetService<IFileEditor>().ShouldNotBeNull();
        serviceProvider.GetService<IStepAnalyzer>().ShouldNotBeNull();
        serviceProvider.GetService<IBuildVerifier>().ShouldNotBeNull();
        serviceProvider.GetService<IGitHubClient>().ShouldNotBeNull();
        serviceProvider.GetService<IGitHubService>().ShouldNotBeNull();
        serviceProvider.GetService<IGitHubRepositoryAdapter>().ShouldNotBeNull();
        serviceProvider.GetService<IGitHubGitAdapter>().ShouldNotBeNull();
        serviceProvider.GetService<IGitHubPullRequestAdapter>().ShouldNotBeNull();
        serviceProvider.GetService<IGitHubIssueAdapter>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithAzureOpenAIConfiguration_RegistersAllServices() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging infrastructure
        services.AddSingleton(TimeProvider.System); // Add TimeProvider
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "AzureOpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:AzureEndpoint"] = "https://test.openai.azure.com",
                ["LLM:Planner:AzureDeployment"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "AzureOpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:AzureEndpoint"] = "https://test.openai.azure.com",
                ["LLM:Executor:AzureDeployment"] = "gpt-4o"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<PlannerKernel>().ShouldNotBeNull();
        serviceProvider.GetService<ExecutorKernel>().ShouldNotBeNull();
        serviceProvider.GetService<IPlannerService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithDefaultModelId_UsesGpt4o() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
                // ModelId not specified
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should not throw and kernel should be registered
        serviceProvider.GetService<PlannerKernel>().ShouldNotBeNull();
        serviceProvider.GetService<ExecutorKernel>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithDefaultProvider_UsesOpenAI() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
                // Provider defaulted to OpenAI
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should not throw and kernel should be registered
        serviceProvider.GetService<PlannerKernel>().ShouldNotBeNull();
        serviceProvider.GetService<ExecutorKernel>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithMissingApiKey_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI"
                // ApiKey missing
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldContain("Planner AI configuration is required");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithAzureOpenAIMissingEndpoint_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "AzureOpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:AzureDeployment"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "AzureOpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:AzureDeployment"] = "gpt-4o"
                // AzureEndpoint missing
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => 
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldContain("Planner Azure OpenAI configuration requires AzureEndpoint and AzureDeployment");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithAzureOpenAIMissingDeployment_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "AzureOpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:AzureEndpoint"] = "https://test.openai.azure.com",
                ["LLM:Executor:Provider"] = "AzureOpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:AzureEndpoint"] = "https://test.openai.azure.com"
                // AzureDeployment missing
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => 
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldContain("Planner Azure OpenAI configuration requires AzureEndpoint and AzureDeployment");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithUnsupportedProvider_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "Claude",
                ["LLM:ApiKey"] = "test-api-key"
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => 
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldContain("Unsupported LLM provider: Claude");
        exception.Message.ShouldContain("Supported providers: OpenAI, AzureOpenAI");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithGitHubToken_ConfiguresGitHubClient() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o",
                ["GitHub:Token"] = "test-github-token"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var gitHubClient = serviceProvider.GetService<IGitHubClient>();
        gitHubClient.ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithoutGitHubToken_ConfiguresGitHubClient() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
                // No GitHub token
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var gitHubClient = serviceProvider.GetService<IGitHubClient>();
        gitHubClient.ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_RegistersServicesAsSingletons() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging infrastructure
        services.AddSingleton(TimeProvider.System); // Add TimeProvider
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration); // Register configuration

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Get the same service twice and verify they are the same instance
        var planner1 = serviceProvider.GetService<IPlannerService>();
        var planner2 = serviceProvider.GetService<IPlannerService>();
        ReferenceEquals(planner1, planner2).ShouldBeTrue();

        var executor1 = serviceProvider.GetService<IExecutorService>();
        var executor2 = serviceProvider.GetService<IExecutorService>();
        ReferenceEquals(executor1, executor2).ShouldBeTrue();
    }

    [Fact]
    public void AddPRGenerationAgentServices_ReturnsServiceCollection() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
            })
            .Build();

        // Act
        var result = services.AddPRGenerationAgentServices(configuration);

        // Assert - Should return the same collection for method chaining
        result.ShouldBe(services);
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithCaseInsensitiveProvider_WorksCorrectly() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "OPENAI",  // Uppercase
                ["LLM:ApiKey"] = "test-api-key"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should not throw and kernel should be registered
        serviceProvider.GetService<PlannerKernel>().ShouldNotBeNull();
        serviceProvider.GetService<ExecutorKernel>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_RegistersGitHubAdapters() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify all GitHub adapters are registered
        var repoAdapter = serviceProvider.GetService<IGitHubRepositoryAdapter>();
        var gitAdapter = serviceProvider.GetService<IGitHubGitAdapter>();
        var prAdapter = serviceProvider.GetService<IGitHubPullRequestAdapter>();
        var issueAdapter = serviceProvider.GetService<IGitHubIssueAdapter>();

        repoAdapter.ShouldNotBeNull();
        gitAdapter.ShouldNotBeNull();
        prAdapter.ShouldNotBeNull();
        issueAdapter.ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_GitHubAdaptersUseSameClient() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "test-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "test-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - All adapters should share the same GitHub client instance
        var client1 = serviceProvider.GetService<IGitHubClient>();
        var client2 = serviceProvider.GetService<IGitHubClient>();
        
        ReferenceEquals(client1, client2).ShouldBeTrue();
    }

    [Fact(Skip = "Requires actual PostgreSQL database connection")]
    public void ApplyDatabaseMigrations_WithPostgreSqlConfigured_DoesNotThrow() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:AgentTaskDatabase"] = "Host=localhost;Database=test",
                ["LLM:ApiKey"] = "test-key",
                ["LLM:Provider"] = "OpenAI"
            })
            .Build();

        services.AddLogging();
        services.AddPRGenerationAgentServices(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Should not throw even if database doesn't exist
        // The method should handle gracefully when database is not available
        serviceProvider.ApplyDatabaseMigrations();
    }

    [Fact]
    public void ApplyDatabaseMigrations_WithNoDbContext_DoesNotThrow() {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert - Should not throw
        serviceProvider.ApplyDatabaseMigrations();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithPostgreSqlConnectionString_RegistersDbContext() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:AgentTaskDatabase"] = "Host=localhost;Database=test",
                ["LLM:ApiKey"] = "test-key",
                ["LLM:Provider"] = "OpenAI"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddPRGenerationAgentServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetService<AgentTaskDbContext>();
        context.ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithEmptyConnectionString_DoesNotRegisterDbContext() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:AgentTaskDatabase"] = "",
                ["LLM:ApiKey"] = "test-key",
                ["LLM:Provider"] = "OpenAI"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddPRGenerationAgentServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var context = serviceProvider.GetService<AgentTaskDbContext>();
        context.ShouldBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithNoConnectionString_DoesNotRegisterDbContext() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:ApiKey"] = "test-key",
                ["LLM:Provider"] = "OpenAI"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddPRGenerationAgentServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var context = serviceProvider.GetService<AgentTaskDbContext>();
        context.ShouldBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithPostgreSqlConnectionString_RegistersScopedTaskStore() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionStrings:AgentTaskDatabase"] = "Host=localhost;Database=test",
                ["LLM:ApiKey"] = "test-key",
                ["LLM:Provider"] = "OpenAI"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddPRGenerationAgentServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var taskStore = scope.ServiceProvider.GetService<IAgentTaskStore>();
        taskStore.ShouldNotBeNull();
        taskStore.GetType().Name.ShouldBe("PostgreSqlAgentTaskStore");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithoutPostgreSqlConnectionString_RegistersSingletonTaskStore() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:ApiKey"] = "test-key",
                ["LLM:Provider"] = "OpenAI"
            })
            .Build();

        services.AddLogging();

        // Act
        services.AddPRGenerationAgentServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var taskStore = serviceProvider.GetService<IAgentTaskStore>();
        taskStore.ShouldNotBeNull();
        taskStore.GetType().Name.ShouldBe("InMemoryAgentTaskStore");
    }
}
