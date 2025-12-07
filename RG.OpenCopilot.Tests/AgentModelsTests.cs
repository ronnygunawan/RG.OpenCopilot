using RG.OpenCopilot.PRGenerationAgent;
using Shouldly;
using System.Text.Json;

namespace RG.OpenCopilot.Tests;

public class AgentModelsTests {
    [Fact]
    public void AgentPlan_CanBeCreatedWithInitializers() {
        // Act
        var plan = new AgentPlan {
            ProblemSummary = "Test summary",
            Constraints = { "Constraint 1", "Constraint 2" },
            Steps = {
                new PlanStep { Id = "1", Title = "Step 1", Details = "Details", Done = false }
            },
            Checklist = { "Item 1" },
            FileTargets = { "file1.cs" }
        };

        // Assert
        plan.ProblemSummary.ShouldBe("Test summary");
        plan.Constraints.Count.ShouldBe(2);
        plan.Steps.Count.ShouldBe(1);
        plan.Checklist.Count.ShouldBe(1);
        plan.FileTargets.Count.ShouldBe(1);
    }

    [Fact]
    public void AgentPlan_DefaultValuesAreEmpty() {
        // Act
        var plan = new AgentPlan();

        // Assert
        plan.ProblemSummary.ShouldBe("");
        plan.Constraints.ShouldBeEmpty();
        plan.Steps.ShouldBeEmpty();
        plan.Checklist.ShouldBeEmpty();
        plan.FileTargets.ShouldBeEmpty();
    }

    [Fact]
    public void PlanStep_CanBeCreatedWithProperties() {
        // Act
        var step = new PlanStep {
            Id = "step-1",
            Title = "Test Step",
            Details = "Step details",
            Done = true
        };

        // Assert
        step.Id.ShouldBe("step-1");
        step.Title.ShouldBe("Test Step");
        step.Details.ShouldBe("Step details");
        step.Done.ShouldBeTrue();
    }

    [Fact]
    public void PlanStep_DoneCanBeModified() {
        // Arrange
        var step = new PlanStep {
            Id = "1",
            Title = "Test",
            Details = "Details",
            Done = false
        };

        // Act
        step.Done = true;

        // Assert
        step.Done.ShouldBeTrue();
    }

    [Fact]
    public void AgentTask_CanBeCreatedWithAllProperties() {
        // Act
        var task = new AgentTask {
            Id = "test-id",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 42,
            Plan = new AgentPlan { ProblemSummary = "Test" },
            Status = AgentTaskStatus.Executing
        };

        // Assert
        task.Id.ShouldBe("test-id");
        task.InstallationId.ShouldBe(123);
        task.RepositoryOwner.ShouldBe("owner");
        task.RepositoryName.ShouldBe("repo");
        task.IssueNumber.ShouldBe(42);
        task.Plan.ShouldNotBeNull();
        task.Status.ShouldBe(AgentTaskStatus.Executing);
    }

    [Fact]
    public void AgentTask_DefaultStatusIsPendingPlanning() {
        // Act
        var task = new AgentTask {
            Id = "test",
            InstallationId = 1,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1
        };

        // Assert
        task.Status.ShouldBe(AgentTaskStatus.PendingPlanning);
    }

    [Fact]
    public void AgentTask_PlanCanBeNull() {
        // Act
        var task = new AgentTask {
            Id = "test",
            InstallationId = 1,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1
        };

        // Assert
        task.Plan.ShouldBeNull();
    }

    [Fact]
    public void AgentTask_StatusCanBeModified() {
        // Arrange
        var task = new AgentTask {
            Id = "test",
            InstallationId = 1,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 1
        };

        // Act
        task.Status = AgentTaskStatus.Completed;

        // Assert
        task.Status.ShouldBe(AgentTaskStatus.Completed);
    }

    [Fact]
    public void AgentTaskContext_CanBeCreatedWithAllProperties() {
        // Act
        var context = new AgentTaskContext {
            IssueTitle = "Test Issue",
            IssueBody = "Issue body",
            InstructionsMarkdown = "# Instructions",
            RepositorySummary = "C# project"
        };

        // Assert
        context.IssueTitle.ShouldBe("Test Issue");
        context.IssueBody.ShouldBe("Issue body");
        context.InstructionsMarkdown.ShouldBe("# Instructions");
        context.RepositorySummary.ShouldBe("C# project");
    }

    [Fact]
    public void AgentTaskStatus_HasAllExpectedValues() {
        // Assert
        Enum.GetValues<AgentTaskStatus>().ShouldContain(AgentTaskStatus.PendingPlanning);
        Enum.GetValues<AgentTaskStatus>().ShouldContain(AgentTaskStatus.Planned);
        Enum.GetValues<AgentTaskStatus>().ShouldContain(AgentTaskStatus.Executing);
        Enum.GetValues<AgentTaskStatus>().ShouldContain(AgentTaskStatus.Completed);
        Enum.GetValues<AgentTaskStatus>().ShouldContain(AgentTaskStatus.Blocked);
        Enum.GetValues<AgentTaskStatus>().ShouldContain(AgentTaskStatus.Failed);
    }

