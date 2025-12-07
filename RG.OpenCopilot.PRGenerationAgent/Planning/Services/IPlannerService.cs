using RG.OpenCopilot.PRGenerationAgent.Planning.Models;

namespace RG.OpenCopilot.PRGenerationAgent.Planning.Services;

public interface IPlannerService {
    Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default);
}
