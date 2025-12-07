using RG.OpenCopilot.PRGenerationAgent.Execution.Models;

namespace RG.OpenCopilot.PRGenerationAgent.Execution.Services;

public interface IExecutorService {
    Task ExecutePlanAsync(AgentTask task, CancellationToken cancellationToken = default);
}
