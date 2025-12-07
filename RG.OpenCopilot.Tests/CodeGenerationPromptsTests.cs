using RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class CodeGenerationPromptsTests {
    [Fact]
    public void GetBestPractices_ReturnsNonEmptyString() {
        // Act
        var result = CodeGenerationPrompts.GetBestPractices();

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("Best Practices");
        result.ShouldContain("Code Quality");
        result.ShouldContain("Error Handling");
        result.ShouldContain("Security");
    }

    [Theory]
    [InlineData("C#")]
    [InlineData("csharp")]
    [InlineData("JavaScript")]
    [InlineData("js")]
    [InlineData("TypeScript")]
    [InlineData("ts")]
    [InlineData("Python")]
    [InlineData("py")]
    [InlineData("Java")]
    [InlineData("Go")]
    [InlineData("Rust")]
    public void GetTechnologyPrompt_WithValidLanguage_ReturnsNonEmptyString(string language) {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt(language);

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetTechnologyPrompt_CSharp_ContainsCSharpGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("C#");

        // Assert
        result.ShouldContain("C# Specific Guidelines");
        result.ShouldContain("PascalCase");
        result.ShouldContain("async");
        result.ShouldContain("nullable reference types");
    }

    [Fact]
    public void GetTechnologyPrompt_JavaScript_ContainsJavaScriptGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("JavaScript");

        // Assert
        result.ShouldContain("JavaScript Specific Guidelines");
        result.ShouldContain("ES6+");
        result.ShouldContain("const");
        result.ShouldContain("arrow functions");
    }

    [Fact]
    public void GetTechnologyPrompt_TypeScript_ContainsTypeScriptGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("TypeScript");

        // Assert
        result.ShouldContain("TypeScript Specific Guidelines");
        result.ShouldContain("strong typing");
        result.ShouldContain("interfaces");
        result.ShouldContain("generics");
    }

    [Fact]
    public void GetTechnologyPrompt_Python_ContainsPythonGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("Python");

        // Assert
        result.ShouldContain("Python Specific Guidelines");
        result.ShouldContain("PEP 8");
        result.ShouldContain("type hints");
        result.ShouldContain("docstrings");
    }

    [Fact]
    public void GetTechnologyPrompt_Java_ContainsJavaGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("Java");

        // Assert
        result.ShouldContain("Java Specific Guidelines");
        result.ShouldContain("PascalCase");
        result.ShouldContain("JavaDoc");
        result.ShouldContain("Stream API");
    }

    [Fact]
    public void GetTechnologyPrompt_Go_ContainsGoGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("Go");

        // Assert
        result.ShouldContain("Go Specific Guidelines");
        result.ShouldContain("Effective Go");
        result.ShouldContain("goroutines");
        result.ShouldContain("defer");
    }

    [Fact]
    public void GetTechnologyPrompt_Rust_ContainsRustGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("Rust");

        // Assert
        result.ShouldContain("Rust Specific Guidelines");
        result.ShouldContain("borrowing");
        result.ShouldContain("Result");
    }

    [Fact]
    public void GetTechnologyPrompt_UnknownLanguage_ReturnsGenericGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetTechnologyPrompt("UnknownLang");

        // Assert
        result.ShouldNotBeNull();
        result.ShouldNotBeEmpty();
        result.ShouldContain("Generic Language Guidelines");
    }

    [Fact]
    public void GetTechnologyPrompt_CaseInsensitive_ReturnsSameResult() {
        // Act
        var upperCase = CodeGenerationPrompts.GetTechnologyPrompt("PYTHON");
        var lowerCase = CodeGenerationPrompts.GetTechnologyPrompt("python");
        var mixedCase = CodeGenerationPrompts.GetTechnologyPrompt("PyThOn");

        // Assert
        upperCase.ShouldBe(lowerCase);
        lowerCase.ShouldBe(mixedCase);
    }

    [Fact]
    public void GetBestPractices_ContainsSolidPrinciples() {
        // Act
        var result = CodeGenerationPrompts.GetBestPractices();

        // Assert
        result.ShouldContain("SOLID");
    }

    [Fact]
    public void GetBestPractices_ContainsSecurityGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetBestPractices();

        // Assert
        result.ShouldContain("SQL injection");
        result.ShouldContain("XSS");
        result.ShouldContain("validate");
        result.ShouldContain("sanitize");
    }

    [Fact]
    public void GetBestPractices_ContainsPerformanceGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetBestPractices();

        // Assert
        result.ShouldContain("Performance");
    }

    [Fact]
    public void GetBestPractices_ContainsTestingGuidelines() {
        // Act
        var result = CodeGenerationPrompts.GetBestPractices();

        // Assert
        result.ShouldContain("Testing");
        result.ShouldContain("testable");
    }

    [Fact]
    public void GetTechnologyPrompt_AllLanguages_ReturnUniqueContent() {
        // Arrange
        var languages = new[] { "C#", "JavaScript", "TypeScript", "Python", "Java", "Go", "Rust" };

        // Act
        var prompts = languages.Select(lang => CodeGenerationPrompts.GetTechnologyPrompt(lang)).ToList();

        // Assert
        prompts.Distinct().Count().ShouldBe(prompts.Count);
    }
}
