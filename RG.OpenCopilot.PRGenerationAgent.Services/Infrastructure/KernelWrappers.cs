using Microsoft.SemanticKernel;

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

/// <summary>
/// Wrapper for Planner AI Kernel
/// </summary>
public sealed class PlannerKernel {
    public Kernel Kernel { get; }

    public PlannerKernel(Kernel kernel) {
        Kernel = kernel;
    }
}

/// <summary>
/// Wrapper for Executor AI Kernel
/// </summary>
public sealed class ExecutorKernel {
    public Kernel Kernel { get; }

    public ExecutorKernel(Kernel kernel) {
        Kernel = kernel;
    }
}

/// <summary>
/// Wrapper for Thinker AI Kernel
/// </summary>
public sealed class ThinkerKernel {
    public Kernel Kernel { get; }

    public ThinkerKernel(Kernel kernel) {
        Kernel = kernel;
    }
}
