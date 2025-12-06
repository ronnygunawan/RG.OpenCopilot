namespace RG.OpenCopilot.Agent.Execution.Models;

public enum AgentTaskStatus {
    PendingPlanning,
    Planned,
    Executing,
    Completed,
    Blocked,
    Failed
}
