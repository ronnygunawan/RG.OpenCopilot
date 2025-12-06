# Copilot Instructions for RG.OpenCopilot

## Project-Specific Coding Guidelines

### Architecture Patterns
- Follow feature-based organization: group files by domain/feature (Planning, Execution, FileOperations, CodeGeneration)
- Use dependency injection for all services
- Place domain models and interfaces in `RG.OpenCopilot.Agent` project
- Place implementations in `RG.OpenCopilot.App` project
- Use `sealed` classes for concrete implementations that shouldn't be inherited

### Naming Conventions
- Interfaces: Prefix with `I` (e.g., `ICodeGenerator`, `IPlannerService`)
- Private fields: Use `_camelCase` with underscore prefix
- Use PascalCase for public members, properties, methods
- Use camelCase for parameters and local variables

### Code Style
- Use K&R brace style (opening braces on the same line)
- Indentation: 4 spaces (no tabs)
- Use file-scoped namespaces (no braces around namespace)
- Use `init` accessors for immutable properties
- Initialize collections with `= []` syntax (C# 12)
- Use raw string literals (triple quotes `"""`) for multi-line strings
- Use named arguments when calling methods if the meaning isn't immediately obvious

### Async Patterns
- All async methods should end with `Async` suffix
- Accept `CancellationToken cancellationToken = default` as the last parameter
- Use `async`/`await` for I/O operations
- Return `Task<T>` or `Task` from async methods

### Testing
- Use **xUnit** for all tests
- Use **Shouldly** assertions (not FluentAssertions or standard Assert)
- Test class names should end with `Tests`
- Test method pattern: `MethodName_Scenario_ExpectedOutcome`
- Use **Moq** for mocking dependencies
- Each test class should have its own `TestLogger<T>` implementation
- Always assert exception messages when testing for exceptions
- **Comprehensive Test Coverage - Test ALL code paths:**
  - **Happy path tests**: Test all operations with valid inputs and expected success scenarios
  - **Negative case handling**: Test all error paths, validation failures, null inputs, and exceptional conditions
  - **NEVER use "edge case" terminology** - Every code path is either a happy path or negative case that must be tested
  - Achieve 90%+ code coverage on all new code
  - Test every branch, including all if/else statements, early returns, and exception throws
  - Example: If a method can throw 3 different exceptions, write 3 tests for those paths

### Error Handling
- Use appropriate exception types
- Include meaningful error messages
- Log errors with appropriate log levels
- Don't swallow exceptions without logging

### Documentation
- Use XML documentation comments (`///`) for public APIs
- Keep README.md and documentation files up-to-date
- Document significant architectural decisions

### Platform Support and Path Handling
- **Windows and Linux hosts are supported** - The application can run on both Windows and Linux
- **Container paths are always Linux paths** - The executor always uses Linux containers, regardless of host OS
- **Important path handling rules:**
  - Container paths (e.g., `/workspace/src/file.cs`) always use forward slashes (`/`)
  - Use `CombineContainerPath()` helper method for combining container paths, not `Path.Combine()`
  - Never use `Path.GetFullPath()` or `Path.GetDirectoryName()` on container paths
  - Host paths (e.g., temp directories, working directories) should use `Path.Combine()` and standard .NET Path APIs
  - Always validate container paths to prevent directory traversal attacks
- **Example:**
  ```csharp
  // ✅ CORRECT - For container paths
  var containerPath = CombineContainerPath("/workspace", "src/MyClass.cs");
  
  // ❌ WRONG - Don't use Path.Combine for container paths
  var wrongPath = Path.Combine("/workspace", "src/MyClass.cs"); // Returns "\workspace\src\MyClass.cs" on Windows
  
  // ✅ CORRECT - For host paths
  var hostPath = Path.Combine(Path.GetTempPath(), "myfile.txt");
  ```

### LLM Integration
- Use Microsoft Semantic Kernel for LLM integration
- Use temperature 0.2-0.3 for deterministic tasks like code generation and planning
- Use `GetChatMessageContentAsync` for single responses
- Always include proper error handling and fallback mechanisms

### Security
- Never commit API keys or secrets
- Use environment variables for sensitive configuration
- Validate all external inputs
- Use HMAC-SHA256 for webhook signature validation

### Global Usings
- Add new feature namespaces to GlobalUsings.cs files in respective projects
- Keep global usings organized by project dependency order
