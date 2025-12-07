namespace RG.OpenCopilot.PRGenerationAgent.Services.GitHub.Webhook.Models;

public sealed class GitHubIssueEventPayload {
    public string Action { get; set; } = "";
    public GitHubIssue? Issue { get; set; }
    public GitHubRepository? Repository { get; set; }
    public GitHubInstallation? Installation { get; set; }
    public GitHubLabel? Label { get; set; }
}

public sealed class GitHubIssue {
    public int Number { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string State { get; set; } = "";
    public List<GitHubLabel> Labels { get; set; } = [];
}

public sealed class GitHubRepository {
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Full_Name { get; set; } = "";
    public GitHubOwner? Owner { get; set; }
}

public sealed class GitHubOwner {
    public string Login { get; set; } = "";
}

public sealed class GitHubInstallation {
    public long Id { get; set; }
}

public sealed class GitHubLabel {
    public string Name { get; set; } = "";
}
