using RG.OpenCopilot.Agent.Execution.Models;

namespace RG.OpenCopilot.Agent.Execution.Services;

public interface IExecutorService {
    Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default);
}
