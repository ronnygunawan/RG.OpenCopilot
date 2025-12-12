using RG.OpenCopilot.PRGenerationAgent;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;
using Microsoft.Extensions.Logging;

namespace RG.OpenCopilot.Tests;

public class SimplePlannerServiceTests {
    [Fact]
    public async Task CreatePlanAsync_CreatesPlanWithCorrectProblemSummary() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Add user authentication",
            IssueBody = "Implement JWT-based authentication for the API"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.ProblemSummary.ShouldBe("Implement solution for: Add user authentication");
    }

    [Fact]
    public async Task CreatePlanAsync_CreatesExactlyFiveSteps() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Fix bug in login flow",
            IssueBody = "Users cannot log in with valid credentials"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.Steps.Count.ShouldBe(5);
    }

    [Fact]
    public async Task CreatePlanAsync_FirstStepIsAnalyzeRequirements() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Update documentation",
            IssueBody = "Add API documentation for all endpoints"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.Steps[0].Id.ShouldBe("step-1");
        plan.Steps[0].Title.ShouldBe("Analyze the issue requirements");
        plan.Steps[0].Done.ShouldBeFalse();
    }

    [Fact]
    public async Task CreatePlanAsync_AllStepsAreNotDone() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Refactor database layer",
            IssueBody = "Move to Entity Framework Core"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.Steps.ShouldAllBe(step => step.Done == false);
    }

    [Fact]
    public async Task CreatePlanAsync_IncludesThreeConstraints() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Add caching layer",
            IssueBody = "Implement Redis caching for API responses"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.Constraints.Count.ShouldBe(3);
        plan.Constraints.ShouldContain("Follow existing code style and conventions");
        plan.Constraints.ShouldContain("Ensure all tests pass");
        plan.Constraints.ShouldContain("Update documentation if needed");
    }

    [Fact]
    public async Task CreatePlanAsync_IncludesFourChecklistItems() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Optimize query performance",
            IssueBody = "Reduce database query time by 50%"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.Checklist.Count.ShouldBe(4);
        plan.Checklist.ShouldContain("All code changes are complete");
        plan.Checklist.ShouldContain("All tests pass");
        plan.Checklist.ShouldContain("Documentation is updated");
        plan.Checklist.ShouldContain("Code follows project conventions");
    }

    [Fact]
    public async Task CreatePlanAsync_FileTargetsIsTBD() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Add logging framework",
            IssueBody = "Integrate Serilog for structured logging"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.FileTargets.Count.ShouldBe(1);
        plan.FileTargets[0].ShouldBe("TBD - will be determined during implementation");
    }

    [Fact]
    public async Task CreatePlanAsync_StepsHaveUniqueIds() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Implement rate limiting",
            IssueBody = "Add rate limiting to prevent API abuse"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        var stepIds = plan.Steps.Select(s => s.Id).ToList();
        stepIds.Distinct().Count().ShouldBe(stepIds.Count);
    }

    [Fact]
    public async Task CreatePlanAsync_AllStepsHaveDetails() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Add email notifications",
            IssueBody = "Send email when important events occur"
        };

        // Act
        var plan = await service.CreatePlanAsync(context: context);

        // Assert
        plan.Steps.ShouldAllBe(step => !string.IsNullOrWhiteSpace(step.Details));
    }

    [Fact]
    public async Task CreatePlanAsync_LogsCreationMessage() {
        // Arrange
        var logger = new TestLogger<SimplePlannerService>();
        var service = new SimplePlannerService(logger: logger);
        var context = new AgentTaskContext {
            IssueTitle = "Test issue",
            IssueBody = "Test body"
        };

        // Act
        await service.CreatePlanAsync(context: context);

        // Assert
        logger.LoggedMessages.ShouldContain(msg => msg.Contains("Creating plan for issue"));
        logger.LoggedMessages.ShouldContain(msg => msg.Contains("Plan created with"));
    }

    // Test helper class
    private class TestLogger<T> : ILogger<T> {
        public List<string> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) {
            LoggedMessages.Add(formatter(state, exception));
        }
    }
}
