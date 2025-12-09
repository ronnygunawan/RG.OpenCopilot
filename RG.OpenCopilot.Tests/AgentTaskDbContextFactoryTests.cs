using Microsoft.EntityFrameworkCore;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class AgentTaskDbContextFactoryTests {
    [Fact]
    public void CreateDbContext_WithEmptyArgs_CreatesContext() {
        // Arrange
        var factory = new AgentTaskDbContextFactory();
        var args = Array.Empty<string>();

        // Act
        var context = factory.CreateDbContext(args);

        // Assert
        context.ShouldNotBeNull();
        context.ShouldBeOfType<AgentTaskDbContext>();
    }

    [Fact]
    public void CreateDbContext_WithArgs_CreatesContext() {
        // Arrange
        var factory = new AgentTaskDbContextFactory();
        var args = new[] { "arg1", "arg2" };

        // Act
        var context = factory.CreateDbContext(args);

        // Assert
        context.ShouldNotBeNull();
        context.ShouldBeOfType<AgentTaskDbContext>();
    }

    [Fact]
    public void CreateDbContext_CreatesContextWithNpgsqlProvider() {
        // Arrange
        var factory = new AgentTaskDbContextFactory();
        var args = Array.Empty<string>();

        // Act
        var context = factory.CreateDbContext(args);

        // Assert
        context.Database.ProviderName.ShouldBe("Npgsql.EntityFrameworkCore.PostgreSQL");
    }
}
