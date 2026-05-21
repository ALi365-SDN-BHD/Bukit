using Bukit.Config;
using Bukit.Engine.Plugins;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class RobotsTxtWriter
{
    private static readonly string[] DefaultAiBots =
    {
        "GPTBot",
        "ChatGPT-User",
        "Google-Extended",
        "Claude-Web",
        "ClaudeBot",
        "Anthropic-AI",
        "PerplexityBot",
        "Cohere-AI",
        "CCBot",
        "Diffbot",
        "FacebookBot",
        "OAI-SearchBot"
    };

    internal static void WriteIfRequested(
        AppConfig config,
        string outputDir,
        string baseUrl,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex)
    {
        if (!config.Site.Seo.RobotsTxt.Enabled || string.IsNullOrWhiteSpace(config.Site.Url))
        {
            return;
        }

        var robotsPath = Path.Combine(outputDir, "robots.txt");
        if (File.Exists(robotsPath))
        {
            return;
        }

        var lines = new List<string>
        {
            "User-agent: *",
            "Allow: /"
        };
        if (seoIndex.Values.Any(x => x.Indexable))
        {
            lines.Add($"Sitemap: {SitemapGenerator.BuildAbsoluteUrl(config.Site.Url, baseUrl, "/sitemap.xml")}");
        }

        var geo = config.Site.Seo.Geo;
        if (geo.Enabled)
        {
            var aiBotMode = (geo.AiBotMode ?? "allow").Trim().ToLowerInvariant();
            if (aiBotMode == "selective")
            {
                if (geo.AiBotAllowList is { Count: > 0 })
                {
                    foreach (var bot in geo.AiBotAllowList)
                    {
                        lines.Add(string.Empty);
                        lines.Add($"User-agent: {bot}");
                        lines.Add("Allow: /");
                    }
                }

                if (geo.AiBotBlockList is { Count: > 0 })
                {
                    foreach (var bot in geo.AiBotBlockList)
                    {
                        lines.Add(string.Empty);
                        lines.Add($"User-agent: {bot}");
                        lines.Add("Disallow: /");
                    }
                }
            }
            else
            {
                var aiBots = GetAiBotList(geo, aiBotMode);
                foreach (var bot in aiBots)
                {
                    lines.Add(string.Empty);
                    lines.Add($"User-agent: {bot}");
                    lines.Add(aiBotMode == "block" ? "Disallow: /" : "Allow: /");
                }
            }
        }

        FileWriter.WriteUtf8(outputDir, "robots.txt", string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    private static IReadOnlyList<string> GetAiBotList(SeoGeoConfig geo, string aiBotMode)
    {
        if (geo.AiBotBlockList is { Count: > 0 })
        {
            var combined = new List<string>(DefaultAiBots);
            foreach (var blocked in geo.AiBotBlockList)
            {
                if (!combined.Contains(blocked, StringComparer.OrdinalIgnoreCase))
                {
                    combined.Add(blocked);
                }
            }

            return combined;
        }

        if (geo.AiBotAllowList is { Count: > 0 })
        {
            var allowSet = new HashSet<string>(geo.AiBotAllowList, StringComparer.OrdinalIgnoreCase);
            return DefaultAiBots.Where(b => allowSet.Contains(b)).ToList();
        }

        return DefaultAiBots;
    }
}
