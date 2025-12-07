using RG.OpenCopilot.PRGenerationAgent.Execution.Models;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

public interface ICommandExecutor {
    Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default);
}
