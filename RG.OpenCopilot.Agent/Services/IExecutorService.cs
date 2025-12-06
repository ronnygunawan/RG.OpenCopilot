using RG.OpenCopilot.Agent.Models;

namespace RG.OpenCopilot.Agent.Services;

public interface IExecutorService {
    Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default);
}
