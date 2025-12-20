namespace RG.OpenCopilot.PRGenerationAgent.Configuration.Models;

/// <summary>
/// Container for all AI/LLM configurations
/// </summary>
public sealed class LlmConfigurations {
    /// <summary>
    /// Configuration for the Planner AI (used during planning phase)
    /// Ideally uses more powerful and expensive models
    /// </summary>
    public AiConfiguration Planner { get; init; } = new();

    /// <summary>
    /// Configuration for the Executor AI (used during execution phase)
    /// Ideally uses dumber and cheaper models
    /// </summary>
    public AiConfiguration Executor { get; init; } = new();

    /// <summary>
    /// Configuration for the Thinker AI (used in Research Agent)
    /// Ideally uses more powerful and general models
    /// </summary>
    public AiConfiguration Thinker { get; init; } = new();
}
