# GitHub Copilot Instructions for RG.OpenCopilot

## Project Overview

RG.OpenCopilot is a C#/.NET 10 solution that provides a GitHub Enterprise–hosted coding agent similar to GitHub Copilot Coding Agent. The agent is triggered by labeling issues with `copilot-assisted` and uses LLM models to analyze issues, create plans, and execute code changes.

## Solution Structure

- **RG.OpenCopilot.slnx** – root solution file
- **RG.OpenCopilot.App** – ASP.NET Core minimal API for GitHub App webhooks and health checks
- **RG.OpenCopilot.Agent** – shared domain models and planner/executor abstractions
- **RG.OpenCopilot.Runner** – console app to run the agent locally for testing
- **RG.OpenCopilot.Tests** – xUnit tests using Shouldly assertions

All projects target `.NET 10.0` with nullable reference types and implicit usings enabled.

## Build and Test Commands

### Build
```bash
dotnet build RG.OpenCopilot.slnx --configuration Release
```

### Test
```bash
dotnet test RG.OpenCopilot.slnx --configuration Release --no-build --verbosity normal
```

### Run Web App
```bash
dotnet run --project RG.OpenCopilot.App
```

## Coding Conventions

### General Style
- **Indentation**: 4 spaces (no tabs)
- **Line endings**: CRLF
- **Charset**: UTF-8
- **Trailing whitespace**: Remove
- **Final newline**: Always include

### C# Specific Conventions
- Use `sealed` classes for domain models that shouldn't be inherited
- Use `init` accessors for immutable properties
- Initialize collections with `= []` syntax
- Use nullable reference types appropriately (`?` for nullable)
- Use file-scoped namespaces (no braces)
- **Use K&R brace style** - opening braces on the same line as the declaration
- Sort `using` directives with System namespaces first
- Don't use `this.` qualifier unless necessary
- Use `""` instead of `string.Empty`
- **Use named arguments** when calling methods if the meaning of the argument is not immediately obvious
  - Example: `SaveFile(fileName: path, overwrite: true)` or `CreateTask(timeout: 30, retryCount: 3)`
  - Instead of: `SaveFile(path, true)` or `CreateTask(30, 3)`

### Naming Conventions
- Interfaces: Prefix with `I` (e.g., `IPlannerService`, `IExecutorService`)
- Private fields: _camelCase with underscore prefix
- Properties: PascalCase
- Methods: PascalCase
- Parameters: camelCase
- Local variables: camelCase

## Testing Practices

### Testing Framework
- Use **xUnit** for all tests
- Use **Shouldly** assertions (not FluentAssertions or standard Assert)
- Test class names should end with `Tests` (e.g., `AgentPlanTests`)

### Test Structure
```csharp
public class FeatureTests {
    [Fact]
    public void MethodName_Scenario_ExpectedOutcome() {
        // Arrange
        var sut = new Feature();

        // Act
        var result = sut.Method();

        // Assert
        result.ShouldBe(expectedValue);
    }
}
```

### Shouldly Assertion Examples
- `result.ShouldBe(expected)`
- `list.ShouldBeEmpty()`
- `value.ShouldNotBeNull()`
- `string.ShouldBe(expected)` (for string comparisons)

## Architecture Patterns

### Domain Models
- Located in `RG.OpenCopilot.Agent/AgentModels.cs`
- Use `sealed` classes for concrete types
- Use `init` accessors for immutable properties
- Initialize collections in property initializers

### Services and Abstractions
- Define interfaces in `RG.OpenCopilot.Agent`
- Use `async`/`await` for all I/O operations
- Accept `CancellationToken` with default value in async methods
- Return `Task<T>` or `Task` from async methods

### Key Interfaces
- `IPlannerService` – creates structured plans using premium LLM models
- `IExecutorService` – executes plans and makes code changes
- Both interfaces are defined in `AgentModels.cs`

## LLM Integration

### Configuration
LLM settings are in `appsettings.json` or environment variables:
- Supported providers: OpenAI, Azure OpenAI
- Uses Microsoft Semantic Kernel for LLM integration
- Fallback to `SimplePlannerService` when no API key is configured

### Custom Instructions Location
- Issue-specific: `.github/open-copilot/{issueNumber}.md`
- General instructions: `.github/open-copilot/instructions.md`
- Fallback: `.github/open-copilot/README.md`

See `LLM-CONFIGURATION.md` for detailed LLM setup instructions.

## Documentation

- Keep README.md updated with current project status
- Document significant architectural decisions in PLAN.md
- Include setup instructions for new features
- Use markdown for all documentation
- Add XML documentation comments for public APIs

## API Conventions

### ASP.NET Core
- Use minimal APIs pattern
- Health check endpoint at `/health` returns plain text "ok"
- GitHub webhook endpoint at `/github/webhook`
- Validate GitHub webhook signatures (HMAC-SHA256)

## GitHub Integration

### Webhook Handling
- Trigger: Issues labeled with `copilot-assisted`
- Validates GitHub signatures before processing
- Creates `AgentTask` for each issue
- Creates WIP PR with plan

### PR Lifecycle
1. Create WIP PR with `[WIP]` prefix
2. Update PR description with plan and checklist
3. Remove `[WIP]` prefix when complete
4. Archive WIP details in collapsed `<details>` section

## Error Handling

- Use appropriate exception types
- Include meaningful error messages
- Don't swallow exceptions without logging
- Validate inputs early

## Security

- Never commit API keys or secrets
- Use environment variables for sensitive configuration
- Validate all external inputs (especially webhooks)
- Use HMAC-SHA256 for webhook signature validation

## Additional Resources

- **POC-SETUP.md** – Setup and testing instructions
- **LLM-CONFIGURATION.md** – LLM provider configuration
- **PLAN.md** – Architecture and implementation plan
- **COVERAGE.md** – Test coverage information

## Common Patterns to Follow

### Async Method Pattern
```csharp
public async Task<AgentPlan> CreatePlanAsync(
    AgentTaskContext context,
    CancellationToken cancellationToken = default) {
    // Implementation
}
```

### Sealed Class Pattern
```csharp
public sealed class AgentPlan {
    public string ProblemSummary { get; init; } = "";
    public List<string> Constraints { get; init; } = [];
}
```

### Test Method Pattern
```csharp
[Fact]
public void FeatureName_Condition_ExpectedResult() {
    var result = feature.Execute();
    result.ShouldBe(expected);
}
```

## What to Avoid

- Don't use `FluentAssertions` (use Shouldly instead)
- Don't use tabs for indentation
- Don't omit braces for single-line statements
- Don't use `var` when the type isn't obvious
- Don't create mutable domain models (use `init` accessors)
- Don't commit build artifacts (bin/, obj/ directories)
- Don't add unnecessary using directives

## When Making Changes

1. Run build to ensure no compilation errors
2. Run tests to ensure no regressions
3. Add tests for new functionality
4. Update documentation if changing public APIs
5. Follow existing code patterns and conventions
6. Use appropriate logging for debugging
7. Validate webhook inputs when handling GitHub events
8. **Update this instructions file** (`.github/copilot-instructions.md`) whenever coding conventions or architectural patterns change to keep Copilot aligned with the current codebase standards
