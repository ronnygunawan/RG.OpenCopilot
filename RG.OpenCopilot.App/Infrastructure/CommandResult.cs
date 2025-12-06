namespace RG.OpenCopilot.App.Infrastructure;

public sealed class CommandResult {
    public int ExitCode { get; init; }
    public string Output { get; init; } = "";
    public string Error { get; init; } = "";
    public bool Success => ExitCode == 0;
}
