using RG.OpenCopilot.PRGenerationAgent.Execution.Models;

namespace RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

public interface ICommandExecutor {
    Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default);
}