    [Fact]
    public void FileStructure_CanBeCreatedWithProperties() {
        // Act
        var structure = new FileStructure {
            FilePath = "test.cs",
            Language = "C#",
            Imports = { "System", "System.Linq" },
            Classes = { "TestClass" },
            Functions = { "TestMethod" },
            Namespaces = { "TestNamespace" }
        };

        // Assert
        structure.FilePath.ShouldBe("test.cs");
        structure.Language.ShouldBe("C#");
        structure.Imports.Count.ShouldBe(2);
        structure.Classes.Count.ShouldBe(1);
        structure.Functions.Count.ShouldBe(1);
        structure.Namespaces.Count.ShouldBe(1);
    }

    [Fact]
    public void FileTreeNode_CanBeCreatedAsDirectory() {
        // Act
        var node = new FileTreeNode {
            Name = "src",
            Path = "/src",
            IsDirectory = true,
            Children = {
                new FileTreeNode { Name = "file1.cs", Path = "/src/file1.cs", IsDirectory = false }
            }
        };

        // Assert
        node.Name.ShouldBe("src");
        node.IsDirectory.ShouldBeTrue();
        node.Children.Count.ShouldBe(1);
    }

    [Fact]
    public void FileTreeNode_CanBeCreatedAsFile() {
        // Act
        var node = new FileTreeNode {
            Name = "test.cs",
            Path = "/test.cs",
            IsDirectory = false
        };

        // Assert
        node.Name.ShouldBe("test.cs");
        node.IsDirectory.ShouldBeFalse();
        node.Children.ShouldBeEmpty();
    }

    [Fact]
    public void FileTree_CanBeCreatedWithRootAndFiles() {
        // Act
        var tree = new FileTree {
            Root = new FileTreeNode { Name = "root", Path = "/", IsDirectory = true },
            AllFiles = { "file1.cs", "file2.cs", "file3.cs" }
        };

        // Assert
        tree.Root.Name.ShouldBe("root");
        tree.AllFiles.Count.ShouldBe(3);
    }

    [Fact]
    public void FileChange_CanBeCreatedForCreatedFile() {
        // Act
        var change = new FileChange {
            Type = ChangeType.Created,
            Path = "newfile.cs",
            OldContent = null,
            NewContent = "public class Test { }"
        };

        // Assert
        change.Type.ShouldBe(ChangeType.Created);
        change.Path.ShouldBe("newfile.cs");
        change.OldContent.ShouldBeNull();
        change.NewContent.ShouldNotBeNull();
    }

    [Fact]
    public void FileChange_CanBeCreatedForModifiedFile() {
        // Act
        var change = new FileChange {
            Type = ChangeType.Modified,
            Path = "existing.cs",
            OldContent = "old content",
            NewContent = "new content"
        };

        // Assert
        change.Type.ShouldBe(ChangeType.Modified);
        change.OldContent.ShouldNotBeNull();
        change.NewContent.ShouldNotBeNull();
    }

    [Fact]
    public void FileChange_CanBeCreatedForDeletedFile() {
        // Act
        var change = new FileChange {
            Type = ChangeType.Deleted,
            Path = "deleted.cs",
            OldContent = "deleted content",
            NewContent = null
        };

        // Assert
        change.Type.ShouldBe(ChangeType.Deleted);
        change.OldContent.ShouldNotBeNull();
        change.NewContent.ShouldBeNull();
    }

    [Fact]
    public void ChangeType_HasAllExpectedValues() {
        // Assert
        Enum.GetValues<ChangeType>().ShouldContain(ChangeType.Created);
        Enum.GetValues<ChangeType>().ShouldContain(ChangeType.Modified);
        Enum.GetValues<ChangeType>().ShouldContain(ChangeType.Deleted);
    }

    [Fact]
    public void AgentPlan_SerializesToJson() {
        // Arrange
        var plan = new AgentPlan {
            ProblemSummary = "Test",
            Constraints = { "C1" },
            Steps = { new PlanStep { Id = "1", Title = "T1", Details = "D1", Done = false } },
            Checklist = { "I1" },
            FileTargets = { "F1" }
        };

        // Act
        var json = JsonSerializer.Serialize(plan);
        var deserialized = JsonSerializer.Deserialize<AgentPlan>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.ProblemSummary.ShouldBe("Test");
        deserialized.Constraints.Count.ShouldBe(1);
        deserialized.Steps.Count.ShouldBe(1);
    }

    [Fact]
    public void AgentTask_SerializesToJson() {
        // Arrange
        var task = new AgentTask {
            Id = "test",
            InstallationId = 123,
            RepositoryOwner = "owner",
            RepositoryName = "repo",
            IssueNumber = 42,
            Status = AgentTaskStatus.Planned
        };

        // Act
        var json = JsonSerializer.Serialize(task);
        var deserialized = JsonSerializer.Deserialize<AgentTask>(json);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Id.ShouldBe("test");
        deserialized.InstallationId.ShouldBe(123);
        deserialized.RepositoryOwner.ShouldBe("owner");
        deserialized.RepositoryName.ShouldBe("repo");
        deserialized.IssueNumber.ShouldBe(42);
        deserialized.Status.ShouldBe(AgentTaskStatus.Planned);
    }
}
