using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Bukit.Config;
using Bukit.Engine.Plugins;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class SeoAuditReportWriter
{
    private const string ReportSchemaVersion = "1.0";
    private const string ReportSchema = "https://bukit.dev/schemas/seo-report.v1.json";
    private const int TitleMaxLength = 60;
    private const int DescriptionMaxLength = 160;

    internal static SeoAuditReport Write(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        ILogger logger)
    {
        var report = Build(config, outputDir, seoIndex, seoModels, requireHreflangTargets: false);
        WriteReport(config, outputDir, report, logger);
        return report;
    }

    internal static SeoAuditReport WriteMerged(
        AppConfig config,
        string outputDir,
        IReadOnlyList<BuildVariantResult> results,
        ILogger logger)
    {
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        var seoModels = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            foreach (var (key, entry) in result.SeoIndex)
            {
                var mergedKey = BuildMergedKey(result.Language, key);
                seoIndex[mergedKey] = entry with
                {
                    Route = new RouteInfo(
                        CombineBaseUrl(result.BaseUrl, entry.Route.Url),
                        Path.Combine(result.Language, entry.Route.OutputPath),
                        entry.Route.Template)
                };
            }

            foreach (var (key, model) in result.SeoModels)
            {
                seoModels[BuildMergedKey(result.Language, key)] = model;
            }
        }

        var report = Build(config, outputDir, seoIndex, seoModels, requireHreflangTargets: true);
        WriteReport(config, outputDir, report, logger);
        return report;
    }

    private static void WriteReport(AppConfig config, string outputDir, SeoAuditReport report, ILogger logger)
    {
        var json = JsonSerializer.Serialize(report, SeoAuditReportJsonContext.Default.SeoAuditReport);
        FileWriter.WriteUtf8(outputDir, Path.Combine(BuildReporter.ReportDirectoryName, "seo-report.json"), json + Environment.NewLine);

        WriteGeoReport(outputDir, report, logger);

        foreach (var issue in report.Issues)
        {
            var message = $"seo.audit severity={issue.Severity} code={issue.Code} route={issue.Route ?? "-"} message={issue.Message}";
            if (string.Equals(issue.Severity, "error", StringComparison.OrdinalIgnoreCase))
            {
                logger.Error(message);
            }
            else
            {
                logger.Warn(message);
            }
        }
    }

    private static void WriteGeoReport(string outputDir, SeoAuditReport report, ILogger logger)
    {
        if (report.Summary is null)
        {
            return;
        }

        var geoReport = new GeoReport(
            Schema: "https://bukit.dev/schemas/geo-report.v1.json",
            SchemaVersion: "1.0",
            GeneratedAt: DateTimeOffset.UtcNow,
            GeoScore: report.Summary.GeoScore,
            LlmsTxtGenerated: report.Summary.LlmsTxtGenerated,
            LlmsFullTxtGenerated: report.Summary.LlmsFullTxtGenerated,
            GeoEnhancedCount: report.Summary.GeoEnhancedCount,
            GeoEnhancedRoutes: report.Routes
                .Where(r => r.SchemaTypes.Any(t =>
                    t is "FAQPage" or "HowTo" or "Person" or "Article" or "NewsArticle" or "SpeakableSpecification"))
                .Select(r => new GeoRouteEntry(r.Url, r.SchemaTypes))
                .ToList());

        var json = JsonSerializer.Serialize(geoReport, GeoReportJsonContext.Default.GeoReport);
        FileWriter.WriteUtf8(outputDir, Path.Combine(BuildReporter.ReportDirectoryName, "geo-report.json"), json + Environment.NewLine);
    }

    internal static SeoAuditReport Build(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        bool requireHreflangTargets = true)
    {
        var sitemapText = ReadOptional(Path.Combine(outputDir, "sitemap.xml"));
        var searchText = ReadOptional(Path.Combine(outputDir, "search.json"));
        var rssText = ReadOptional(Path.Combine(outputDir, "rss.xml"));
        var robotsText = ReadOptional(Path.Combine(outputDir, "robots.txt"));

        var issues = new List<SeoAuditIssue>();
        AnalyzeSitemapXml(sitemapText, issues);
        var routes = new List<SeoAuditRoute>();
        var modelByCanonical = new Dictionary<string, (SeoIndexEntry Entry, SeoModel Model)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in seoIndex.OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            seoModels.TryGetValue(key, out var model);
            var schemaTypes = model is null ? Array.Empty<string>() : ExtractSchemaTypes(model.JsonLd, entry.Route.Url, issues);
            var outputPath = Path.Combine(outputDir, entry.Route.OutputPath);
            var outputExists = File.Exists(outputPath);
            if (!outputExists)
            {
                issues.Add(Error("seo.output_file_missing", entry.Route.Url, $"Output file is missing for route {entry.Route.Url}."));
            }
            else
            {
                AnalyzeHtmlOutput(config, entry, outputPath, issues);
            }

            var sitemapIncluded = entry.Indexable && ContainsInvariant(sitemapText, entry.Canonical);
            var searchIncluded = entry.Indexable && ContainsInvariant(searchText, entry.Route.Url);
            var rssIncluded = entry.Indexable && IsRssContent(entry) && ContainsInvariant(rssText, entry.Canonical);

            if (entry.Indexable && sitemapText is not null && !sitemapIncluded)
            {
                issues.Add(Warning("seo.sitemap_missing_url", entry.Route.Url, $"Indexable route is missing from sitemap: {entry.Canonical}."));
            }

            if (!entry.Indexable && sitemapText is not null && ContainsInvariant(sitemapText, entry.Canonical))
            {
                issues.Add(Error("seo.noindex_in_sitemap", entry.Route.Url, $"Noindex route appears in sitemap: {entry.Canonical}."));
            }

            if (model is not null)
            {
                AnalyzeRouteModel(config, entry, model, outputDir, issues);
                if (!string.Equals(model.Canonical, entry.Canonical, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(Warning("seo.canonical_sitemap_mismatch", entry.Route.Url, $"Model canonical does not match SeoIndex canonical: {model.Canonical} != {entry.Canonical}."));
                }

                modelByCanonical[model.Canonical] = (entry, model);
            }

            routes.Add(new SeoAuditRoute(
                Url: entry.Route.Url,
                OutputPath: entry.Route.OutputPath,
                Title: model?.Title,
                Description: model?.Description,
                Canonical: entry.Canonical,
                Robots: entry.Robots,
                Indexable: entry.Indexable,
                LastModified: entry.LastModified,
                ContentType: entry.ContentType,
                SourceItemId: entry.SourceItemId,
                SitemapIncluded: sitemapIncluded,
                SearchIncluded: searchIncluded,
                RssIncluded: rssIncluded,
                Alternates: model?.Alternates ?? Array.Empty<SeoAlternateModel>(),
                SchemaTypes: schemaTypes));
        }

        AnalyzeDuplicates(routes, issues);
        AnalyzeCanonicalTargets(routes, issues);
        AnalyzeHreflang(routes, modelByCanonical, issues, requireHreflangTargets);
        AnalyzeRobotsTxt(robotsText, routes, issues);

        var sortedRoutes = routes
            .OrderBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.OutputPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var sortedIssues = issues
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Route ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Message, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var geoEnhancedRoutes = sortedRoutes
            .Where(x => x.SchemaTypes.Any(t =>
                t is "FAQPage" or "HowTo" or "Person" or "Article" or "NewsArticle" or "SpeakableSpecification"))
            .ToArray();
        var llmsTxtGenerated = File.Exists(Path.Combine(outputDir, "llms.txt"));
        var llmsFullTxtGenerated = File.Exists(Path.Combine(outputDir, "llms-full.txt"));

        if (config.Site.Seo.Geo.Enabled && config.Site.Seo.Geo.LlmsTxt && !llmsTxtGenerated)
        {
            issues.Add(new SeoAuditIssue("warning", "geo.llms_txt_missing", null,
                "llms.txt was not generated. Ensure GEO is enabled and content has indexable routes."));
        }

        if (config.Site.Seo.Geo.Enabled && config.Site.Seo.Geo.LlmsFullTxt && !llmsFullTxtGenerated)
        {
            issues.Add(new SeoAuditIssue("warning", "geo.llms_full_txt_missing", null,
                "llms-full.txt was not generated. Check that llmsFullTxt is enabled and content is indexable."));
        }

        var geoScore = ComputeGeoScore(llmsTxtGenerated, llmsFullTxtGenerated, geoEnhancedRoutes, sortedRoutes);

        var summary = new SeoAuditSummary(
            RouteCount: sortedRoutes.Count,
            IndexableCount: sortedRoutes.Count(x => x.Indexable),
            NonIndexableCount: sortedRoutes.Count(x => !x.Indexable),
            ErrorCount: sortedIssues.Count(x => string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase)),
            WarningCount: sortedIssues.Count(x => string.Equals(x.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            LlmsTxtGenerated: llmsTxtGenerated,
            LlmsFullTxtGenerated: llmsFullTxtGenerated,
            GeoEnhancedCount: geoEnhancedRoutes.Length,
            GeoScore: geoScore);

        return new SeoAuditReport(
            Schema: ReportSchema,
            SchemaVersion: ReportSchemaVersion,
            GeneratedAt: DateTimeOffset.UtcNow,
            SiteName: config.Site.Name,
            SiteUrl: config.Site.Url,
            BaseUrl: config.Site.BaseUrl,
            Routes: sortedRoutes,
            Issues: sortedIssues,
            Summary: summary);
    }

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

        AnalyzeImage(config, "seo.og_image", entry.Route.Url, model.Og.Image, outputDir, issues);
        AnalyzeImage(config, "seo.twitter_image", entry.Route.Url, model.Twitter.Image, outputDir, issues);

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

    private static void AnalyzeHtmlOutput(AppConfig config, SeoIndexEntry entry, string outputPath, List<SeoAuditIssue> issues)
    {
        var html = File.ReadAllText(outputPath);
        if (!html.Contains("<head", StringComparison.OrdinalIgnoreCase) ||
            !html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning("seo.html_head_missing", entry.Route.Url, "HTML output has no standard <head>...</head>; inject mode cannot add SEO tags automatically."));
            return;
        }

        var renderMode = (config.Site.Seo.RenderMode ?? "inject").Trim();
        if (string.Equals(renderMode, "inject", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("rel=\"canonical\"", StringComparison.OrdinalIgnoreCase) &&
            !html.Contains("rel='canonical'", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Error("seo.inject_canonical_missing", entry.Route.Url, "Inject mode did not produce a canonical link in the HTML head."));
        }
    }

    private static void AnalyzeImage(AppConfig config, string codePrefix, string routeUrl, string? image, string outputDir, List<SeoAuditIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        if (IsAbsoluteHttpUrl(image))
        {
            if (TryMapSiteUrlToOutputPath(config.Site.Url, config.Site.BaseUrl, image, outputDir, out var localPath))
            {
                AnalyzeLocalImage(codePrefix, routeUrl, image, localPath, issues);
            }
            else
            {
                issues.Add(Warning($"{codePrefix}_external_unverified", routeUrl, $"Image is external and was not fetched during SEO audit: {image}."));
            }

            return;
        }

        issues.Add(Warning($"{codePrefix}_not_absolute", routeUrl, $"Search/social image should be an absolute URL: {image}."));
        var relative = image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        AnalyzeLocalImage(codePrefix, routeUrl, image, Path.Combine(outputDir, relative), issues);
    }

    private static void AnalyzeLocalImage(string codePrefix, string routeUrl, string image, string fullPath, List<SeoAuditIssue> issues)
    {
        if (!File.Exists(fullPath))
        {
            issues.Add(Warning($"{codePrefix}_missing_file", routeUrl, $"Image file was not found in build output: {image}."));
            return;
        }

        var metadata = TryReadImageMetadata(fullPath);
        if (metadata is null)
        {
            issues.Add(Warning($"{codePrefix}_mime_unknown", routeUrl, $"Image MIME/dimensions could not be detected: {image}."));
            return;
        }

        if (!metadata.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Warning($"{codePrefix}_mime_invalid", routeUrl, $"Image MIME is not an image type: {metadata.MimeType}."));
        }

        if (metadata.Width < 300 || metadata.Height < 157)
        {
            issues.Add(Warning($"{codePrefix}_too_small", routeUrl, $"Image dimensions are small for social/search previews: {metadata.Width}x{metadata.Height}."));
        }
    }

    private static void AnalyzeDuplicates(IReadOnlyList<SeoAuditRoute> routes, List<SeoAuditIssue> issues)
    {
        foreach (var group in routes.Where(x => !string.IsNullOrWhiteSpace(x.Title))
                     .GroupBy(x => x.Title!, StringComparer.OrdinalIgnoreCase)
                     .Where(x => HasNonAlternateDuplicate(x.ToArray())))
        {
            foreach (var route in group)
            {
                issues.Add(Warning("seo.title_duplicate", route.Url, $"SEO title is duplicated by {group.Count()} routes."));
            }
        }

        foreach (var group in routes.Where(x => !string.IsNullOrWhiteSpace(x.Description))
                     .GroupBy(x => x.Description!, StringComparer.OrdinalIgnoreCase)
                     .Where(x => HasNonAlternateDuplicate(x.ToArray())))
        {
            foreach (var route in group)
            {
                issues.Add(Warning("seo.description_duplicate", route.Url, $"SEO description is duplicated by {group.Count()} routes."));
            }
        }
    }

    private static bool HasNonAlternateDuplicate(IReadOnlyList<SeoAuditRoute> routes)
    {
        if (routes.Count < 2)
        {
            return false;
        }

        for (var i = 0; i < routes.Count; i++)
        {
            for (var j = i + 1; j < routes.Count; j++)
            {
                if (!AreHreflangAlternates(routes[i], routes[j]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool AreHreflangAlternates(SeoAuditRoute left, SeoAuditRoute right)
    {
        return left.Alternates.Any(x => string.Equals(x.Href, right.Canonical, StringComparison.OrdinalIgnoreCase)) &&
               right.Alternates.Any(x => string.Equals(x.Href, left.Canonical, StringComparison.OrdinalIgnoreCase));
    }

    private static void AnalyzeCanonicalTargets(IReadOnlyList<SeoAuditRoute> routes, List<SeoAuditIssue> issues)
    {
        var noindexCanonicals = routes.Where(x => !x.Indexable).Select(x => x.Canonical).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var route in routes.Where(x => x.Indexable && noindexCanonicals.Contains(x.Canonical)))
        {
            issues.Add(Error("seo.canonical_points_to_noindex", route.Url, $"Canonical points to a noindex route: {route.Canonical}."));
        }
    }

    private static void AnalyzeHreflang(
        IReadOnlyList<SeoAuditRoute> routes,
        IReadOnlyDictionary<string, (SeoIndexEntry Entry, SeoModel Model)> modelByCanonical,
        List<SeoAuditIssue> issues,
        bool requireTargets)
    {
        foreach (var route in routes.Where(x => x.Alternates.Count > 0))
        {
            if (!route.Alternates.Any(x => string.Equals(x.Href, route.Canonical, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Warning("seo.hreflang_self_missing", route.Url, "hreflang alternates must include the current page canonical URL."));
            }

            if (!route.Alternates.Any(x => string.Equals(x.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Warning("seo.hreflang_x_default_missing", route.Url, "hreflang alternates are missing x-default."));
            }

            foreach (var alternate in route.Alternates)
            {
                if (!IsValidHreflang(alternate.Hreflang))
                {
                    issues.Add(Warning("seo.hreflang_invalid_locale", route.Url, $"Invalid hreflang value: {alternate.Hreflang}."));
                }

                if (string.Equals(alternate.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!modelByCanonical.TryGetValue(alternate.Href, out var target))
                {
                    if (requireTargets)
                    {
                        issues.Add(Warning("seo.hreflang_target_missing", route.Url, $"hreflang target is not part of the generated URL inventory: {alternate.Href}."));
                    }

                    continue;
                }

                var hasReturnLink = target.Model.Alternates.Any(x =>
                    string.Equals(x.Href, route.Canonical, StringComparison.OrdinalIgnoreCase));
                if (!hasReturnLink)
                {
                    issues.Add(Warning("seo.hreflang_return_missing", route.Url, $"hreflang target does not link back to {route.Canonical}."));
                }
            }
        }
    }

    private static void AnalyzeRobotsTxt(string? robotsText, IReadOnlyList<SeoAuditRoute> routes, List<SeoAuditIssue> issues)
    {
        if (robotsText is null)
        {
            return;
        }

        if (!ContainsInvariant(robotsText, "Sitemap:"))
        {
            issues.Add(Warning("seo.robots_txt_sitemap_missing", null, "robots.txt does not declare a Sitemap URL."));
        }

        if (!robotsText.Split('\n').Any(x => x.Trim().Equals("Disallow: /", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        foreach (var route in routes.Where(x => x.Indexable))
        {
            issues.Add(Error("seo.robots_txt_blocks_indexable", route.Url, "robots.txt disallows all crawling while route is indexable."));
        }
    }

    private static void AnalyzeSitemapXml(string? sitemapText, List<SeoAuditIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(sitemapText))
        {
            return;
        }

        try
        {
            var doc = XDocument.Parse(sitemapText, LoadOptions.None);
            var rootName = doc.Root?.Name.LocalName;
            if (rootName is not ("urlset" or "sitemapindex"))
            {
                issues.Add(Error("seo.sitemap_xml_invalid_root", null, $"sitemap.xml root must be urlset or sitemapindex, got {rootName ?? "<none>"}."));
            }
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or InvalidOperationException)
        {
            issues.Add(Error("seo.sitemap_xml_invalid", null, $"sitemap.xml is not valid XML: {ex.Message}"));
        }
    }

    private static IReadOnlyList<string> ExtractSchemaTypes(IReadOnlyList<string> jsonLd, string routeUrl, List<SeoAuditIssue> issues)
    {
        var types = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var json in jsonLd)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                ExtractSchemaTypes(doc.RootElement, types);
                ValidateSchemaObject(doc.RootElement, routeUrl, issues);
                if (types.Count == 0)
                {
                    issues.Add(Warning("seo.json_ld_type_missing", routeUrl, "JSON-LD does not declare @type."));
                }
            }
            catch (JsonException ex)
            {
                issues.Add(Error("seo.json_ld_invalid", routeUrl, $"JSON-LD is not valid JSON: {ex.Message}"));
            }
        }

        return types.ToArray();
    }

    private static void ExtractSchemaTypes(JsonElement element, ISet<string> types)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@type", out var type))
            {
                if (type.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(type.GetString()))
                {
                    types.Add(type.GetString()!);
                }
                else if (type.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in type.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String))
                    {
                        types.Add(item.GetString()!);
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                ExtractSchemaTypes(property.Value, types);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ExtractSchemaTypes(item, types);
            }
        }
    }

    private static void ValidateSchemaObject(JsonElement element, string routeUrl, List<SeoAuditIssue> issues)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            ValidateSchemaNode(element, routeUrl, issues);
            foreach (var property in element.EnumerateObject())
            {
                ValidateSchemaObject(property.Value, routeUrl, issues);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ValidateSchemaObject(item, routeUrl, issues);
            }
        }
    }

    private static void ValidateSchemaNode(JsonElement node, string routeUrl, List<SeoAuditIssue> issues)
    {
        foreach (var type in ReadTypes(node))
        {
            switch (type)
            {
                case "WebSite":
                    if (node.TryGetProperty("@context", out _) ||
                        node.TryGetProperty("potentialAction", out _))
                    {
                        ValidateWebSite(node, routeUrl, issues);
                    }
                    break;
                case "BlogPosting":
                case "Article":
                    ValidateArticle(node, type, routeUrl, issues);
                    break;
                case "ItemList":
                    ValidateItemList(node, routeUrl, issues);
                    break;
            }
        }
    }

    private static IReadOnlyList<string> ReadTypes(JsonElement node)
    {
        if (!node.TryGetProperty("@type", out var type))
        {
            return Array.Empty<string>();
        }

        if (type.ValueKind == JsonValueKind.String)
        {
            var value = type.GetString();
            return string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value! };
        }

        if (type.ValueKind == JsonValueKind.Array)
        {
            return type.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                .Select(x => x.GetString()!)
                .ToArray();
        }

        return Array.Empty<string>();
    }

    private static void ValidateWebSite(JsonElement node, string routeUrl, List<SeoAuditIssue> issues)
    {
        if (!HasNonEmptyString(node, "name"))
        {
            issues.Add(Warning("seo.schema_website_name_missing", routeUrl, "WebSite JSON-LD should include a non-empty name."));
        }

        if (!HasAbsoluteUrl(node, "url"))
        {
            issues.Add(Warning("seo.schema_website_url_invalid", routeUrl, "WebSite JSON-LD should include an absolute url."));
        }

        if (!node.TryGetProperty("@context", out _) &&
            !node.TryGetProperty("potentialAction", out _))
        {
            return;
        }

        if (!node.TryGetProperty("potentialAction", out var action))
        {
            issues.Add(Warning("seo.schema_website_searchaction_missing", routeUrl, "WebSite JSON-LD should include potentialAction SearchAction when site search is enabled."));
            return;
        }

        ValidateSearchAction(action, routeUrl, issues);
    }

    private static void ValidateSearchAction(JsonElement action, string routeUrl, List<SeoAuditIssue> issues)
    {
        if (action.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in action.EnumerateArray())
            {
                if (IsSchemaType(item, "SearchAction"))
                {
                    ValidateSearchAction(item, routeUrl, issues);
                    return;
                }
            }

            issues.Add(Warning("seo.schema_searchaction_missing", routeUrl, "WebSite potentialAction does not contain a SearchAction."));
            return;
        }

        if (action.ValueKind != JsonValueKind.Object)
        {
            issues.Add(Warning("seo.schema_searchaction_invalid", routeUrl, "WebSite potentialAction must be an object or array."));
            return;
        }

        if (!IsSchemaType(action, "SearchAction"))
        {
            issues.Add(Warning("seo.schema_searchaction_type_missing", routeUrl, "WebSite potentialAction should declare @type SearchAction."));
        }

        if (!HasNonEmptyString(action, "target"))
        {
            issues.Add(Warning("seo.schema_searchaction_target_missing", routeUrl, "SearchAction should include a non-empty target."));
        }
        else if (!HasAbsoluteUrl(action, "target"))
        {
            issues.Add(Warning("seo.schema_searchaction_target_not_absolute", routeUrl, "SearchAction target should be an absolute URL."));
        }

        if (!HasNonEmptyString(action, "query-input"))
        {
            issues.Add(Warning("seo.schema_searchaction_query_input_missing", routeUrl, "SearchAction should include query-input."));
        }
    }

    private static void ValidateArticle(JsonElement node, string type, string routeUrl, List<SeoAuditIssue> issues)
    {
        var prefix = type.Equals("BlogPosting", StringComparison.OrdinalIgnoreCase)
            ? "seo.schema_blogposting"
            : "seo.schema_article";

        if (!HasNonEmptyString(node, "headline"))
        {
            issues.Add(Error($"{prefix}_headline_missing", routeUrl, $"{type} JSON-LD must include headline."));
        }

        if (!HasNonEmptyString(node, "datePublished"))
        {
            issues.Add(Error($"{prefix}_date_published_missing", routeUrl, $"{type} JSON-LD must include datePublished."));
        }

        if (!node.TryGetProperty("author", out var author) || IsEmptySchemaValue(author))
        {
            issues.Add(Warning($"{prefix}_author_missing", routeUrl, $"{type} JSON-LD should include author."));
        }

        if (!node.TryGetProperty("image", out var image) || IsEmptySchemaValue(image))
        {
            issues.Add(Warning($"{prefix}_image_missing", routeUrl, $"{type} JSON-LD should include image."));
        }
    }

    private static void ValidateItemList(JsonElement node, string routeUrl, List<SeoAuditIssue> issues)
    {
        if (!node.TryGetProperty("itemListElement", out var elements) ||
            elements.ValueKind != JsonValueKind.Array ||
            elements.GetArrayLength() == 0)
        {
            issues.Add(Error("seo.schema_itemlist_elements_missing", routeUrl, "ItemList JSON-LD must include a non-empty itemListElement array."));
            return;
        }

        var index = 0;
        foreach (var item in elements.EnumerateArray())
        {
            index++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Error("seo.schema_itemlist_item_invalid", routeUrl, $"ItemList item #{index} must be an object."));
                continue;
            }

            if (!item.TryGetProperty("position", out var position) || position.ValueKind != JsonValueKind.Number)
            {
                issues.Add(Error("seo.schema_itemlist_position_missing", routeUrl, $"ItemList item #{index} must include numeric position."));
            }

            if (!HasNonEmptyString(item, "name"))
            {
                issues.Add(Error("seo.schema_itemlist_name_missing", routeUrl, $"ItemList item #{index} must include name."));
            }

            if (!HasAbsoluteUrl(item, "url") && !HasAbsoluteUrl(item, "item"))
            {
                issues.Add(Warning("seo.schema_itemlist_url_missing", routeUrl, $"ItemList item #{index} should include an absolute url or item."));
            }
        }
    }

    private static bool IsValidHreflang(string value)
    {
        if (string.Equals(value, "x-default", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is < 1 or > 3 ||
            parts[0].Length is < 2 or > 3 ||
            !parts[0].All(char.IsLetter))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            return true;
        }

        var second = parts[1];
        var secondLooksLikeScript = second.Length == 4 && second.All(char.IsLetter);
        var secondLooksLikeRegion = second.Length == 2 && second.All(char.IsLetter);
        if (!secondLooksLikeScript && !secondLooksLikeRegion)
        {
            return false;
        }

        if (parts.Length == 2)
        {
            return true;
        }

        var third = parts[2];
        return secondLooksLikeScript && third.Length == 2 && third.All(char.IsLetter);
    }

    private static bool IsRssContent(SeoIndexEntry entry)
        => string.Equals(entry.ContentType, "post", StringComparison.OrdinalIgnoreCase);

    private static bool IsAbsoluteHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool TryMapSiteUrlToOutputPath(string? siteUrl, string baseUrl, string imageUrl, string outputDir, out string fullPath)
    {
        fullPath = string.Empty;
        var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl);
        var siteRoot = siteUrl?.Trim().TrimEnd('/') + normalizedBaseUrl;
        if (string.IsNullOrWhiteSpace(siteUrl) ||
            !Uri.TryCreate(siteRoot, UriKind.Absolute, out var siteUri) ||
            !Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) ||
            !string.Equals(siteUri.Scheme, imageUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(siteUri.Host, imageUri.Host, StringComparison.OrdinalIgnoreCase) ||
            siteUri.Port != imageUri.Port ||
            !imageUri.AbsolutePath.StartsWith(siteUri.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = Uri.UnescapeDataString(imageUri.AbsolutePath[siteUri.AbsolutePath.Length..])
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(relative))
        {
            return false;
        }

        fullPath = Path.Combine(outputDir, relative);
        return true;
    }

    private static ImageMetadata? TryReadImageMetadata(string path)
    {
        Span<byte> buffer = stackalloc byte[64];
        using var stream = File.OpenRead(path);
        var read = stream.Read(buffer);
        if (read < 10)
        {
            return null;
        }

        var bytes = buffer[..read];
        if (bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            read >= 24)
        {
            return new ImageMetadata("image/png", ReadBigEndianInt32(bytes[16..20]), ReadBigEndianInt32(bytes[20..24]));
        }

        if (bytes[0] == 0x47 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            read >= 10)
        {
            return new ImageMetadata("image/gif", ReadLittleEndianUInt16(bytes[6..8]), ReadLittleEndianUInt16(bytes[8..10]));
        }

        if (bytes[0] == 0x52 &&
            bytes[1] == 0x49 &&
            bytes[2] == 0x46 &&
            bytes[3] == 0x46 &&
            read >= 30 &&
            bytes[8] == 0x57 &&
            bytes[9] == 0x45 &&
            bytes[10] == 0x42 &&
            bytes[11] == 0x50)
        {
            return TryReadWebpMetadata(path);
        }

        if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return TryReadJpegMetadata(path);
        }

        return null;
    }

    private static ImageMetadata? TryReadJpegMetadata(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.ReadByte() != 0xFF || stream.ReadByte() != 0xD8)
        {
            return null;
        }

        while (stream.Position < stream.Length)
        {
            if (stream.ReadByte() != 0xFF)
            {
                continue;
            }

            int marker;
            do
            {
                marker = stream.ReadByte();
            }
            while (marker == 0xFF);

            if (marker < 0 || marker is 0xD8 or 0xD9)
            {
                continue;
            }

            var length = ReadBigEndianUInt16(stream);
            if (length < 2 || stream.Position + length - 2 > stream.Length)
            {
                return null;
            }

            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                stream.ReadByte();
                var height = ReadBigEndianUInt16(stream);
                var width = ReadBigEndianUInt16(stream);
                return new ImageMetadata("image/jpeg", width, height);
            }

            stream.Seek(length - 2, SeekOrigin.Current);
        }

        return null;
    }

    private static ImageMetadata? TryReadWebpMetadata(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 30)
        {
            return null;
        }

        var chunk = System.Text.Encoding.ASCII.GetString(bytes, 12, 4);
        if (chunk == "VP8X" && bytes.Length >= 30)
        {
            var width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
            var height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
            return new ImageMetadata("image/webp", width, height);
        }

        if (chunk == "VP8 " && bytes.Length >= 30)
        {
            var width = ReadLittleEndianUInt16(bytes.AsSpan(26, 2)) & 0x3FFF;
            var height = ReadLittleEndianUInt16(bytes.AsSpan(28, 2)) & 0x3FFF;
            return new ImageMetadata("image/webp", width, height);
        }

        if (chunk == "VP8L" && bytes.Length >= 25)
        {
            var b0 = bytes[21];
            var b1 = bytes[22];
            var b2 = bytes[23];
            var b3 = bytes[24];
            var width = 1 + (((b1 & 0x3F) << 8) | b0);
            var height = 1 + (((b3 & 0x0F) << 10) | (b2 << 2) | ((b1 & 0xC0) >> 6));
            return new ImageMetadata("image/webp", width, height);
        }

        return null;
    }

    private static int ReadBigEndianInt32(ReadOnlySpan<byte> value)
        => (value[0] << 24) | (value[1] << 16) | (value[2] << 8) | value[3];

    private static int ReadLittleEndianUInt16(ReadOnlySpan<byte> value)
        => value[0] | (value[1] << 8);

    private static int ReadBigEndianUInt16(Stream stream)
    {
        var high = stream.ReadByte();
        var low = stream.ReadByte();
        return high < 0 || low < 0 ? 0 : (high << 8) | low;
    }

    private static bool HasFragment(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return !string.IsNullOrWhiteSpace(absolute.Fragment);
        }

        return value.Contains('#', StringComparison.Ordinal);
    }

    private static bool HasNonEmptyString(JsonElement node, string property)
        => node.TryGetProperty(property, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasAbsoluteUrl(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return IsAbsoluteHttpUrl(value.GetString() ?? string.Empty);
        }

        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("@id", out var id) &&
            id.ValueKind == JsonValueKind.String)
        {
            return IsAbsoluteHttpUrl(id.GetString() ?? string.Empty);
        }

        return false;
    }

    private static bool IsSchemaType(JsonElement node, string expectedType)
    {
        return node.ValueKind == JsonValueKind.Object &&
               ReadTypes(node).Any(x => string.Equals(x, expectedType, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEmptySchemaValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Array => value.GetArrayLength() == 0,
            JsonValueKind.Object => !value.EnumerateObject().Any(),
            _ => false
        };

    private static int SeverityRank(string severity)
        => string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static string? ReadOptional(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    private static bool ContainsInvariant(string? haystack, string needle)
        => haystack?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private static SeoAuditIssue Error(string code, string? route, string message) => new("error", code, route, message);

    private static SeoAuditIssue Warning(string code, string? route, string message) => new("warning", code, route, message);

    private static string BuildMergedKey(string language, string key) => language + "/" + key;

    private sealed record ImageMetadata(string MimeType, int Width, int Height);

    private static string CombineBaseUrl(string baseUrl, string routeUrl)
    {
        var b = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        var r = routeUrl.StartsWith('/') ? routeUrl : "/" + routeUrl;
        return string.IsNullOrWhiteSpace(b) ? r : b + r;
    }

}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SeoAuditReport))]
internal sealed partial class SeoAuditReportJsonContext : JsonSerializerContext;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(GeoReport))]
internal sealed partial class GeoReportJsonContext : JsonSerializerContext;

