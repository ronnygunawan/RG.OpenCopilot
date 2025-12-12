using System.Text.Json;
using RG.OpenCopilot.PRGenerationAgent.Services;
using RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;
using Shouldly;

namespace RG.OpenCopilot.Tests;

public class GitHubWebhookModelsTests {
    [Fact]
    public void GitHubIssueEventPayload_DeserializesCorrectly() {
        // Arrange
        var json = """
            {
                "action": "labeled",
                "issue": {
                    "number": 42,
                    "title": "Test Issue",
                    "body": "Test body",
                    "state": "open",
                    "labels": [
                        { "name": "bug" },
                        { "name": "copilot-assisted" }
                    ]
                },
                "repository": {
                    "id": 12345,
                    "name": "test-repo",
                    "full_name": "owner/test-repo",
                    "owner": {
                        "login": "owner"
                    }
                },
                "installation": {
                    "id": 67890
                },
                "label": {
                    "name": "copilot-assisted"
                }
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<GitHubIssueEventPayload>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        payload.ShouldNotBeNull();
        payload.Action.ShouldBe("labeled");
        payload.Issue.ShouldNotBeNull();
        payload.Issue.Number.ShouldBe(42);
        payload.Issue.Title.ShouldBe("Test Issue");
        payload.Issue.Body.ShouldBe("Test body");
        payload.Issue.State.ShouldBe("open");
        payload.Issue.Labels.Count.ShouldBe(2);
        payload.Issue.Labels[0].Name.ShouldBe("bug");
        payload.Issue.Labels[1].Name.ShouldBe("copilot-assisted");
        payload.Repository.ShouldNotBeNull();
        payload.Repository.Id.ShouldBe(12345);
        payload.Repository.Name.ShouldBe("test-repo");
        payload.Repository.Full_Name.ShouldBe("owner/test-repo");
        payload.Repository.Owner.ShouldNotBeNull();
        payload.Repository.Owner.Login.ShouldBe("owner");
        payload.Installation.ShouldNotBeNull();
        payload.Installation.Id.ShouldBe(67890);
        payload.Label.ShouldNotBeNull();
        payload.Label.Name.ShouldBe("copilot-assisted");
    }

    [Fact]
    public void GitHubIssueEventPayload_SerializesCorrectly() {
        // Arrange
        var payload = new GitHubIssueEventPayload {
            Action = "opened",
            Issue = new GitHubIssue {
                Number = 1,
                Title = "Test",
                Body = "Test body",
                State = "open",
                Labels = new List<GitHubLabel> {
                    new() { Name = "enhancement" }
                }
            },
            Repository = new GitHubRepository {
                Id = 123,
                Name = "repo",
                Full_Name = "owner/repo",
                Owner = new GitHubOwner {
                    Login = "owner"
                }
            },
            Installation = new GitHubInstallation {
                Id = 456
            },
            Label = new GitHubLabel {
                Name = "enhancement"
            }
        };

        // Act
        var json = JsonSerializer.Serialize(payload);
        var deserialized = JsonSerializer.Deserialize<GitHubIssueEventPayload>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.Action.ShouldBe(payload.Action);
        deserialized.Issue!.Number.ShouldBe(payload.Issue.Number);
        deserialized.Repository!.Name.ShouldBe(payload.Repository.Name);
    }

    [Fact]
    public void GitHubIssue_HandlesEmptyLabels() {
        // Arrange
        var json = """
            {
                "number": 1,
                "title": "Test",
                "body": "Body",
                "state": "open",
                "labels": []
            }
            """;

        // Act
        var issue = JsonSerializer.Deserialize<GitHubIssue>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        issue.ShouldNotBeNull();
        issue.Labels.ShouldBeEmpty();
    }

    [Fact]
    public void GitHubRepository_HandlesNullOwner() {
        // Arrange
        var json = """
            {
                "id": 123,
                "name": "test",
                "full_name": "owner/test"
            }
            """;

        // Act
        var repo = JsonSerializer.Deserialize<GitHubRepository>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        repo.ShouldNotBeNull();
        repo.Owner.ShouldBeNull();
    }

    [Fact]
    public void GitHubIssueEventPayload_HandlesMinimalPayload() {
        // Arrange
        var json = """
            {
                "action": "opened"
            }
            """;

        // Act
        var payload = JsonSerializer.Deserialize<GitHubIssueEventPayload>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        payload.ShouldNotBeNull();
        payload.Action.ShouldBe("opened");
        payload.Issue.ShouldBeNull();
        payload.Repository.ShouldBeNull();
        payload.Installation.ShouldBeNull();
        payload.Label.ShouldBeNull();
    }

    [Fact]
    public void GitHubOwner_DeserializesCorrectly() {
        // Arrange
        var json = """
            {
                "login": "testuser"
            }
            """;

        // Act
        var owner = JsonSerializer.Deserialize<GitHubOwner>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        owner.ShouldNotBeNull();
        owner.Login.ShouldBe("testuser");
    }

    [Fact]
    public void GitHubInstallation_DeserializesCorrectly() {
        // Arrange
        var json = """
            {
                "id": 999
            }
            """;

        // Act
        var installation = JsonSerializer.Deserialize<GitHubInstallation>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        installation.ShouldNotBeNull();
        installation.Id.ShouldBe(999);
    }

    [Fact]
    public void GitHubLabel_DeserializesCorrectly() {
        // Arrange
        var json = """
            {
                "name": "bug"
            }
            """;

        // Act
        var label = JsonSerializer.Deserialize<GitHubLabel>(json, new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        label.ShouldNotBeNull();
        label.Name.ShouldBe("bug");
    }
}
