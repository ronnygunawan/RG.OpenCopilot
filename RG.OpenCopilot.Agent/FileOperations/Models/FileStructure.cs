namespace RG.OpenCopilot.Agent.FileOperations.Models;

/// <summary>
/// Represents the structure of a file including its classes, functions, and other elements
/// </summary>
public sealed class FileStructure {
    public string FilePath { get; init; } = "";
    public string Language { get; init; } = "";
    public List<string> Imports { get; init; } = [];
    public List<string> Classes { get; init; } = [];
    public List<string> Functions { get; init; } = [];
    public List<string> Namespaces { get; init; } = [];
}
