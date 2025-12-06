using Octokit;

namespace RG.OpenCopilot.App.GitHub.Git.Adapters;

// Anti-corruption layer: Simple DTOs without Octokit dependencies
public sealed class RepositoryInfo {
    public string DefaultBranch { get; init; } = "";
}

public sealed class ReferenceInfo {
    public string Sha { get; init; } = "";
}

public sealed class PullRequestInfo {
    public int Number { get; init; }
    public string HeadRef { get; init; } = "";
}

public sealed class LanguageInfo {
    public string Name { get; init; } = "";
    public long Bytes { get; init; }
}

public sealed class ContentInfo {
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public bool IsDirectory { get; init; }
}

// Adapter interfaces
public interface IGitHubRepositoryAdapter {
    Task<RepositoryInfo> GetRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync(string owner, string repo, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContentInfo>> GetContentsAsync(string owner, string repo, CancellationToken cancellationToken = default);
}

public interface IGitHubGitAdapter {
    Task<ReferenceInfo> GetReferenceAsync(string owner, string repo, string reference, CancellationToken cancellationToken = default);
    Task<ReferenceInfo> CreateReferenceAsync(string owner, string repo, string refName, string sha, CancellationToken cancellationToken = default);
}

public interface IGitHubPullRequestAdapter {
    Task<PullRequestInfo> CreateAsync(string owner, string repo, string title, string head, string baseRef, string body, CancellationToken cancellationToken = default);
    Task UpdateAsync(string owner, string repo, int number, string title, string body, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PullRequestInfo>> GetAllForRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default);
}

public interface IGitHubIssueAdapter {
    Task CreateCommentAsync(string owner, string repo, int number, string comment, CancellationToken cancellationToken = default);
}

// Adapter implementations
public sealed class GitHubRepositoryAdapter : IGitHubRepositoryAdapter {
    private readonly IGitHubClient _client;

    public GitHubRepositoryAdapter(IGitHubClient client) {
        _client = client;
    }

    public async Task<RepositoryInfo> GetRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default) {
        var repository = await _client.Repository.Get(owner, repo);
        return new RepositoryInfo { DefaultBranch = repository.DefaultBranch };
    }

    public async Task<IReadOnlyList<LanguageInfo>> GetLanguagesAsync(string owner, string repo, CancellationToken cancellationToken = default) {
        var languages = await _client.Repository.GetAllLanguages(owner, repo);
        return languages.Select(l => new LanguageInfo { Name = l.Name, Bytes = l.NumberOfBytes }).ToList();
    }

    public async Task<IReadOnlyList<ContentInfo>> GetContentsAsync(string owner, string repo, CancellationToken cancellationToken = default) {
        var contents = await _client.Repository.Content.GetAllContents(owner, repo);
        return contents.Select(c => new ContentInfo {
            Name = c.Name,
            Path = c.Path,
            IsDirectory = c.Type == ContentType.Dir
        }).ToList();
    }
}

public sealed class GitHubGitAdapter : IGitHubGitAdapter {
    private readonly IGitHubClient _client;

    public GitHubGitAdapter(IGitHubClient client) {
        _client = client;
    }

    public async Task<ReferenceInfo> GetReferenceAsync(string owner, string repo, string reference, CancellationToken cancellationToken = default) {
        var refData = await _client.Git.Reference.Get(owner, repo, reference);
        return new ReferenceInfo { Sha = refData.Object.Sha };
    }

    public async Task<ReferenceInfo> CreateReferenceAsync(string owner, string repo, string refName, string sha, CancellationToken cancellationToken = default) {
        var newRef = new NewReference(refName, sha);
        var refData = await _client.Git.Reference.Create(owner, repo, newRef);
        return new ReferenceInfo { Sha = refData.Object.Sha };
    }
}

public sealed class GitHubPullRequestAdapter : IGitHubPullRequestAdapter {
    private readonly IGitHubClient _client;

    public GitHubPullRequestAdapter(IGitHubClient client) {
        _client = client;
    }

    public async Task<PullRequestInfo> CreateAsync(string owner, string repo, string title, string head, string baseRef, string body, CancellationToken cancellationToken = default) {
        var newPr = new NewPullRequest(title, head, baseRef) { Body = body };
        var pr = await _client.PullRequest.Create(owner, repo, newPr);
        return new PullRequestInfo { Number = pr.Number, HeadRef = pr.Head.Ref };
    }

    public async Task UpdateAsync(string owner, string repo, int number, string title, string body, CancellationToken cancellationToken = default) {
        var update = new PullRequestUpdate { Title = title, Body = body };
        await _client.PullRequest.Update(owner, repo, number, update);
    }

    public async Task<IReadOnlyList<PullRequestInfo>> GetAllForRepositoryAsync(string owner, string repo, CancellationToken cancellationToken = default) {
        var request = new PullRequestRequest { State = ItemStateFilter.All };
        var prs = await _client.PullRequest.GetAllForRepository(owner, repo, request);
        return prs.Select(pr => new PullRequestInfo { Number = pr.Number, HeadRef = pr.Head.Ref }).ToList();
    }
}

public sealed class GitHubIssueAdapter : IGitHubIssueAdapter {
    private readonly IGitHubClient _client;

    public GitHubIssueAdapter(IGitHubClient client) {
        _client = client;
    }

    public async Task CreateCommentAsync(string owner, string repo, int number, string comment, CancellationToken cancellationToken = default) {
        await _client.Issue.Comment.Create(owner, repo, number, comment);
    }
}
