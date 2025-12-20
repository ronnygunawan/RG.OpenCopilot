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
}
