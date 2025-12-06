using RG.OpenCopilot.Agent.CodeGeneration.Models;

namespace RG.OpenCopilot.Agent.CodeGeneration.Services;

public interface ITestGenerator {
    Task<string> GenerateTestsAsync(string containerId, string codeFilePath, string codeContent, string? testFramework = null, CancellationToken cancellationToken = default);
    Task<string?> DetectTestFrameworkAsync(string containerId, CancellationToken cancellationToken = default);
    Task<List<TestFile>> FindExistingTestsAsync(string containerId, CancellationToken cancellationToken = default);
    Task<TestPattern> AnalyzeTestPatternAsync(List<TestFile> existingTests, CancellationToken cancellationToken = default);
    Task<TestResult> RunTestsAsync(string containerId, string testFilePath, CancellationToken cancellationToken = default);
}
