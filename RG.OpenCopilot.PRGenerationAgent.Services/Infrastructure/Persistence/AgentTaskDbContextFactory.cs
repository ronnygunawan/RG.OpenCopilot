using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating AgentTaskDbContext during migrations.
/// This is used by EF Core tools when running migration commands.
/// </summary>
internal sealed class AgentTaskDbContextFactory : IDesignTimeDbContextFactory<AgentTaskDbContext> {
    public AgentTaskDbContext CreateDbContext(string[] args) {
        var optionsBuilder = new DbContextOptionsBuilder<AgentTaskDbContext>();
        
        // Use a default connection string for design-time operations
        // This will be overridden at runtime with the actual connection string
        optionsBuilder.UseNpgsql("Host=localhost;Database=opencopilot;Username=postgres;Password=postgres");
        
        return new AgentTaskDbContext(optionsBuilder.Options);
    }
}
