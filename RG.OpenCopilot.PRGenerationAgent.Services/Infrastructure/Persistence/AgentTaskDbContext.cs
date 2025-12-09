using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent.Execution.Models;
using RG.OpenCopilot.PRGenerationAgent.Planning.Models;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;

internal sealed class AgentTaskDbContext : DbContext {
    public DbSet<AgentTask> Tasks => Set<AgentTask>();

    public AgentTaskDbContext(DbContextOptions<AgentTaskDbContext> options) : base(options) {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AgentTask>(entity => {
            entity.ToTable("agent_tasks");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.InstallationId)
                .HasColumnName("installation_id")
                .IsRequired();

            entity.Property(e => e.RepositoryOwner)
                .HasColumnName("repository_owner")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.RepositoryName)
                .HasColumnName("repository_name")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.IssueNumber)
                .HasColumnName("issue_number")
                .IsRequired();

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e => e.StartedAt)
                .HasColumnName("started_at");

            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");

            // Store Plan as JSON using property-level serialization
            entity.Property(e => e.Plan)
                .HasColumnName("plan")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<AgentPlan>(v, (JsonSerializerOptions?)null));

            // Indexes for common queries
            entity.HasIndex(e => e.InstallationId)
                .HasDatabaseName("ix_agent_tasks_installation_id");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("ix_agent_tasks_status");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("ix_agent_tasks_created_at");

            entity.HasIndex(e => new { e.RepositoryOwner, e.RepositoryName })
                .HasDatabaseName("ix_agent_tasks_repository");
        });
    }
}