internal sealed record GeoReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    int GeoScore,
    bool LlmsTxtGenerated,
    bool LlmsFullTxtGenerated,
    int GeoEnhancedCount,
    IReadOnlyList<GeoRouteEntry> GeoEnhancedRoutes);

internal sealed record GeoRouteEntry(string Url, IReadOnlyList<string> SchemaTypes);

internal sealed record SeoAuditReport(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string SiteName,
    string? SiteUrl,
    string BaseUrl,
    IReadOnlyList<SeoAuditRoute> Routes,
    IReadOnlyList<SeoAuditIssue> Issues,
    SeoAuditSummary Summary);

internal sealed record SeoAuditRoute(
    string Url,
    string OutputPath,
    string? Title,
    string? Description,
    string Canonical,
    string? Robots,
    bool Indexable,
    DateTimeOffset LastModified,
    string? ContentType,
    string? SourceItemId,
    bool SitemapIncluded,
    bool SearchIncluded,
    bool RssIncluded,
    IReadOnlyList<SeoAlternateModel> Alternates,
    IReadOnlyList<string> SchemaTypes);

internal sealed record SeoAuditIssue(string Severity, string Code, string? Route, string Message);

internal sealed record SeoAuditSummary(
    int RouteCount,
    int IndexableCount,
    int NonIndexableCount,
    int ErrorCount,
    int WarningCount,
    bool LlmsTxtGenerated,
    bool LlmsFullTxtGenerated,
    int GeoEnhancedCount,
    int GeoScore);
