using System.Diagnostics;
using System.Text;
using RG.OpenCopilot.PRGenerationAgent.Execution.Models;
using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Services;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Executes commands in a subprocess
/// </summary>
public sealed class ProcessCommandExecutor : ICommandExecutor {
    private readonly ILogger<ProcessCommandExecutor> _logger;

    public ProcessCommandExecutor(ILogger<ProcessCommandExecutor> logger) {
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteCommandAsync(
        string workingDirectory,
        string command,
        string[] args,
        CancellationToken cancellationToken = default) {
        var startInfo = new ProcessStartInfo {
            FileName = command,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args) {
            startInfo.ArgumentList.Add(arg);
        }

        _logger.LogDebug("Executing: {Command} {Args} in {WorkingDirectory}",
            command, string.Join(" ", args), workingDirectory);

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => {
            if (e.Data != null) {
                outputBuilder.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) => {
            if (e.Data != null) {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var result = new CommandResult {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Error = errorBuilder.ToString()
        };

        if (!result.Success) {
            _logger.LogWarning("Command failed with exit code {ExitCode}: {Error}",
                result.ExitCode, result.Error);
        }

        return result;
    }
}
