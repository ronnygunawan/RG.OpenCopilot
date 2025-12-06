using RG.OpenCopilot.Agent.Planning.Models;

namespace RG.OpenCopilot.Agent.Planning.Services;

public interface IPlannerService {
    Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default);
}
