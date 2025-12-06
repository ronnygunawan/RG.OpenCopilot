namespace RG.OpenCopilot.Agent.CodeGeneration.Models;

public sealed class TestPattern {
    public string NamingConvention { get; init; } = "";
    public string AssertionStyle { get; init; } = "";
    public bool UsesArrangeActAssert { get; init; }
    public List<string> CommonImports { get; init; } = [];
    public string BaseTestClass { get; init; } = "";
}
