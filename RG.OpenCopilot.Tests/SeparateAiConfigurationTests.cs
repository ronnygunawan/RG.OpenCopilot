using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Configuration.Models;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class SeparateAiConfigurationTests {
    [Fact]
    public void AddPRGenerationAgentServices_WithSeparatePlannerAndExecutor_RegistersBothKernels() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "planner-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "executor-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o-mini"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var plannerKernel = serviceProvider.GetService<PlannerKernel>();
        var executorKernel = serviceProvider.GetService<ExecutorKernel>();
        
        plannerKernel.ShouldNotBeNull();
        executorKernel.ShouldNotBeNull();
        plannerKernel.Kernel.ShouldNotBeNull();
        executorKernel.Kernel.ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithDifferentProvidersForPlannerAndExecutor_Works() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "AzureOpenAI",
                ["LLM:Planner:ApiKey"] = "planner-api-key",
                ["LLM:Planner:AzureEndpoint"] = "https://planner.openai.azure.com",
                ["LLM:Planner:AzureDeployment"] = "gpt-4o-deployment",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "executor-api-key",
                ["LLM:Executor:ModelId"] = "gpt-3.5-turbo"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var plannerKernel = serviceProvider.GetService<PlannerKernel>();
        var executorKernel = serviceProvider.GetService<ExecutorKernel>();
        
        plannerKernel.ShouldNotBeNull();
        executorKernel.ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithThinkerConfiguration_RegistersOptionalThinkerKernel() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "planner-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "executor-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o-mini",
                ["LLM:Thinker:Provider"] = "OpenAI",
                ["LLM:Thinker:ApiKey"] = "thinker-api-key",
                ["LLM:Thinker:ModelId"] = "gpt-4o"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var thinkerKernel = serviceProvider.GetService<ThinkerKernel>();
        thinkerKernel.ShouldNotBeNull();
        thinkerKernel.Kernel.ShouldNotBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithoutThinkerConfiguration_DoesNotRegisterThinkerKernel() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "planner-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o",
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "executor-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o-mini"
            })
            .Build();

        // Act
        services.AddPRGenerationAgentServices(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var thinkerKernel = serviceProvider.GetService<ThinkerKernel>();
        thinkerKernel.ShouldBeNull();
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithoutPlannerConfiguration_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Executor:Provider"] = "OpenAI",
                ["LLM:Executor:ApiKey"] = "executor-api-key",
                ["LLM:Executor:ModelId"] = "gpt-4o-mini"
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldContain("Planner AI configuration is required");
    }

    [Fact]
    public void AddPRGenerationAgentServices_WithoutExecutorConfiguration_ThrowsException() {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                ["LLM:Planner:Provider"] = "OpenAI",
                ["LLM:Planner:ApiKey"] = "planner-api-key",
                ["LLM:Planner:ModelId"] = "gpt-4o"
            })
            .Build();

        // Act & Assert
        var exception = Should.Throw<InvalidOperationException>(() =>
            services.AddPRGenerationAgentServices(configuration));
        
        exception.Message.ShouldContain("Executor AI configuration is required");
    }

    [Fact]
    public void AiConfiguration_IsValid_ReturnsTrueForValidOpenAIConfig() {
        // Arrange
        var config = new AiConfiguration {
            Provider = "OpenAI",
            ApiKey = "test-key",
            ModelId = "gpt-4o"
        };

        // Act
        var result = config.IsValid();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void AiConfiguration_IsValid_ReturnsTrueForValidAzureOpenAIConfig() {
        // Arrange
        var config = new AiConfiguration {
            Provider = "AzureOpenAI",
            ApiKey = "test-key",
            AzureEndpoint = "https://test.openai.azure.com",
            AzureDeployment = "gpt-4o-deployment"
        };

        // Act
        var result = config.IsValid();

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void AiConfiguration_IsValid_ReturnsFalseForMissingApiKey() {
        // Arrange
        var config = new AiConfiguration {
            Provider = "OpenAI",
            ModelId = "gpt-4o"
        };

        // Act
        var result = config.IsValid();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void AiConfiguration_IsValid_ReturnsFalseForAzureOpenAIMissingEndpoint() {
        // Arrange
        var config = new AiConfiguration {
            Provider = "AzureOpenAI",
            ApiKey = "test-key",
            AzureDeployment = "gpt-4o-deployment"
        };

        // Act
        var result = config.IsValid();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void LlmConfigurations_HasPlannerExecutorAndThinkerProperties() {
        // Arrange
        var configurations = new LlmConfigurations();

        // Assert
        configurations.Planner.ShouldNotBeNull();
        configurations.Executor.ShouldNotBeNull();
        configurations.Thinker.ShouldNotBeNull();
    }
}
