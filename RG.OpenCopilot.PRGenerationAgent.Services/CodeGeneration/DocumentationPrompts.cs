namespace RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;

/// <summary>
/// Prompt templates for documentation generation
/// </summary>
internal static class DocumentationPrompts {
    public static string GetInlineDocumentationPrompt(string language) {
        return language.ToLowerInvariant() switch {
            "c#" or "csharp" => GetCSharpDocPrompt(),
            "javascript" or "js" => GetJavaScriptDocPrompt(),
            "typescript" or "ts" => GetTypeScriptDocPrompt(),
            "python" or "py" => GetPythonDocPrompt(),
            "java" => GetJavaDocPrompt(),
            "go" => GetGoDocPrompt(),
            _ => GetGenericDocPrompt()
        };
    }

    private static string GetCSharpDocPrompt() {
        return """
            Generate XML documentation comments for C# code following these guidelines:
            - Use /// for XML doc comments
            - Include <summary> for all public types and members
            - Add <param> tags for all parameters with meaningful descriptions
            - Add <returns> tags for methods that return values
            - Include <exception> tags for thrown exceptions
            - Use <remarks> for additional important information
            - Add <example> tags for complex APIs showing usage
            - Be concise but informative
            - Don't just repeat the member name in the description
            - Focus on the "why" and "what" rather than the "how"
            
            Example:
            /// <summary>
            /// Calculates the sum of two integers.
            /// </summary>
            /// <param name="a">The first number to add.</param>
            /// <param name="b">The second number to add.</param>
            /// <returns>The sum of the two numbers.</returns>
            public int Add(int a, int b) {
                return a + b;
            }
            """;
    }

    private static string GetJavaScriptDocPrompt() {
        return """
            Generate JSDoc comments for JavaScript code following these guidelines:
            - Use /** */ for JSDoc comments
            - Include @description or start with a description
            - Add @param {type} name - description for all parameters
            - Add @returns {type} description for return values
            - Include @throws {ErrorType} description for exceptions
            - Use @example to show usage
            - Add @deprecated if applicable
            - Be concise but informative
            
            Example:
            /**
             * Calculates the sum of two numbers.
             * @param {number} a - The first number to add.
             * @param {number} b - The second number to add.
             * @returns {number} The sum of the two numbers.
             * @example
             * const result = add(2, 3); // returns 5
             */
            function add(a, b) {
                return a + b;
            }
            """;
    }

    private static string GetTypeScriptDocPrompt() {
        return """
            Generate TSDoc comments for TypeScript code following these guidelines:
            - Use /** */ for TSDoc comments
            - Include @description or start with a description
            - Add @param name - description (types are inferred from TypeScript)
            - Add @returns description for return values
            - Include @throws description for exceptions
            - Use @example to show usage
            - Add @deprecated if applicable
            - Be concise but informative
            
            Example:
            /**
             * Calculates the sum of two numbers.
             * @param a - The first number to add.
             * @param b - The second number to add.
             * @returns The sum of the two numbers.
             * @example
             * const result = add(2, 3); // returns 5
             */
            function add(a: number, b: number): number {
                return a + b;
            }
            """;
    }

    private static string GetPythonDocPrompt() {
        return """
            Generate docstrings for Python code following Google style:
            - Use triple quotes for docstrings
            - Start with a one-line summary
            - Add a blank line, then detailed description if needed
            - Use Args: section for parameters
            - Use Returns: section for return values
            - Use Raises: section for exceptions
            - Include Examples: section for complex APIs
            - Be concise but informative
            
            Example:
            def add(a, b):
                \"\"\"Calculates the sum of two numbers.
                
                Args:
                    a (int): The first number to add.
                    b (int): The second number to add.
                
                Returns:
                    int: The sum of the two numbers.
                
                Examples:
                    >>> add(2, 3)
                    5
                \"\"\"
                return a + b
            """;
    }

    private static string GetJavaDocPrompt() {
        return """
            Generate Javadoc comments for Java code following these guidelines:
            - Use /** */ for Javadoc comments
            - Start with a description
            - Add @param tag for each parameter
            - Add @return tag for return values
            - Include @throws tag for exceptions
            - Use @deprecated if applicable
            - Be concise but informative
            
            Example:
            /**
             * Calculates the sum of two integers.
             *
             * @param a the first number to add
             * @param b the second number to add
             * @return the sum of the two numbers
             */
            public int add(int a, int b) {
                return a + b;
            }
            """;
    }

    private static string GetGoDocPrompt() {
        return """
            Generate documentation comments for Go code following these guidelines:
            - Use // for single-line comments before declarations
            - Start with the name of the thing being documented
            - Be concise and clear
            - Document all exported (capitalized) identifiers
            - Group related declarations with a single comment
            
            Example:
            // Add calculates the sum of two integers.
            // It returns the result of adding a and b.
            func Add(a, b int) int {
                return a + b
            }
            """;
    }

    private static string GetGenericDocPrompt() {
        return """
            Generate inline documentation comments following these guidelines:
            - Use appropriate comment syntax for the language
            - Document all public/exported APIs
            - Include parameter descriptions
            - Describe return values
            - Note any exceptions or errors
            - Add usage examples for complex APIs
            - Be concise but informative
            """;
    }

    public static string GetReadmeUpdatePrompt() {
        return """
            Update the README.md file to reflect new features and changes:
            - Maintain the existing structure and style
            - Add new features to appropriate sections
            - Update installation or usage instructions if needed
            - Add code examples for new public APIs
            - Update any version numbers or badges
            - Keep descriptions concise and user-focused
            - Preserve existing content unless it's outdated
            - Use proper markdown formatting
            """;
    }

    public static string GetApiDocGenerationPrompt() {
        return """
            Generate comprehensive API documentation:
            - Extract all public APIs, classes, and methods
            - Include method signatures with parameter types
            - Add descriptions for each API element
            - Organize by namespace, module, or package
            - Include usage examples for main APIs
            - Document parameter types and return types
            - Note any exceptions or errors
            - Use clear, consistent formatting
            - Add a table of contents for navigation
            """;
    }

    public static string GetChangelogUpdatePrompt() {
        return """
            Update the CHANGELOG.md file following Keep a Changelog format:
            - Add new version section at the top
            - Use format: ## [Version] - YYYY-MM-DD
            - Group changes into categories: Added, Changed, Deprecated, Removed, Fixed, Security
            - List each change as a bullet point
            - Be concise but descriptive
            - Link to pull requests or issues when relevant
            - Maintain existing entries
            - Follow semantic versioning
            """;
    }

    public static string GetUsageExamplesPrompt() {
        return """
            Generate practical usage examples for the API:
            - Show common use cases
            - Include complete, runnable code
            - Add comments explaining key parts
            - Demonstrate different features
            - Show error handling when relevant
            - Use realistic variable names
            - Keep examples simple and focused
            - Include expected output or results
            """;
    }
}
