# TestGenerator - LLM-Driven Test Generation

## Overview

The TestGenerator is a service that automatically generates comprehensive unit tests for code using Large Language Models (LLMs). It analyzes existing test patterns in the repository and generates tests that match the project's testing style and conventions.

## Features

- **Framework Detection**: Automatically detects test frameworks (xUnit, NUnit, MSTest, Jest, pytest, etc.)
- **Pattern Analysis**: Learns from existing tests to match naming conventions and assertion styles
- **Multi-Framework Support**: Works with .NET, JavaScript/TypeScript, and Python test frameworks
- **Test Execution**: Runs generated tests to verify they compile and pass
- **Comprehensive Coverage**: Generates tests for happy paths, edge cases, and error conditions

## Architecture

### Domain Models

#### TestFile
Represents a test file in the repository.

```csharp
public sealed class TestFile {
    public string Path { get; init; } = "";
    public string Content { get; init; } = "";
    public string Framework { get; init; } = "";
}
```

#### TestPattern
Captures testing conventions and patterns.

```csharp
public sealed class TestPattern {
    public string NamingConvention { get; init; } = "";
    public string AssertionStyle { get; init; } = "";
    public bool UsesArrangeActAssert { get; init; }
    public List<string> CommonImports { get; init; } = [];
    public string BaseTestClass { get; init; } = "";
}
```

#### TestResult
Contains test execution results.

```csharp
public sealed class TestResult {
    public bool Success { get; init; }
    public int TotalTests { get; init; }
    public int PassedTests { get; init; }
    public int FailedTests { get; init; }
    public List<string> Failures { get; init; } = [];
    public string Output { get; init; } = "";
}
```

### Service Interface

```csharp
public interface ITestGenerator {
    Task<string> GenerateTestsAsync(
        string containerId, 
        string codeFilePath, 
        string codeContent, 
        string? testFramework = null, 
        CancellationToken cancellationToken = default);
        
    Task<string?> DetectTestFrameworkAsync(
        string containerId, 
        CancellationToken cancellationToken = default);
        
    Task<List<TestFile>> FindExistingTestsAsync(
        string containerId, 
        CancellationToken cancellationToken = default);
        
    Task<TestPattern> AnalyzeTestPatternAsync(
        List<TestFile> existingTests, 
        CancellationToken cancellationToken = default);
        
    Task<TestResult> RunTestsAsync(
        string containerId, 
        string testFilePath, 
        CancellationToken cancellationToken = default);
}
```

## Usage

### Basic Test Generation

```csharp
var testGenerator = serviceProvider.GetRequiredService<ITestGenerator>();

var codeContent = """
    public class Calculator {
        public int Add(int a, int b) => a + b;
        public int Subtract(int a, int b) => a - b;
    }
    """;

var generatedTests = await testGenerator.GenerateTestsAsync(
    containerId: "my-container",
    codeFilePath: "Calculator.cs",
    codeContent: codeContent,
    testFramework: "xUnit");
```

### Automatic Framework Detection

```csharp
var testGenerator = serviceProvider.GetRequiredService<ITestGenerator>();

// Automatically detects the test framework
var framework = await testGenerator.DetectTestFrameworkAsync("my-container");
Console.WriteLine($"Detected framework: {framework}"); // e.g., "xUnit"
```

### Pattern Analysis

```csharp
var testGenerator = serviceProvider.GetRequiredService<ITestGenerator>();

// Find existing tests in the container
var existingTests = await testGenerator.FindExistingTestsAsync("my-container");

// Analyze patterns from existing tests
var pattern = await testGenerator.AnalyzeTestPatternAsync(existingTests);

Console.WriteLine($"Naming: {pattern.NamingConvention}");
Console.WriteLine($"Assertions: {pattern.AssertionStyle}");
Console.WriteLine($"Uses AAA: {pattern.UsesArrangeActAssert}");
```

### Running Generated Tests

