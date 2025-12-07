namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure;

public interface IRepositoryCloner {
    Task<string> CloneRepositoryAsync(string owner, string repo, string token, string branch, CancellationToken cancellationToken = default);
    void CleanupRepository(string localPath);
}
