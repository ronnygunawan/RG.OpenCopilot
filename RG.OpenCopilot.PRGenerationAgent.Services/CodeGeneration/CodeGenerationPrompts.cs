namespace RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;

/// <summary>
/// Curated prompts for code generation, inspired by best practices from the community
/// </summary>
public static class CodeGenerationPrompts {
    /// <summary>
    /// General best practices for code generation (curated from industry standards)
    /// </summary>
    public static string GetBestPractices() {
        return """
            # Best Practices for Code Generation
            
            ## Code Quality
            - Write clean, readable, and maintainable code
            - Follow DRY (Don't Repeat Yourself) principle
            - Keep functions/methods small and focused on a single responsibility
            - Use meaningful and descriptive names for variables, functions, and classes
            - Avoid magic numbers and strings - use named constants
            
            ## Error Handling
            - Always handle potential errors and edge cases
            - Use appropriate exception handling mechanisms
            - Validate inputs before processing
            - Provide meaningful error messages
            - Fail fast and fail gracefully
            
            ## Code Structure
            - Follow SOLID principles (Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion)
            - Keep related code together (high cohesion, low coupling)
            - Use appropriate design patterns when beneficial
            - Organize code logically with clear hierarchy
            
            ## Documentation
            - Add comments for complex logic, not obvious code
            - Use XML documentation comments for public APIs (C#, Java)
            - Include JSDoc comments for JavaScript/TypeScript
            - Use docstrings for Python
            - Keep comments up-to-date with code changes
            
            ## Testing & Maintainability
            - Write testable code
            - Avoid tight coupling to make testing easier
            - Use dependency injection where appropriate
            - Consider future maintenance and extensibility
            
            ## Security
            - Never trust user input - always validate and sanitize
            - Avoid SQL injection by using parameterized queries
            - Prevent XSS attacks by escaping output
            - Use secure random number generators for security-sensitive operations
            - Never hardcode sensitive data (passwords, API keys, secrets)
            
            ## Performance
            - Avoid premature optimization, but be aware of common performance pitfalls
            - Use appropriate data structures for the task
            - Consider memory usage and avoid unnecessary allocations
            - Use async/await for I/O-bound operations
            
            ## Modern Practices
            - Use modern language features appropriately
            - Prefer immutability where possible
            - Use functional programming concepts when beneficial
            - Follow the language's idioms and conventions
            """;
    }

    /// <summary>
    /// Get technology-specific prompts based on the programming language
    /// </summary>
    public static string GetTechnologyPrompt(string language) {
        return language.ToLowerInvariant() switch {
            "c#" or "csharp" => GetCSharpPrompt(),
            "javascript" or "js" => GetJavaScriptPrompt(),
            "typescript" or "ts" => GetTypeScriptPrompt(),
            "python" or "py" => GetPythonPrompt(),
            "java" => GetJavaPrompt(),
            "go" => GetGoPrompt(),
            "rust" => GetRustPrompt(),
            _ => GetGenericPrompt()
        };
    }

    private static string GetCSharpPrompt() {
        return """
            # C# Specific Guidelines
            
            - Follow Microsoft's C# Coding Conventions
            - Use PascalCase for public members, camelCase with underscore prefix (_) for private fields
            - Use nullable reference types (`?`) appropriately
            - Prefer `var` when the type is obvious from the right side
            - Use collection expressions `[]` for .NET 8+ (C# 12)
            - Use `init` accessors for immutable properties
            - Use `record` types for DTOs and value objects
            - Use `async`/`await` for asynchronous operations
            - Implement `IDisposable` or use `using` statements for resource management
            - Use LINQ for collection operations when appropriate
            - Follow async naming convention: methods returning Task should end with 'Async'
            - Use XML documentation comments (///) for public APIs
            - Use raw string literals (triple quotes) for multi-line strings
            - Prefer sealed classes when inheritance is not intended
            """;
    }

    private static string GetJavaScriptPrompt() {
        return """
            # JavaScript Specific Guidelines
            
            - Use modern ES6+ syntax (const/let, arrow functions, destructuring, template literals)
            - Prefer `const` over `let`, avoid `var`
            - Use arrow functions for callbacks and functional operations
            - Use async/await instead of Promise chains where possible
            - Handle promises properly with try/catch in async functions
            - Use strict equality (===) instead of loose equality (==)
            - Avoid global variables
            - Use JSDoc comments for documentation
            - Follow the Airbnb JavaScript Style Guide principles
            - Use destructuring for objects and arrays
            - Use spread operator for copying and merging
            - Handle null/undefined appropriately
            - Use optional chaining (?.) and nullish coalescing (??)
            """;
    }

