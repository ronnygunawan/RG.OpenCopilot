namespace RG.OpenCopilot.PRGenerationAgent.Execution.Models;

public enum AgentTaskStatus {
    PendingPlanning,
    Planned,
    Executing,
    Completed,
    Blocked,
    Failed
}
