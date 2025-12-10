using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent.Execution.Models;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using RG.OpenCopilot.PRGenerationAgent.Planning.Models;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;

internal sealed class AgentTaskDbContext : DbContext {
    public DbSet<AgentTask> Tasks => Set<AgentTask>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

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

        modelBuilder.Entity<AuditLog>(entity => {
            entity.ToTable("audit_logs");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.EventType)
                .HasColumnName("event_type")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Timestamp)
                .HasColumnName("timestamp")
                .IsRequired();

            entity.Property(e => e.CorrelationId)
                .HasColumnName("correlation_id")
                .HasMaxLength(100);

            entity.Property(e => e.Description)
                .HasColumnName("description")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(e => e.Initiator)
                .HasColumnName("initiator")
                .HasMaxLength(255);

            entity.Property(e => e.Target)
                .HasColumnName("target")
                .HasMaxLength(500);

            entity.Property(e => e.Result)
                .HasColumnName("result")
                .HasMaxLength(50);

            entity.Property(e => e.DurationMs)
                .HasColumnName("duration_ms");

            entity.Property(e => e.ErrorMessage)
                .HasColumnName("error_message")
                .HasMaxLength(2000);

            // Store Data as JSON using property-level serialization
            entity.Property(e => e.Data)
                .HasColumnName("data")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

            // Indexes for common queries
            entity.HasIndex(e => e.EventType)
                .HasDatabaseName("ix_audit_logs_event_type");

            entity.HasIndex(e => e.CorrelationId)
                .HasDatabaseName("ix_audit_logs_correlation_id");

            entity.HasIndex(e => e.Timestamp)
                .HasDatabaseName("ix_audit_logs_timestamp");

            entity.HasIndex(e => new { e.EventType, e.Timestamp })
                .HasDatabaseName("ix_audit_logs_event_type_timestamp");
        });
    }
}
