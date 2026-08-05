using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
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

        RunGeoDiagnostics(models, logger, config);
    }

    internal static string AnalyzeHtml(AppConfig config, RouteInfo route, SeoModel? seo, string html, ILogger logger)
    {
        if (!IsEnabled(config) || seo is null)
        {
            return html;
        }

        var titleInspection = HtmlDocumentTitleInspector.Inspect(html);
        if (!titleInspection.HasHead)
        {
            Report(config, logger, $"seo.head_missing route={route.Url}");
            Report(config, logger, $"seo.document_title_missing route={route.Url}");
            return html;
        }

        AnalyzeDocumentTitle(config, route, seo, titleInspection, logger);
        var head = titleInspection.HeadHtml!;

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

    private static void RunGeoDiagnostics(
        IReadOnlyDictionary<string, SeoModel> models,
        ILogger logger,
        AppConfig config)
    {
        foreach (var (key, model) in models)
        {
            if (model.FaqItems is { Count: > 0 })
            {
                foreach (var faq in model.FaqItems)
                {
                    if (string.IsNullOrWhiteSpace(faq.Question))
                    {
                        Report(config, logger, $"geo.faq_empty_question route={key}");
                    }

                    if (string.IsNullOrWhiteSpace(faq.Answer))
                    {
                        Report(config, logger, $"geo.faq_empty_answer route={key}");
                    }
                }
            }

            if (model.HowToSteps is { Count: > 0 })
            {
                var indexPath = 0;
                foreach (var step in model.HowToSteps)
                {
                    if (string.IsNullOrWhiteSpace(step.Name))
                    {
                        Report(config, logger, $"geo.howto_step_empty_name route={key} step={indexPath}");
                    }

                    if (string.IsNullOrWhiteSpace(step.Text))
                    {
                        Report(config, logger, $"geo.howto_step_empty_text route={key} step={indexPath}");
                    }

                    indexPath++;
                }
            }

            if (model.Citations is { Count: > 0 })
            {
                foreach (var citation in model.Citations)
                {
                    if (!Uri.TryCreate(citation.Url, UriKind.Absolute, out _))
                    {
                        Report(config, logger, $"geo.citation_url_invalid route={key} url={citation.Url}");
                    }

                    if (citation.Relation is not ("citation" or "based-on"))
                    {
                        Report(config, logger, $"geo.citation_relation_invalid route={key} relation={citation.Relation}");
                    }
                }
            }

            if (model.GeoAuthor is { } author && (author.SameAs is null || author.SameAs.Count == 0))
            {
                Report(config, logger, $"geo.author_no_sameas route={key} author={author.Name}");
            }

            if (!string.IsNullOrWhiteSpace(model.SpeakableXPath) &&
                !model.SpeakableXPath.StartsWith("/", StringComparison.Ordinal))
            {
                Report(config, logger, $"geo.speakable_path_invalid route={key} xpath={model.SpeakableXPath}");
            }

            if (model.Article.PublishedTime is not null &&
                string.IsNullOrWhiteSpace(model.SchemaType) &&
                model.FaqItems is null &&
                model.HowToSteps is null &&
                model.Citations is null &&
                model.GeoAuthor is null &&
                string.IsNullOrWhiteSpace(model.SpeakableXPath))
            {
                Report(config, logger, $"geo.schema_type_missing route={key}");
            }
        }
    }

    private static void Report(AppConfig config, ILogger logger, string message)
    {
        if (string.Equals(config.Site.Seo.Diagnostics, "strict", StringComparison.OrdinalIgnoreCase))
        {
            logger.Error(message);
            throw new ConfigException(message, DiagnosticCode.ConfigInvalidValue);
        }

        logger.Warn(message);
    }

    private static void AnalyzeDocumentTitle(
        AppConfig config,
        RouteInfo route,
        SeoModel seo,
        HtmlDocumentTitleInspection inspection,
        ILogger logger)
    {
        if (inspection.Count == 0)
        {
            Report(config, logger, $"seo.document_title_missing route={route.Url}");
            return;
        }

        if (inspection.Count > 1)
        {
            Report(config, logger, $"seo.document_title_multiple route={route.Url} count={inspection.Count}");
        }

        if (inspection.Titles.Any(string.IsNullOrWhiteSpace))
        {
            Report(config, logger, $"seo.document_title_empty route={route.Url}");
        }

        var actual = inspection.PrimaryTitle!;
        if (actual.Length > 60)
        {
            Report(config, logger, $"seo.document_title_too_long route={route.Url} length={actual.Length}");
        }

        var expected = SeoDocumentTitleResolver.ResolveEffective(seo);
        if (!string.IsNullOrWhiteSpace(actual) &&
            !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            Report(config, logger, $"seo.document_title_mismatch route={route.Url} expected={expected} actual={actual}");
        }
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

    [GeneratedRegex(@"<link\b(?=[^>]*\brel\s*=\s*[""']?canonical[""']?)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalRegex();

    [GeneratedRegex(@"<meta\b(?=[^>]*\bname\s*=\s*[""']?robots[""']?)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RobotsRegex();

    [GeneratedRegex(@"<link\b(?=[^>]*\brel\s*=\s*[""']?alternate[""']?)(?=[^>]*\bhreflang\s*=)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AlternateRegex();

    [GeneratedRegex(@"<script\b(?=[^>]*\btype\s*=\s*[""']application/ld\+json[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JsonLdRegex();
}
