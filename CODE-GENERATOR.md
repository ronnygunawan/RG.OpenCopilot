# CodeGenerator - LLM-Driven Code Generation

## Overview

The `CodeGenerator` service provides LLM-powered code generation capabilities for the RG.OpenCopilot agent. It can generate high-quality, production-ready code in multiple programming languages based on natural language descriptions.

## Features

- **Multi-language Support**: Generate code in C#, JavaScript, TypeScript, Python, and more
- **Context-Aware Generation**: Incorporate existing code, dependencies, and contextual information
- **Code Extraction**: Automatically clean LLM responses by removing markdown formatting
- **Syntax Validation**: Basic syntax checking for generated code
- **Style Preservation**: When modifying existing code, maintains the original code style
- **Convenience Methods**: Simplified APIs for common tasks (class/function generation)

## Usage

### Basic Code Generation

```csharp
var request = new LlmCodeGenerationRequest {
    Description = "Create a User class with Id, Name, and Email properties",
    Language = "C#",
    FilePath = "Models/User.cs"
};

var generatedCode = await codeGenerator.GenerateCodeAsync(request);
```

### Modifying Existing Code

```csharp
var existingCode = """
    public class Calculator {
        public int Add(int a, int b) => a + b;
    }
    """;

var request = new LlmCodeGenerationRequest {
    Description = "Add Multiply and Divide methods",
    Language = "C#",
    FilePath = "Calculator.cs"
};

var updatedCode = await codeGenerator.GenerateCodeAsync(request, existingCode);
```

### Generating a Class

```csharp
var code = await codeGenerator.GenerateClassAsync(
    className: "Product",
    description: "A product entity with Id, Name, Price properties and validation",
    language: "C#"
);
```

### Generating a Function

```csharp
var code = await codeGenerator.GenerateFunctionAsync(
    functionName: "validateEmail",
    description: "Validate email address using regex",
    language: "JavaScript"
);
```

### With Dependencies and Context

```csharp
var request = new LlmCodeGenerationRequest {
    Description = "Create a repository class for database operations",
    Language = "C#",
    FilePath = "Data/UserRepository.cs",
    Dependencies = [
        "Microsoft.EntityFrameworkCore",
        "System.Linq"
    ],
    Context = new Dictionary<string, string> {
        { "Database", "PostgreSQL" },
        { "ORM", "Entity Framework Core 8.0" }
    }
};

var code = await codeGenerator.GenerateCodeAsync(request);
```

### Syntax Validation

```csharp
var isValid = await codeGenerator.ValidateSyntaxAsync(generatedCode, "C#");
if (!isValid) {
    // Handle invalid syntax
}
```

## Architecture

### Components

1. **ICodeGenerator Interface** (`RG.OpenCopilot.Agent/CodeGeneration/Services`)
   - Defines the public API for code generation services

2. **CodeGenerator Implementation** (`RG.OpenCopilot.App/CodeGeneration`)
   - Uses Microsoft Semantic Kernel for LLM integration
   - Implements prompt engineering for effective code generation
   - Handles response extraction and cleanup

3. **Domain Models** (`RG.OpenCopilot.Agent/CodeGeneration/Models`)
   - `LlmCodeGenerationRequest`: Input model for generation requests
   - `GeneratedCode`: Output model with code and metadata

### Dependency Injection

The `CodeGenerator` is registered as a singleton in the DI container:

```csharp
builder.Services.AddSingleton<ICodeGenerator, CodeGenerator>();
```

## Prompt Engineering

### Enhanced Multi-Source System Prompt

The CodeGenerator uses an advanced, multi-source system prompt that combines:

1. **General Best Practices** - Curated industry standards covering:
   - Code quality (DRY, SOLID, clean code)
   - Error handling and validation
   - Security practices (input validation, SQL injection prevention, XSS protection)
   - Performance considerations
   - Testing and maintainability
   - Modern programming practices

2. **Technology-Specific Prompts** - Language-specific guidelines based on the target language:
   - **C#**: Microsoft conventions, nullable types, async/await, LINQ, XML docs
   - **JavaScript**: ES6+ syntax, const/let, arrow functions, JSDoc
   - **TypeScript**: Strong typing, interfaces, generics, type guards
   - **Python**: PEP 8, type hints, docstrings, comprehensions
   - **Java**: Java conventions, streams, Optional, JavaDoc
   - **Go**: Effective Go, error handling, goroutines, defer
   - **Rust**: Ownership, borrowing, Result/Option, traits

