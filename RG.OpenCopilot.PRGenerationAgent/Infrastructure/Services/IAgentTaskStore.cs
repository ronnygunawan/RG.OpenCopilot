namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

public interface IAgentTaskStore {
    Task<AgentTask?> GetTaskAsync(string id, CancellationToken cancellationToken = default);
    Task<AgentTask> CreateTaskAsync(AgentTask task, CancellationToken cancellationToken = default);
    Task UpdateTaskAsync(AgentTask task, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentTask>> GetTasksByInstallationIdAsync(long installationId, CancellationToken cancellationToken = default);
}
