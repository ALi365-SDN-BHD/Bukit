using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class RobotsTxtWriter
{
    internal static void WriteIfRequested(
        AppConfig config,
        string outputDir,
        string baseUrl,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyList<string>? sitemapUrls = null,
        ILogger? logger = null)
    {
        var robotsPath = Path.Combine(outputDir, "robots.txt");
        var geo = config.Site.Seo.Geo;
        var aiPolicyConfigured = HasConfiguredAiPolicy(geo);
        var contentSignalLine = BuildContentSignalLine(config.Site.Seo.RobotsTxt);
        if (!config.Site.Seo.RobotsTxt.Enabled || string.IsNullOrWhiteSpace(config.Site.Url))
        {
            if (contentSignalLine is not null)
            {
                var reason = !config.Site.Seo.RobotsTxt.Enabled ? "robots_txt_disabled" : "site_url_missing";
                logger?.Warn($"event=robots.content_signal_inactive reason={reason} message=site.seo.robotsTxt.contentSignal only takes effect when site.seo.robotsTxt.enabled is true and site.url is set.");
            }

            if (aiPolicyConfigured)
            {
                var reason = !config.Site.Seo.RobotsTxt.Enabled ? "robots_txt_disabled"
                    : !geo.Enabled ? "geo_disabled"
                    : "site_url_missing";
                LogInactivePolicy(logger, reason);
            }

            return;
        }

        if (File.Exists(robotsPath))
        {
            if (contentSignalLine is not null)
            {
                logger?.Warn("event=robots.content_signal_inactive reason=static_robots_txt message=A static robots.txt is kept as is, so site.seo.robotsTxt.contentSignal is not written. Add the Content-Signal line to that file or remove it.");
            }

            if (aiPolicyConfigured)
            {
                logger?.Info("event=robots.ai_policy_inactive reason=static_robots_txt message=A static robots.txt is kept as is, so the configured AI bot policy is not written.");
            }

            return;
        }

        if (aiPolicyConfigured && !geo.Enabled)
        {
            LogInactivePolicy(logger, "geo_disabled");
        }

        var lines = new List<string>
        {
            "User-agent: *",
            "Allow: /"
        };
        if (contentSignalLine is not null)
        {
            lines.Add(contentSignalLine);
        }

        if (seoIndex.Values.Any(x => x.Indexable))
        {
            foreach (var sitemapUrl in sitemapUrls ?? [SitemapGenerator.BuildAbsoluteUrl(config.Site.Url, baseUrl, "/sitemap.xml")])
            {
                lines.Add($"Sitemap: {sitemapUrl}");
            }
        }

        if (geo.Enabled)
        {
            var rules = BuildAiBotRules(geo);
            WarnIfSelectiveBlocksAiSearch(geo, rules, logger);
            LogGroundingTradeoff(rules, logger);
            foreach (var (userAgent, allow) in rules)
            {
                lines.Add(string.Empty);
                lines.Add($"User-agent: {userAgent}");
                lines.Add(allow ? "Allow: /" : "Disallow: /");
            }
        }

        FileWriter.WriteUtf8(outputDir, "robots.txt", string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    /// <summary>
    /// Builds one robots.txt group per AI crawler.
    /// <list type="bullet">
    /// <item><c>allow</c>: every known crawler is allowed.</item>
    /// <item><c>block-training</c>: training crawlers are blocked; AI search and user-fetch crawlers stay allowed, so the site can still be cited.</item>
    /// <item><c>block</c>: every known crawler is blocked, which also removes the site from AI search answers.</item>
    /// <item><c>selective</c>: with an allow list, only listed crawlers are allowed and other known crawlers are blocked; with only a block list, just those are blocked.</item>
    /// </list>
    /// In every mode, <c>aiBotAllowList</c> entries are allowed and <c>aiBotBlockList</c> entries are blocked;
    /// a crawler on both lists is blocked. Unknown names in either list are added as extra groups.
    /// </summary>
    internal static IReadOnlyList<(string UserAgent, bool Allow)> BuildAiBotRules(SeoGeoConfig geo)
    {
        var mode = NormalizeMode(geo.AiBotMode);
        var allowList = geo.AiBotAllowList ?? [];
        var blockList = geo.AiBotBlockList ?? [];
        if (mode == "selective" && allowList.Count == 0)
        {
            // Only a block list: write just those groups; everything else follows "User-agent: *".
            return [.. Dedupe(blockList).Select(agent => (agent, false))];
        }

        var allowSet = new HashSet<string>(Dedupe(allowList), StringComparer.OrdinalIgnoreCase);
        var blockSet = new HashSet<string>(Dedupe(blockList), StringComparer.OrdinalIgnoreCase);
        var agents = AiCrawlerCatalog.All.Select(crawler => crawler.UserAgent)
            .Concat(allowList)
            .Concat(blockList);
        var purposes = AiCrawlerCatalog.All.ToDictionary(crawler => crawler.UserAgent, crawler => crawler.Purpose, StringComparer.OrdinalIgnoreCase);
        return
        [
            .. Dedupe(agents).Select(agent =>
            {
                if (blockSet.Contains(agent))
                {
                    return (agent, false);
                }

                if (allowSet.Contains(agent))
                {
                    return (agent, true);
                }

                var allowed = mode switch
                {
                    "block" or "selective" => false,
                    "block-training" => !purposes.TryGetValue(agent, out var purpose) ||
                                        purpose is not (AiCrawlerPurpose.Training or AiCrawlerPurpose.TrainingAndGrounding),
                    _ => true
                };
                return (agent, allowed);
            })
        ];
    }

    /// <summary>
    /// Builds the <c>Content-Signal:</c> line for the <c>User-agent: *</c> group, listing only the
    /// signals that are set, or null when none is set.
    /// </summary>
    internal static string? BuildContentSignalLine(SeoRobotsTxtConfig robots)
    {
        var parts = new List<string>(3);
        Add("search", robots.ContentSignalSearch);
        Add("ai-input", robots.ContentSignalAiInput);
        Add("ai-train", robots.ContentSignalAiTrain);
        return parts.Count == 0 ? null : "Content-Signal: " + string.Join(", ", parts);

        void Add(string name, bool? value)
        {
            if (value is { } set)
            {
                parts.Add($"{name}={(set ? "yes" : "no")}");
            }
        }
    }

    // Any non-default AI crawler setting, whether or not GEO is enabled.
    private static bool HasConfiguredAiPolicy(SeoGeoConfig geo)
        => NormalizeMode(geo.AiBotMode) != "allow" ||
           geo.AiBotAllowList is { Count: > 0 } ||
           geo.AiBotBlockList is { Count: > 0 };

    private static void LogInactivePolicy(ILogger? logger, string reason)
        => logger?.Warn($"event=robots.ai_policy_inactive reason={reason} message=site.seo.geo.aiBotMode and AI bot lists only take effect when site.seo.geo.enabled and site.seo.robotsTxt.enabled are true and site.url is set.");

    // Selective mode blocks every known crawler that is not on the allow list, AI search crawlers
    // included. Say so, because that removes the site from AI search answers.
    private static void WarnIfSelectiveBlocksAiSearch(SeoGeoConfig geo, IReadOnlyList<(string UserAgent, bool Allow)> rules, ILogger? logger)
    {
        if (logger is null || NormalizeMode(geo.AiBotMode) != "selective")
        {
            return;
        }

        var explicitlyBlocked = new HashSet<string>((geo.AiBotBlockList ?? []).Select(agent => agent.Trim()), StringComparer.OrdinalIgnoreCase);
        var visibility = new HashSet<string>(AiCrawlerCatalog.VisibilityUserAgents, StringComparer.OrdinalIgnoreCase);
        var implicitlyBlocked = rules
            .Where(rule => !rule.Allow && visibility.Contains(rule.UserAgent) && !explicitlyBlocked.Contains(rule.UserAgent))
            .Select(rule => rule.UserAgent)
            .ToArray();
        if (implicitlyBlocked.Length > 0)
        {
            logger.Warn($"event=robots.ai_search_blocked mode=selective agents={string.Join(',', implicitlyBlocked)} message=These AI search and user-fetch crawlers are not on site.seo.geo.aiBotAllowList, so they are blocked and the site cannot be cited in their AI answers. Add them to aiBotAllowList to stay citable.");
        }
    }

    // Some tokens control training and grounding at once, so blocking them also limits how the
    // site is used in that vendor's AI answers. Say so instead of treating the block as neutral.
    private static void LogGroundingTradeoff(IReadOnlyList<(string UserAgent, bool Allow)> rules, ILogger? logger)
    {
        if (logger is null)
        {
            return;
        }

        var groundingTokens = new HashSet<string>(
            AiCrawlerCatalog.All.Where(crawler => crawler.Purpose == AiCrawlerPurpose.TrainingAndGrounding).Select(crawler => crawler.UserAgent),
            StringComparer.OrdinalIgnoreCase);
        var blocked = rules.Where(rule => !rule.Allow && groundingTokens.Contains(rule.UserAgent)).Select(rule => rule.UserAgent).ToArray();
        if (blocked.Length > 0)
        {
            logger.Info($"event=robots.ai_grounding_blocked agents={string.Join(',', blocked)} message=These tokens control both model training and grounding in the vendor's AI answers (for Google-Extended: Gemini Apps and Vertex AI), so blocking them also opts the site out of that grounding. Search indexing by the vendor's main crawler is not affected.");
        }
    }

    private static string NormalizeMode(string? mode) => (mode ?? "allow").Trim().ToLowerInvariant();

    private static IEnumerable<string> Dedupe(IEnumerable<string> agents)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents)
        {
            var trimmed = agent?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && seen.Add(trimmed))
            {
                yield return trimmed;
            }
        }
    }
}

