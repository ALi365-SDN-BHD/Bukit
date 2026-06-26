namespace Bukit.Notion.Client;

public sealed record NotionRequestOptions(
    string Token,
    string NotionVersion = "2026-03-11",
    int MaxRetries = 2,
    Uri? BaseUri = null)
{
    public Uri EffectiveBaseUri { get; init; } = BaseUri ?? new Uri("https://api.notion.com");
}