3. **Repository-Specific Instructions** - Loaded from `copilot-instructions.md` in the repository:
   - Project-specific architecture patterns
   - Naming conventions
   - Testing requirements
   - Security guidelines
   - Any custom rules for your codebase

To use repository-specific instructions, include repository context in your request:

```csharp
var request = new LlmCodeGenerationRequest {
    Description = "Create a service class",
    Language = "C#",
    Context = new Dictionary<string, string> {
        { "RepositoryOwner", "your-org" },
        { "RepositoryName", "your-repo" }
    }
};
```

The CodeGenerator will automatically load and cache the `copilot-instructions.md` file from the repository root.

### System Prompt Components

The final system prompt includes all these elements in order:

- Write clean, maintainable code following best practices
- Include appropriate error handling and validation
- Add clear comments for complex logic
- Match the style of existing code when modifying
- Use modern language features appropriately
- Return only code without markdown formatting

### User Prompt Structure

User prompts are structured to include:

1. **Task Header**: Clearly states the language and objective
2. **Requirements**: Detailed description of what to generate
3. **File Path**: Target file location (if applicable)
4. **Dependencies**: Required libraries or packages
5. **Additional Context**: Database info, frameworks, conventions
6. **Existing Code**: Code to modify (if applicable)

Example prompt structure:

```
# Task: Generate C# Code

## Requirements
Create a User class with Id, Name, and Email properties

## Target File Path
Models/User.cs

## Dependencies
- System.ComponentModel.DataAnnotations

## Additional Context
**Framework:** ASP.NET Core 8.0
**Validation:** Use data annotations

## Existing Code to Modify
[existing code here if modifying]
```

## Configuration

### LLM Settings

The CodeGenerator uses the following LLM configuration:

- **Temperature**: 0.2 (low for more deterministic code generation)
- **Max Tokens**: 4000 (enough for most code generation tasks)
- **Model**: Configurable via `LLM:ModelId` in appsettings.json

### Supported Languages

- C# / CSharp
- JavaScript / JS
- TypeScript / TS
- Python / Py
- Any other language (uses generic validation)

## Syntax Validation

The CodeGenerator includes basic syntax validation for generated code:

### C# Validation
- Balanced braces `{}`
- Balanced parentheses `()`
- Balanced brackets `[]`

### JavaScript/TypeScript Validation
- Balanced braces `{}`
- Balanced parentheses `()`

### Python Validation
- Balanced parentheses `()`
- Balanced brackets `[]`

**Note**: Validation is basic and doesn't replace proper compilation or linting tools.

## Best Practices

### Writing Effective Descriptions

1. **Be Specific**: Clearly describe what you want
   ```csharp
   // Good
   Description = "Create a User class with Id (int), Name (string), Email (string), and CreatedAt (DateTime) properties. Add validation attributes."
   
   // Less specific
   Description = "Create a User class"
   ```

2. **Include Constraints**: Mention specific requirements
   ```csharp
   Description = "Create an async method that retrieves users from database. Include error handling for null results. Use Entity Framework Core."
   ```

3. **Specify Patterns**: If you want specific patterns
   ```csharp
   Description = "Create a repository using the repository pattern with dependency injection"
   ```

### Using Context Effectively

Provide relevant context that helps the LLM generate better code:

```csharp
Context = new Dictionary<string, string> {
    { "Framework", "ASP.NET Core 8.0" },
    { "Database", "PostgreSQL" },
    { "Architecture Pattern", "Clean Architecture" },
    { "Conventions", "Use async/await, prefer records for DTOs" }
}
```

### Modifying Existing Code

When modifying code:
1. Always provide the complete existing code
2. Be clear about what needs to change
3. The LLM will preserve the existing style and structure

## Testing

### Unit Tests

The CodeGenerator has comprehensive unit tests covering:
- Basic code generation
- Markdown extraction
- Syntax validation
- Prompt building
- Error handling
- Logging

Run unit tests:
```bash
dotnet test --filter "FullyQualifiedName~CodeGeneratorTests"
```

### Integration Tests

Integration tests use real LLM models and require an API key:

```bash
# Set environment variable
export LLM__ApiKey="your-api-key"

# Run integration tests
dotnet test --filter "FullyQualifiedName~CodeGeneratorIntegrationTests"
```

Integration tests verify:
- Real code generation in multiple languages
- Code quality and correctness
- Syntax validity
- Style preservation
- Complex scenarios

## Limitations

