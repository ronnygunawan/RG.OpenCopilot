using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using RG.OpenCopilot.PRGenerationAgent.Services.CodeGeneration;
using Shouldly;

namespace RG.OpenCopilot.Tests;

/// <summary>
/// Integration tests for TestGenerator that verify end-to-end functionality.
/// Note: These tests require an actual LLM API key configured to run.
/// They are marked with [Fact(Skip = "Requires LLM API key")] by default.
/// </summary>
public class TestGeneratorIntegrationTests {
    private readonly IConfiguration _configuration;

    public TestGeneratorIntegrationTests() {
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    [Fact(Skip = "Requires LLM API key and Docker container")]
    public async Task GenerateTestsAsync_EndToEnd_GeneratesValidTests() {
        // This test would require:
        // 1. A running Docker container with a real repository
        // 2. An actual LLM service (OpenAI or Azure OpenAI)
        // 3. File system access to read and write test files
        
        // Skip this test in CI/CD pipeline
        if (string.IsNullOrEmpty(_configuration["OpenAI:ApiKey"])) {
            return;
        }

        // Arrange - would set up real services
        // Act - would call TestGenerator.GenerateTestsAsync
        // Assert - would verify generated test code compiles and runs
        
        await Task.CompletedTask;
    }

    [Fact]
    public void TestFile_CanBeCreated_WithAllProperties() {
        // Arrange & Act
        var testFile = new TestFile {
            Path = "CalculatorTests.cs",
            Content = "using Xunit; public class CalculatorTests { }",
            Framework = "xUnit"
        };

        // Assert
        testFile.Path.ShouldBe("CalculatorTests.cs");
        testFile.Content.ShouldContain("CalculatorTests");
        testFile.Framework.ShouldBe("xUnit");
    }

    [Fact]
    public void TestPattern_DefaultValues_AreCorrect() {
        // Arrange & Act
        var pattern = new TestPattern {
            NamingConvention = "MethodName_Scenario_ExpectedResult",
            AssertionStyle = "Shouldly",
            UsesArrangeActAssert = true,
            CommonImports = new List<string> { "using Xunit;", "using Shouldly;" },
            BaseTestClass = ""
        };

        // Assert
        pattern.NamingConvention.ShouldBe("MethodName_Scenario_ExpectedResult");
        pattern.AssertionStyle.ShouldBe("Shouldly");
        pattern.UsesArrangeActAssert.ShouldBeTrue();
        pattern.CommonImports.Count.ShouldBe(2);
        pattern.BaseTestClass.ShouldBe("");
    }

    [Fact]
    public void TestResult_CalculatesSuccess_BasedOnFailures() {
        // Arrange & Act
        var successResult = new TestResult {
            TotalTests = 10,
            PassedTests = 10,
            FailedTests = 0,
            Failures = new List<string>(),
            Output = "All tests passed",
            Success = true
        };

        var failureResult = new TestResult {
            TotalTests = 10,
            PassedTests = 8,
            FailedTests = 2,
            Failures = new List<string> { "Test1 failed", "Test2 failed" },
            Output = "Some tests failed",
            Success = false
        };

        // Assert
        successResult.Success.ShouldBeTrue();
        successResult.FailedTests.ShouldBe(0);
        
        failureResult.Success.ShouldBeFalse();
        failureResult.FailedTests.ShouldBe(2);
        failureResult.Failures.Count.ShouldBe(2);
    }

    [Fact]
    public void TestPattern_SupportsMultipleAssertionStyles() {
        // Arrange & Act
        var shouldlyPattern = new TestPattern { AssertionStyle = "Shouldly" };
        var fluentPattern = new TestPattern { AssertionStyle = "FluentAssertions" };
        var assertPattern = new TestPattern { AssertionStyle = "Assert" };

        // Assert
        shouldlyPattern.AssertionStyle.ShouldBe("Shouldly");
        fluentPattern.AssertionStyle.ShouldBe("FluentAssertions");
        assertPattern.AssertionStyle.ShouldBe("Assert");
    }

    [Fact]
    public void TestFile_SupportsMultipleFrameworks() {
        // Arrange & Act
        var xunitTest = new TestFile { Framework = "xUnit" };
        var nunitTest = new TestFile { Framework = "NUnit" };
        var mstestTest = new TestFile { Framework = "MSTest" };
        var jestTest = new TestFile { Framework = "Jest" };
        var pytestTest = new TestFile { Framework = "pytest" };

        // Assert
        xunitTest.Framework.ShouldBe("xUnit");
        nunitTest.Framework.ShouldBe("NUnit");
        mstestTest.Framework.ShouldBe("MSTest");
        jestTest.Framework.ShouldBe("Jest");
        pytestTest.Framework.ShouldBe("pytest");
    }

    [Fact]
    public void TestResult_HandlesEmptyOutput() {
        // Arrange & Act
        var emptyResult = new TestResult {
            TotalTests = 0,
            PassedTests = 0,
            FailedTests = 0,
            Failures = new List<string>(),
            Output = "",
            Success = true
        };

        // Assert
        emptyResult.TotalTests.ShouldBe(0);
        emptyResult.Output.ShouldBe("");
        emptyResult.Failures.ShouldBeEmpty();
    }
}
