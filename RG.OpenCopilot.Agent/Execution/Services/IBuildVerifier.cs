using RG.OpenCopilot.Agent.Execution.Models;

namespace RG.OpenCopilot.Agent.Execution.Services;

/// <summary>
/// Service for running builds, detecting errors, and auto-fixing with LLM assistance
/// </summary>
public interface IBuildVerifier {
    Task<BuildResult> VerifyBuildAsync(string containerId, int maxRetries = 3, CancellationToken cancellationToken = default);
    Task<CommandResult> RunBuildAsync(string containerId, CancellationToken cancellationToken = default);
    Task<string?> DetectBuildToolAsync(string containerId, CancellationToken cancellationToken = default);
    Task<List<BuildError>> ParseBuildErrorsAsync(string output, string buildTool, CancellationToken cancellationToken = default);
    Task<List<CodeFix>> GenerateFixesAsync(List<BuildError> errors, CancellationToken cancellationToken = default);
}
