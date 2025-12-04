namespace RG.OpenCopilot.App;

public sealed class GitHubIssueEventPayload
{
    public string Action { get; set; } = string.Empty;
    public GitHubIssue? Issue { get; set; }
    public GitHubRepository? Repository { get; set; }
    public GitHubInstallation? Installation { get; set; }
    public GitHubLabel? Label { get; set; }
}

public sealed class GitHubIssue
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public List<GitHubLabel> Labels { get; set; } = new();
}

public sealed class GitHubRepository
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Full_Name { get; set; } = string.Empty;
    public GitHubOwner? Owner { get; set; }
}

public sealed class GitHubOwner
{
    public string Login { get; set; } = string.Empty;
}

public sealed class GitHubInstallation
{
    public long Id { get; set; }
}

public sealed class GitHubLabel
{
    public string Name { get; set; } = string.Empty;
}