```csharp
var testGenerator = serviceProvider.GetRequiredService<ITestGenerator>();

// Generate tests
var tests = await testGenerator.GenerateTestsAsync(...);

// Write tests to a file (not shown)
await File.WriteAllTextAsync("CalculatorTests.cs", tests);

// Run the tests
var result = await testGenerator.RunTestsAsync(
    containerId: "my-container",
    testFilePath: "CalculatorTests.cs");

Console.WriteLine($"Total: {result.TotalTests}");
Console.WriteLine($"Passed: {result.PassedTests}");
Console.WriteLine($"Failed: {result.FailedTests}");
```

## Supported Test Frameworks

### .NET
- **xUnit** - Uses `[Fact]` and `[Theory]` attributes
- **NUnit** - Uses `[Test]` attribute
- **MSTest** - Uses `[TestMethod]` attribute

### JavaScript/TypeScript
- **Jest** - Uses `describe()` and `test()`/`it()`
- **Mocha** - Uses `describe()` and `it()`

### Python
- **pytest** - Uses `test_*` function naming
- **unittest** - Uses `TestCase` classes

## Assertion Styles

The TestGenerator recognizes and matches various assertion styles:

- **Shouldly**: `result.ShouldBe(expected)`
- **FluentAssertions**: `result.Should().Be(expected)`
- **xUnit Assert**: `Assert.Equal(expected, result)`
- **Jest expect**: `expect(result).toBe(expected)`
- **pytest assert**: `assert result == expected`

## Test Naming Conventions

Common patterns detected and replicated:

- `MethodName_Scenario_ExpectedOutcome` (most common in C#)
- `MethodName_StateUnderTest_ExpectedBehavior`
- `test_method_name_scenario` (Python)
- `should_do_something_when_condition` (BDD style)

## Integration with LLM

The TestGenerator uses Microsoft Semantic Kernel to communicate with LLM services:

1. **System Prompt**: Provides framework-specific guidelines and best practices
2. **User Prompt**: Includes:
   - Code to test
   - Detected framework
   - Analyzed patterns
   - Test requirements
3. **Response Processing**: Extracts clean code from LLM response

## Configuration

Configure LLM settings in `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "your-api-key",
    "ModelId": "gpt-4"
  }
}
```

Or use Azure OpenAI:

```json
{
  "AzureOpenAI": {
    "ApiKey": "your-api-key",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4"
  }
}
```

## Best Practices

1. **Provide Context**: Include relevant code context for better test generation
2. **Review Generated Tests**: Always review and adjust generated tests as needed
3. **Run Tests**: Verify generated tests compile and pass before committing
4. **Customize Prompts**: Adjust prompts for project-specific requirements
5. **Iterate**: Use feedback from test runs to improve generation

## Limitations

- Requires an LLM API key (OpenAI or Azure OpenAI)
- Generated tests may need manual refinement
- Complex logic may require human oversight
- Container environment required for test execution

## Future Enhancements

- Support for more test frameworks (Jasmine, AVA, etc.)
- Better error message parsing
- Test coverage analysis
- Automatic test data generation
- Mock generation for dependencies
- Property-based testing support

## Examples

### xUnit Test Generation

```csharp
// Input code
public class UserService {
    public User? GetUser(int id) {
        if (id <= 0) throw new ArgumentException("Invalid ID");
        return _repository.Get(id);
    }
}

// Generated tests
using Xunit;
using Shouldly;

public class UserServiceTests {
    [Fact]
    public void GetUser_WithValidId_ReturnsUser() {
        // Arrange
        var service = new UserService();

        // Act
        var user = service.GetUser(1);

        // Assert
        user.ShouldNotBeNull();
    }

    [Fact]
    public void GetUser_WithInvalidId_ThrowsArgumentException() {
        // Arrange
        var service = new UserService();

        // Act & Assert
        Should.Throw<ArgumentException>(() => service.GetUser(0));
    }
}
```

## Related Documentation

- [LLM Configuration](LLM-CONFIGURATION.md)
- [Code Generator](CODE-GENERATOR.md)
- [Container Manager](EXECUTOR-SERVICE.md)
