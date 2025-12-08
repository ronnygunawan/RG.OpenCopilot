using RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Models;

namespace RG.OpenCopilot.PRGenerationAgent.CodeGeneration.Services;

/// <summary>
/// Service for generating and maintaining code documentation
/// </summary>
public interface IDocumentationGenerator {
    /// <summary>
    /// Generates inline documentation (XML docs, JSDoc, docstrings) for code
    /// </summary>
    /// <param name="code">The source code to document</param>
    /// <param name="language">The programming language</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Documented code with inline comments</returns>
    Task<DocumentedCode> GenerateInlineDocsAsync(string code, string language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates README file with new features and usage examples
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="changes">List of file changes made</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateReadmeAsync(string containerId, List<FileChange> changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates API documentation from code signatures and comments
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="format">Documentation format (Markdown, HTML, XML)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated API documentation</returns>
    Task<ApiDocumentation> GenerateApiDocsAsync(string containerId, ApiDocFormat format = ApiDocFormat.Markdown, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates CHANGELOG with version history and changes
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="version">Version number</param>
    /// <param name="changes">List of changelog entries</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateChangelogAsync(string containerId, string version, List<ChangelogEntry> changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates usage examples for public APIs
    /// </summary>
    /// <param name="containerId">Docker container ID</param>
    /// <param name="apiFilePath">Path to the API file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated usage examples</returns>
    Task<string> GenerateUsageExamplesAsync(string containerId, string apiFilePath, CancellationToken cancellationToken = default);
}
