namespace RG.OpenCopilot.PRGenerationAgent.FileOperations.Models;

/// <summary>
/// Type of refactoring operation
/// </summary>
public enum RefactoringType {
    Rename,
    ExtractMethod,
    ExtractClass,
    MoveFile,
    InlineMethod,
    ChangeSignature,
    ExtractInterface,
    Custom
}