1. **Syntax Validation**: Only provides basic bracket/brace matching, not full compilation
2. **Token Limits**: Large code generation requests may hit token limits
3. **Language Support**: While it supports many languages, validation is limited to specific languages
4. **LLM Dependency**: Quality depends on the configured LLM model
5. **Cost**: Each generation request consumes LLM tokens (costs money)

## Performance Considerations

- **Caching**: Consider caching similar requests to reduce LLM calls
- **Batch Operations**: For multiple related generations, consider combining into one request
- **Temperature**: Using lower temperature (0.2) provides more consistent results but less creative solutions
- **Token Usage**: Monitor token usage to control costs

## Error Handling

The CodeGenerator throws exceptions for:
- LLM service failures
- Invalid configurations
- Timeout issues

Always wrap calls in try-catch blocks:

```csharp
try {
    var code = await codeGenerator.GenerateCodeAsync(request);
}
catch (InvalidOperationException ex) {
    // Handle LLM service errors
    logger.LogError(ex, "Failed to generate code");
}
```

## Future Enhancements

Potential improvements for the CodeGenerator:

1. **Advanced Syntax Validation**: Integration with Roslyn for C# or language-specific parsers
2. **Style Guide Integration**: Automatically apply project-specific style guides
3. **Code Review**: Integration with static analysis tools
4. **Iterative Refinement**: Allow multiple passes to improve code quality
5. **Template Support**: Predefined templates for common patterns
6. **Multi-file Generation**: Generate multiple related files in one request
7. **Test Generation**: Automatically generate unit tests for generated code

## Examples

### Example 1: REST API Controller

```csharp
var request = new LlmCodeGenerationRequest {
    Description = """
        Create a REST API controller for managing users with the following endpoints:
        - GET /api/users - List all users
        - GET /api/users/{id} - Get user by ID
        - POST /api/users - Create new user
        - PUT /api/users/{id} - Update user
        - DELETE /api/users/{id} - Delete user
        
        Include:
        - Dependency injection for IUserService
        - XML documentation
        - Proper HTTP status codes
        - Model validation
        """,
    Language = "C#",
    FilePath = "Controllers/UsersController.cs",
    Dependencies = [
        "Microsoft.AspNetCore.Mvc",
        "System.ComponentModel.DataAnnotations"
    ],
    Context = new Dictionary<string, string> {
        { "Framework", "ASP.NET Core 8.0" },
        { "API Style", "RESTful" }
    }
};

var code = await codeGenerator.GenerateCodeAsync(request);
```

### Example 2: React Component

```csharp
var request = new LlmCodeGenerationRequest {
    Description = """
        Create a React component for displaying a user profile card.
        
        Props:
        - user: { name: string, email: string, avatar: string, role: string }
        - onEdit: () => void
        - onDelete: () => void
        
        Features:
        - Display user avatar, name, email, and role
        - Show edit and delete buttons
        - Use TypeScript
        - Use functional component with hooks
        """,
    Language = "TypeScript",
    FilePath = "components/UserProfileCard.tsx",
    Dependencies = ["react"]
};

var code = await codeGenerator.GenerateCodeAsync(request);
```

### Example 3: Python Data Processing

```csharp
var request = new LlmCodeGenerationRequest {
    Description = """
        Create a class for processing CSV data with methods:
        - load_csv(file_path): Load CSV into pandas DataFrame
        - clean_data(): Remove duplicates and handle missing values
        - transform_data(): Apply transformations (lowercase strings, normalize numbers)
        - save_csv(file_path): Save processed data to CSV
        
        Include error handling and logging.
        """,
    Language = "Python",
    FilePath = "data/csv_processor.py",
    Dependencies = ["pandas", "logging"]
};

var code = await codeGenerator.GenerateCodeAsync(request);
```

## Troubleshooting

### Common Issues

**Issue**: Generated code has markdown formatting
- **Solution**: The `ExtractCode` method should handle this automatically. If not, check the system prompt.

**Issue**: Code doesn't match project style
- **Solution**: Provide existing code examples in the Context dictionary or as existingCode parameter.

**Issue**: Syntax validation fails
- **Solution**: Check for unbalanced brackets/braces. For advanced validation, use language-specific tools.

**Issue**: LLM returns incomplete code
- **Solution**: Increase MaxTokens in the execution settings or simplify the request.

## Support

For issues or questions:
- Check the unit tests for usage examples
- Review the integration tests for real-world scenarios
- Examine the source code for implementation details

## License

This component is part of RG.OpenCopilot and follows the same license terms.
