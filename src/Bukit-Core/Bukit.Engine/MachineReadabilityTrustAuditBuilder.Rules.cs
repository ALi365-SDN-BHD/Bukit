using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.PublishAuditRules;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static partial class MachineReadabilityTrustAuditBuilder
{
    private static int ComputeGeoScore(bool llmsTxtGenerated, bool llmsFullTxtGenerated, SeoAuditRoute[] geoRoutes, List<SeoAuditRoute> allRoutes)
    {
        var score = 0;

        if (llmsTxtGenerated)
        {
            score += 25;
        }

        if (llmsFullTxtGenerated)
        {
            score += 15;
        }

        if (geoRoutes.Length > 0)
        {
            score += 10;
        }

        var articleCount = allRoutes.Count(r => r.SchemaTypes.Any(t =>
            t is "BlogPosting" or "Article" or "NewsArticle"));
        var withSchemaType = geoRoutes.Count(r => r.SchemaTypes.Any(t =>
            t is "BlogPosting" or "Article" or "NewsArticle" or "FAQPage" or "HowTo"));
        if (articleCount > 0)
        {
            var ratio = (double)withSchemaType / articleCount;
            score += (int)(ratio * 15);
        }

        if (geoRoutes.Any(r => r.SchemaTypes.Contains("FAQPage") || r.SchemaTypes.Contains("HowTo")))
        {
            score += 15;
        }

        if (geoRoutes.Any(r => r.SchemaTypes.Contains("Person")))
        {
            score += 10;
        }

        if (geoRoutes.Any(r => r.SchemaTypes.Contains("SpeakableSpecification")))
        {
            score += 5;
        }

        if (geoRoutes.Any(r => r.SchemaTypes.Contains("WebPage") && r.SchemaTypes.Any(t => t is "WebPage") && geoRoutes.Length >= 2))
        {
            score += 5;
        }

        return Math.Min(score, 100);
    }

    private static void AnalyzeRouteModel(
        AppConfig config,
        SeoIndexEntry entry,
        SeoModel model,
        string outputDir,
        List<SeoAuditIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            issues.Add(Error("seo.title_missing", entry.Route.Url, "SEO title is missing."));
        }
        else if (model.Title.Length > TitleMaxLength)
        {
            issues.Add(Warning("seo.title_too_long", entry.Route.Url, $"SEO title length is {model.Title.Length}, recommended maximum is {TitleMaxLength}."));
        }

        if (string.IsNullOrWhiteSpace(model.Description))
        {
            issues.Add(Warning("seo.description_missing", entry.Route.Url, "SEO description is missing."));
        }
        else if (model.Description.Length > DescriptionMaxLength)
        {
            issues.Add(Warning("seo.description_too_long", entry.Route.Url, $"SEO description length is {model.Description.Length}, recommended maximum is {DescriptionMaxLength}."));
        }

        ImageMetadataReader.AnalyzeImage(config, "seo.og_image", entry.Route.Url, model.Og.Image, outputDir, issues);
        ImageMetadataReader.AnalyzeImage(config, "seo.twitter_image", entry.Route.Url, model.Twitter.Image, outputDir, issues);

        if (string.IsNullOrWhiteSpace(config.Site.Url) && IsAbsoluteHttpUrl(model.Canonical))
        {
            issues.Add(Warning("seo.site_url_missing_for_absolute_canonical", entry.Route.Url, "site.url is missing but canonical is absolute."));
        }

        if (!IsAbsoluteHttpUrl(model.Canonical))
        {
            issues.Add(Warning("seo.canonical_not_absolute", entry.Route.Url, $"Canonical URL should be absolute: {model.Canonical}."));
        }

        if (HasFragment(model.Canonical))
        {
            issues.Add(Warning("seo.canonical_has_fragment", entry.Route.Url, $"Canonical URL should not include a fragment: {model.Canonical}."));
        }

        if (Uri.TryCreate(model.Canonical, UriKind.Absolute, out var absoluteCanonical) &&
            absoluteCanonical.Scheme == Uri.UriSchemeHttp)
        {
            issues.Add(Warning("seo.canonical_http", entry.Route.Url, $"Prefer HTTPS canonical URLs where possible: {model.Canonical}."));
        }
    }

    private static void AnalyzeHtmlOutput(
        AppConfig config,
        SeoIndexEntry entry,
        PublishDocument document,
        string html,
        List<SeoAuditIssue> seoIssues,
        List<PublishAuditIssue> publishIssues)
    {
        if (!html.Contains("<head", StringComparison.OrdinalIgnoreCase) ||
            !html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            seoIssues.Add(Warning("seo.html_head_missing", entry.Route.Url, "HTML output has no standard <head>...</head>; inject mode cannot add SEO tags automatically."));
            return;
        }

        var renderMode = (config.Site.Seo.RenderMode ?? "inject").Trim();
        if (string.Equals(renderMode, "inject", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("rel=\"canonical\"", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("rel='canonical'", StringComparison.OrdinalIgnoreCase))
        {
            seoIssues.Add(Error("seo.inject_canonical_missing", entry.Route.Url, "Inject mode did not produce a canonical link in the HTML head."));
        }

        if (entry.Indexable)
        {
            SemanticHtmlAuditRules.Analyze(entry, document, html, publishIssues);
        }
    }

}