/// <summary>What an AI crawler fetches pages for.</summary>
internal enum AiCrawlerPurpose
{
    /// <summary>Collects content to train models. Blocking it does not hide the site from AI answers.</summary>
    Training,

    /// <summary>
    /// A control token that covers both model training and grounding in the vendor's AI answers.
    /// <c>block-training</c> blocks it, because it is the only way to opt out of that vendor's training,
    /// and the build logs <c>event=robots.ai_grounding_blocked</c> to disclose the grounding tradeoff.
    /// </summary>
    TrainingAndGrounding,

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
        // Controls Gemini training and grounding in Gemini Apps and Vertex AI; not Google Search.
        new("Google-Extended", AiCrawlerPurpose.TrainingAndGrounding),
        new("Applebot-Extended", AiCrawlerPurpose.Training),
        new("Meta-ExternalAgent", AiCrawlerPurpose.Training),
        new("CCBot", AiCrawlerPurpose.Training),
        new("Bytespider", AiCrawlerPurpose.Training),
        new("Amazonbot", AiCrawlerPurpose.Training),
        new("cohere-ai", AiCrawlerPurpose.Training),
        new("Diffbot", AiCrawlerPurpose.Training),
        new("FacebookBot", AiCrawlerPurpose.Training),
        // Legacy Anthropic tokens, kept so block modes still cover sites that see them.
        new("anthropic-ai", AiCrawlerPurpose.Training),
        new("Claude-Web", AiCrawlerPurpose.Training),
        new("OAI-SearchBot", AiCrawlerPurpose.Search),
        new("Claude-SearchBot", AiCrawlerPurpose.Search),
        new("PerplexityBot", AiCrawlerPurpose.Search),
        new("ChatGPT-User", AiCrawlerPurpose.UserFetch),
        new("Claude-User", AiCrawlerPurpose.UserFetch),
        new("Perplexity-User", AiCrawlerPurpose.UserFetch)
    ];

    /// <summary>
    /// Crawlers that decide whether the site can be cited in AI search answers. Training-and-grounding
    /// tokens are left out: blocking them is the documented way to opt out of training, and the
    /// robots writer discloses their grounding tradeoff with <c>event=robots.ai_grounding_blocked</c>.
    /// </summary>
    internal static IReadOnlyList<string> VisibilityUserAgents { get; } =
    [
        .. All.Where(crawler => crawler.Purpose is AiCrawlerPurpose.Search or AiCrawlerPurpose.UserFetch).Select(crawler => crawler.UserAgent)
    ];
}