    private static string GetTypeScriptPrompt() {
        return """
            # TypeScript Specific Guidelines
            
            - Use strong typing - avoid `any`, prefer `unknown` or specific types
            - Define interfaces for object shapes and contracts
            - Use type aliases for complex or reusable types
            - Leverage union and intersection types appropriately
            - Use generics for reusable, type-safe code
            - Enable strict mode in tsconfig.json
            - Use enums for fixed sets of values
            - Use type guards for runtime type checking
            - Prefer interfaces over type aliases for object shapes
            - Use readonly for immutable properties
            - Use const assertions for literal types
            - Document public APIs with JSDoc comments including type information
            - Use discriminated unions for type-safe state management
            """;
    }

    private static string GetPythonPrompt() {
        return """
            # Python Specific Guidelines
            
            - Follow PEP 8 Style Guide
            - Use snake_case for functions and variables, PascalCase for classes
            - Use type hints for function parameters and return values (PEP 484)
            - Use docstrings (triple quotes) for all public modules, functions, classes, and methods
            - Prefer list comprehensions and generator expressions over loops where readable
            - Use context managers (with statement) for resource management
            - Use f-strings for string formatting (Python 3.6+)
            - Handle exceptions with specific exception types, not bare except
            - Use dataclasses for data-holding classes (Python 3.7+)
            - Follow the Zen of Python principles (import this)
            - Use `pathlib` for file path operations
            - Prefer `is` for None comparisons, `==` for value comparisons
            - Use `enumerate()` when you need both index and value
            - Use `zip()` for parallel iteration
            """;
    }

    private static string GetJavaPrompt() {
        return """
            # Java Specific Guidelines
            
            - Follow Java Code Conventions and Google Java Style Guide
            - Use PascalCase for classes, camelCase for methods and variables
            - Use UPPER_SNAKE_CASE for constants
            - Use interfaces to define contracts
            - Prefer composition over inheritance
            - Use try-with-resources for AutoCloseable resources
            - Use Stream API for collection operations (Java 8+)
            - Use Optional to represent potentially absent values
            - Use meaningful names and avoid abbreviations
            - Follow JavaDoc conventions for documentation
            - Use @Override annotation consistently
            - Minimize mutability - use final for variables and classes where appropriate
            - Use lombok annotations to reduce boilerplate (if available)
            """;
    }

    private static string GetGoPrompt() {
        return """
            # Go Specific Guidelines
            
            - Follow Effective Go and Go Code Review Comments
            - Use gofmt for code formatting
            - Use short, concise names (i for index, err for error, etc.)
            - Handle errors explicitly - don't ignore them
            - Use defer for cleanup operations
            - Use goroutines and channels for concurrency
            - Prefer composition over inheritance
            - Use interfaces to define behavior
            - Use the zero value as the default
            - Keep packages small and focused
            - Document exported identifiers with comments
            - Use table-driven tests
            - Avoid global state
            """;
    }

    private static string GetRustPrompt() {
        return """
            # Rust Specific Guidelines
            
            - Follow Rust API Guidelines and The Rust Book
            - Use snake_case for functions and variables, PascalCase for types
            - Handle errors with Result type, use ? operator for propagation
            - Use pattern matching extensively
            - Prefer borrowing over cloning
            - Use iterators and closures for functional operations
            - Use &str for string slices, String for owned strings
            - Use Option for potentially absent values
            - Write documentation comments with ///
            - Use cargo fmt for formatting, cargo clippy for lints
            - Implement appropriate traits (Debug, Clone, etc.)
            - Use lifetimes explicitly when needed
            - Prefer compile-time safety over runtime checks
            """;
    }

    private static string GetGenericPrompt() {
        return """
            # Generic Language Guidelines
            
            - Follow the language's standard conventions and idioms
            - Write clear, readable code
            - Handle errors appropriately for the language
            - Use the language's built-in features and standard library
            - Document public APIs
            - Write code that's easy to test and maintain
            """;
    }
}
