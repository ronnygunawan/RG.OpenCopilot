using Microsoft.EntityFrameworkCore;
using RG.OpenCopilot.PRGenerationAgent.Execution.Models;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

internal sealed class PostgreSqlAgentTaskStore : IAgentTaskStore {
    private readonly AgentTaskDbContext _context;

    public PostgreSqlAgentTaskStore(AgentTaskDbContext context) {
        _context = context;
    }

    public async Task<AgentTask?> GetTaskAsync(string id, CancellationToken cancellationToken = default) {
        return await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<AgentTask> CreateTaskAsync(AgentTask task, CancellationToken cancellationToken = default) {
        var existingTask = await _context.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);

        if (existingTask != null) {
            throw new InvalidOperationException($"Task with ID {task.Id} already exists");
        }

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task UpdateTaskAsync(AgentTask task, CancellationToken cancellationToken = default) {
        var existingTask = await _context.Tasks
            .FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);

        if (existingTask == null) {
            // Behavior matches InMemoryAgentTaskStore - create if not exists
            _context.Tasks.Add(task);
        } else {
            // Update all properties
            existingTask.Status = task.Status;
            existingTask.Plan = task.Plan;
            existingTask.StartedAt = task.StartedAt;
            existingTask.CompletedAt = task.CompletedAt;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentTask>> GetTasksByInstallationIdAsync(
        long installationId,
        CancellationToken cancellationToken = default) {
        return await _context.Tasks
            .AsNoTracking()
            .Where(t => t.InstallationId == installationId)
            .ToListAsync(cancellationToken);
    }
}
