using RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Services;

public interface ICodeGenerator {
    Task<string> GenerateCodeAsync(LlmCodeGenerationRequest request, string? existingCode = null, CancellationToken cancellationToken = default);
    Task<string> GenerateClassAsync(string className, string description, string language, CancellationToken cancellationToken = default);
    Task<string> GenerateFunctionAsync(string functionName, string description, string language, CancellationToken cancellationToken = default);
    Task<bool> ValidateSyntaxAsync(string code, string language, CancellationToken cancellationToken = default);
}
