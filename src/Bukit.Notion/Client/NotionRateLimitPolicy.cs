namespace Bukit.Notion.Client;

public sealed record NotionRateLimitPolicy(
    int MaxRetries)
{
    public bool ShouldRetry(int attempt)
        => attempt < MaxRetries;
}
