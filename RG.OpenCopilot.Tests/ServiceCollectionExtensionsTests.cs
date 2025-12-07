using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Octokit;
using RG.OpenCopilot.PRGenerationAgent.Services;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class ServiceCollectionExtensionsTests {
    [Fact]
    public void AddPRGenerationAgentServices_WithOpenAIConfiguration_RegistersAllServices() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(); // Add logging infrastructure
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key",
                ["LLM:ModelId"] = "gpt-4o"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration); // Register configuration

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<Kernel>().ShouldNotBeNull();
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "AzureOpenAI",
                ["LLM:ApiKey"] = "test-api-key",
                ["LLM:AzureEndpoint"] = "https://test.openai.azure.com",
                ["LLM:AzureDeployment"] = "gpt-4o"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        serviceProvider.GetService<Kernel>().ShouldNotBeNull();
        serviceProvider.GetService<IPlannerService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithDefaultModelId_UsesGpt4o() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key"
                // ModelId not specified
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should not throw and kernel should be registered
        serviceProvider.GetService<Kernel>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithDefaultProvider_UsesOpenAI() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:ApiKey"] = "test-api-key"
                // Provider not specified
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Should not throw and kernel should be registered
        serviceProvider.GetService<Kernel>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithMissingApiKey_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "OpenAI"
                // ApiKey missing
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidProgramException>(() => 
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldBe("Unconfigured LLM:ApiKey");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithAzureOpenAIMissingEndpoint_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "AzureOpenAI",
                ["LLM:ApiKey"] = "test-api-key",
                ["LLM:AzureDeployment"] = "gpt-4o"
                // AzureEndpoint missing
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => 
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldBe("Azure OpenAI requires LLM:AzureEndpoint and LLM:AzureDeployment configuration");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithAzureOpenAIMissingDeployment_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "AzureOpenAI",
                ["LLM:ApiKey"] = "test-api-key",
                ["LLM:AzureEndpoint"] = "https://test.openai.azure.com"
                // AzureDeployment missing
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() => 
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldBe("Azure OpenAI requires LLM:AzureEndpoint and LLM:AzureDeployment configuration");
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
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key",
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
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key"
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
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key"
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
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key"
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
        serviceProvider.GetService<Kernel>().ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_RegistersGitHubAdapters() {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key"
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
                ["LLM:Provider"] = "OpenAI",
                ["LLM:ApiKey"] = "test-api-key"
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
}
