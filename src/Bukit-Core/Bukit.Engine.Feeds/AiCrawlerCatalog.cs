namespace Bukit.Engine;

/// <summary>What an AI crawler fetches pages for.</summary>
internal enum AiCrawlerPurpose
{
    /// <summary>Collects content to train models. Blocking it does not hide the site from AI answers.</summary>
    Training,

    /// <summary>Indexes pages so AI search can find and cite them.</summary>
    Search,

    /// <summary>Fetches a page when a user asks an assistant about it.</summary>
    UserFetch
}

internal sealed record AiCrawler(string UserAgent, AiCrawlerPurpose Purpose);

/// <summary>
/// The single list of known AI crawler user-agent tokens, shared by robots.txt
/// generation and the publish audit. Vendors rename and add crawlers often, so
/// review this list against their published crawler documentation.
/// </summary>
internal static class AiCrawlerCatalog
{
    internal static readonly IReadOnlyList<AiCrawler> All =
    [
        new("GPTBot", AiCrawlerPurpose.Training),
        new("ClaudeBot", AiCrawlerPurpose.Training),
        new("Google-Extended", AiCrawlerPurpose.Training),
        new("Applebot-Extended", AiCrawlerPurpose.Training),
        new("Meta-ExternalAgent", AiCrawlerPurpose.Training),
        new("CCBot", AiCrawlerPurpose.Training),
        new("Bytespider", AiCrawlerPurpose.Training),
        new("Amazonbot", AiCrawlerPurpose.Training),
        new("cohere-ai", AiCrawlerPurpose.Training),
        new("Diffbot", AiCrawlerPurpose.Training),
        new("OAI-SearchBot", AiCrawlerPurpose.Search),
        new("Claude-SearchBot", AiCrawlerPurpose.Search),
        new("PerplexityBot", AiCrawlerPurpose.Search),
        new("ChatGPT-User", AiCrawlerPurpose.UserFetch),
        new("Claude-User", AiCrawlerPurpose.UserFetch),
        new("Perplexity-User", AiCrawlerPurpose.UserFetch)
    ];

    /// <summary>Crawlers that decide whether the site can appear in AI search answers.</summary>
    internal static IReadOnlyList<string> VisibilityUserAgents { get; } = All
        .Where(crawler => crawler.Purpose != AiCrawlerPurpose.Training)
        .Select(crawler => crawler.UserAgent)
        .ToArray();
}
