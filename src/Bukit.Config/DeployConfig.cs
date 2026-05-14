namespace Bukit.Config;

public sealed record DeployConfig
{
    public string? Provider { get; init; }
    public string Branch { get; init; } = "gh-pages";
    public string Message { get; init; } = "bukit deploy";
    public string? Cname { get; init; }
    public bool KeepHistory { get; init; }
    public IReadOnlyDictionary<string, object>? Options { get; init; }
}
