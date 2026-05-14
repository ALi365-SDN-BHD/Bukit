using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SeoDiagnostics
{
    internal static void AnalyzeIndex(
        AppConfig config,
        IReadOnlyDictionary<string, SeoIndexEntry> index,
        IReadOnlyDictionary<string, SeoModel> models,
        ILogger logger)
    {
        if (!IsEnabled(config))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(config.Site.Url))
        {
            Report(config, logger, "seo.site_url_missing absolute SEO URLs require site.url.");
        }

        foreach (var group in index.Values
                     .Where(x => !string.IsNullOrWhiteSpace(x.Canonical))
                     .GroupBy(x => x.Canonical, StringComparer.OrdinalIgnoreCase)
                     .Where(x => x.Count() > 1))
        {
            Report(config, logger, $"seo.canonical_duplicate_index canonical={group.Key} routes={string.Join(",", group.Select(x => x.Route.Url))}");
        }

        foreach (var entry in index.Values)
        {
            if (HasDoubleSlashInPath(entry.Canonical))
            {
                Report(config, logger, $"seo.canonical_double_slash url={entry.Canonical}");
            }

            if (!string.IsNullOrWhiteSpace(config.Site.Url) &&
                entry.Canonical.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !entry.Canonical.StartsWith(config.Site.Url.Trim().TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                Report(config, logger, $"seo.canonical_external route={entry.Route.Url} canonical={entry.Canonical}");
            }

            if (models.TryGetValue(BuildPathUtils.NormalizeRelPath(entry.Route.OutputPath), out var model) &&
                model.Alternates.Count > 0 &&
                !model.Alternates.Any(x => string.Equals(x.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase)))
            {
                Report(config, logger, $"seo.hreflang_x_default_missing route={entry.Route.Url}");
            }
        }
    }

    internal static string AnalyzeHtml(AppConfig config, RouteInfo route, SeoModel? seo, string html, ILogger logger)
    {
        if (!IsEnabled(config) || seo is null)
        {
            return html;
        }

        var head = ExtractHead(html);
        if (head is null)
        {
            Report(config, logger, $"seo.head_missing route={route.Url}");
            return html;
        }

        var canonicalCount = Count(CanonicalRegex(), head);
        if (canonicalCount == 0)
        {
            Report(config, logger, $"seo.canonical_missing route={route.Url}");
        }
        else if (canonicalCount > 1)
        {
            Report(config, logger, $"seo.canonical_duplicate route={route.Url} count={canonicalCount}");
        }

        if (!string.IsNullOrWhiteSpace(seo.Robots) && Count(RobotsRegex(), head) == 0)
        {
            Report(config, logger, $"seo.robots_missing route={route.Url}");
        }

        if (seo.Alternates.Count > 0 && Count(AlternateRegex(), head) == 0)
        {
            Report(config, logger, $"seo.hreflang_missing route={route.Url}");
        }

        if (seo.JsonLd.Count > 0 && Count(JsonLdRegex(), head) == 0)
        {
            Report(config, logger, $"seo.json_ld_missing route={route.Url}");
        }

        return html;
    }

    private static bool IsEnabled(AppConfig config)
        => !string.Equals(config.Site.Seo.Diagnostics, "off", StringComparison.OrdinalIgnoreCase);

    private static void Report(AppConfig config, ILogger logger, string message)
    {
        if (string.Equals(config.Site.Seo.Diagnostics, "strict", StringComparison.OrdinalIgnoreCase))
        {
            logger.Error(message);
            throw new ConfigException(message);
        }

        logger.Warn(message);
    }

    private static string? ExtractHead(string html)
    {
        var match = HeadRegex().Match(html);
        return match.Success ? match.Value : null;
    }

    private static int Count(Regex regex, string value) => regex.Matches(value).Count;

    private static bool HasDoubleSlashInPath(string canonical)
    {
        if (!Uri.TryCreate(canonical, UriKind.Absolute, out var uri))
        {
            return canonical.Contains("//", StringComparison.Ordinal);
        }

        return uri.AbsolutePath.Contains("//", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<head\b[^>]*>.*?</head>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HeadRegex();

    [GeneratedRegex(@"<link\b(?=[^>]*\brel\s*=\s*[""']?canonical[""']?)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalRegex();

    [GeneratedRegex(@"<meta\b(?=[^>]*\bname\s*=\s*[""']?robots[""']?)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RobotsRegex();

    [GeneratedRegex(@"<link\b(?=[^>]*\brel\s*=\s*[""']?alternate[""']?)(?=[^>]*\bhreflang\s*=)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AlternateRegex();

    [GeneratedRegex(@"<script\b(?=[^>]*\btype\s*=\s*[""']application/ld\+json[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdRegex();
}
