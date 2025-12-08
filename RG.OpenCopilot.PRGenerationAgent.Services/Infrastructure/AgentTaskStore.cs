using System.Collections.Concurrent;
using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

internal sealed class InMemoryAgentTaskStore : IAgentTaskStore {
    private readonly ConcurrentDictionary<string, AgentTask> _tasks = [];

    public Task<AgentTask?> GetTaskAsync(string id, CancellationToken cancellationToken = default) {
        _tasks.TryGetValue(id, out var task);
        return Task.FromResult(task);
    }

    public Task<AgentTask> CreateTaskAsync(AgentTask task, CancellationToken cancellationToken = default) {
        if (!_tasks.TryAdd(task.Id, task)) {
            throw new InvalidOperationException($"Task with ID {task.Id} already exists");
        }
        return Task.FromResult(task);
    }

    public Task UpdateTaskAsync(AgentTask task, CancellationToken cancellationToken = default) {
        _tasks[task.Id] = task;
        return Task.CompletedTask;
    }
}
