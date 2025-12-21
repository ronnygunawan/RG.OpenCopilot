using RG.OpenCopilot.PRGenerationAgent.Infrastructure.Models;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class InstallationPermissionsTests {
    [Fact]
    public void HasRequiredPermissions_WithAllPermissions_ReturnsTrue() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = true,
            HasPullRequests = true,
            HasWorkflows = true
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeTrue();
    }

    [Fact]
    public void HasRequiredPermissions_WithOnlyRequiredPermissions_ReturnsTrue() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = true,
            HasPullRequests = true,
            HasWorkflows = false
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeTrue();
    }

    [Fact]
    public void HasRequiredPermissions_WithMissingContents_ReturnsFalse() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = false,
            HasIssues = true,
            HasPullRequests = true,
            HasWorkflows = true
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_WithMissingIssues_ReturnsFalse() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = false,
            HasPullRequests = true,
            HasWorkflows = true
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_WithMissingPullRequests_ReturnsFalse() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = true,
            HasPullRequests = false,
            HasWorkflows = true
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_WithNoPermissions_ReturnsFalse() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = false,
            HasIssues = false,
            HasPullRequests = false,
            HasWorkflows = false
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_WithMissingContentsAndIssues_ReturnsFalse() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = false,
            HasIssues = false,
            HasPullRequests = true,
            HasWorkflows = true
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_WithMissingIssuesAndPullRequests_ReturnsFalse() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = false,
            HasPullRequests = false,
            HasWorkflows = true
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_WithMissingContentsAndPullRequests_ReturnsFalse() {
        // Arrange
        var permissions = new AppInstallationPermissions {
            HasContents = false,
            HasIssues = true,
            HasPullRequests = false,
            HasWorkflows = true
        };

        // Act
        var hasRequired = permissions.HasRequiredPermissions();

        // Assert
        hasRequired.ShouldBeFalse();
    }

    [Fact]
    public void AppInstallationPermissions_DefaultValues_AreFalse() {
        // Arrange & Act
        var permissions = new AppInstallationPermissions();

        // Assert
        permissions.HasContents.ShouldBeFalse();
        permissions.HasIssues.ShouldBeFalse();
        permissions.HasPullRequests.ShouldBeFalse();
        permissions.HasWorkflows.ShouldBeFalse();
        permissions.HasRequiredPermissions().ShouldBeFalse();
    }

    [Fact]
    public void AppInstallationPermissions_CanBeCreatedWithInitializer() {
        // Arrange & Act
        var permissions = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = false,
            HasPullRequests = true,
            HasWorkflows = false
        };

        // Assert
        permissions.HasContents.ShouldBeTrue();
        permissions.HasIssues.ShouldBeFalse();
        permissions.HasPullRequests.ShouldBeTrue();
        permissions.HasWorkflows.ShouldBeFalse();
    }

    [Fact]
    public void HasRequiredPermissions_WorkflowsIsOptional_NotRequiredForSuccess() {
        // Arrange - all required permissions but no workflows
        var permissionsWithoutWorkflows = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = true,
            HasPullRequests = true,
            HasWorkflows = false
        };

        // Arrange - all permissions including workflows
        var permissionsWithWorkflows = new AppInstallationPermissions {
            HasContents = true,
            HasIssues = true,
            HasPullRequests = true,
            HasWorkflows = true
        };

        // Act & Assert
        permissionsWithoutWorkflows.HasRequiredPermissions().ShouldBeTrue();
        permissionsWithWorkflows.HasRequiredPermissions().ShouldBeTrue();
    }
}
