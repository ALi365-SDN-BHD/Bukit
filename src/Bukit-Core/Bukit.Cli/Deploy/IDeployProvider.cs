using Bukit.Shared;

namespace Bukit.Cli.Deploy;

public interface IDeployProvider
{
    string Name { get; }
    Task<DeployResult> DeployAsync(DeployContext context, CancellationToken ct);
}

public sealed record DeployContext
{
    public required string OutputDir { get; init; }
    public required string SiteUrl { get; init; }
    public required string BaseUrl { get; init; }
    public required string? Branch { get; init; }
    public required string? Message { get; init; }
    public required string? Cname { get; init; }
    // Internal on purpose: keeps the public API baseline unchanged for a CLI-only option.
    internal string? TokenEnv { get; init; }
    // Internal on purpose: deploy.repo / --repo is a CLI-resolved target, not a new public provider contract.
    internal string? Repo { get; init; }
    internal string? RepoSource { get; init; }
    public bool KeepHistory { get; init; }
    public bool Force { get; init; }
    public required ILogger Logger { get; init; }
}

public sealed record DeployResult
{
    public bool Success { get; init; }
    public string? DeployedUrl { get; init; }
    public string? Error { get; init; }
}
