using RG.OpenCopilot.Agent.Models;

namespace RG.OpenCopilot.Agent.Services;

public interface IPlannerService {
    Task<AgentPlan> CreatePlanAsync(AgentTaskContext context, CancellationToken cancellationToken = default);
}
