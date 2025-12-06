namespace RG.OpenCopilot.App.Infrastructure;

public interface ICommandExecutor {
    Task<CommandResult> ExecuteCommandAsync(string workingDirectory, string command, string[] args, CancellationToken cancellationToken = default);
}
