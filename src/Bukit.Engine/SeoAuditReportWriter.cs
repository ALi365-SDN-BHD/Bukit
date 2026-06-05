using System.Text.Json;
using System.Xml.Linq;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.PublishAuditRules;
using Bukit.Rendering;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SeoAuditReportWriter
{
    private const int TitleMaxLength = 60;
    private const int DescriptionMaxLength = 160;

    internal static SeoAuditReport Write(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        ILogger logger)
        => Write(config, outputDir, seoIndex, seoModels, null, logger);

    internal static SeoAuditReport Write(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        CanonicalContentGraph? contentGraph,
        ILogger logger)
    {
        var result = MachineReadabilityTrustAuditBuilder.Build(config, outputDir, seoIndex, seoModels, contentGraph, requireHreflangTargets: false);
        WriteReport(outputDir, result, logger);
        return result.SeoReport;
    }

    internal static SeoAuditReport WriteMerged(
        AppConfig config,
        string outputDir,
        IReadOnlyList<BuildVariantResult> results,
        ILogger logger)
    {
        var seoIndex = new Dictionary<string, SeoIndexEntry>(StringComparer.OrdinalIgnoreCase);
        var seoModels = new Dictionary<string, SeoModel>(StringComparer.OrdinalIgnoreCase);
        var records = new List<ContentRecord>();
        var entities = new List<EntityRecord>();
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

            records.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Records);
            entities.AddRange((result.ContentGraph ?? CanonicalContentGraph.Empty).Entities);
        }

        var auditResult = MachineReadabilityTrustAuditBuilder.Build(config, outputDir, seoIndex, seoModels, new CanonicalContentGraph(records, entities), requireHreflangTargets: true);
        WriteReport(outputDir, auditResult, logger);
        return auditResult.SeoReport;
    }

    private static void WriteReport(string outputDir, MachineReadabilityTrustAuditResult result, ILogger logger)
    {
        var report = result.SeoReport;
        var json = JsonSerializer.Serialize(report, SeoAuditReportJsonContext.Default.SeoAuditReport);
        FileWriter.WriteUtf8(outputDir, Path.Combine(BuildReporter.ReportDirectoryName, "seo-report.json"), json + Environment.NewLine);
        PublishAuditReportWriter.Write(outputDir, result.PublishReport);

        WriteGeoReport(outputDir, report, logger);
        WriteAgentManifest(outputDir, report);

        foreach (var issue in report.Issues)
        {
            var message = $"{LogPrefix(issue.Code)} severity={issue.Severity} code={issue.Code} route={issue.Route ?? "-"} message={issue.Message}";
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

    private static string LogPrefix(string code)
    {
        if (code.StartsWith("publish.", StringComparison.OrdinalIgnoreCase))
        {
            return "publish.audit";
        }

        if (code.StartsWith("geo.", StringComparison.OrdinalIgnoreCase))
        {
            return "geo.audit";
        }

        return "seo.audit";
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

    private static void WriteAgentManifest(string outputDir, SeoAuditReport report)
    {
        var manifest = new AgentManifest(
            Schema: "https://bukit.dev/schemas/agent-manifest.v1.json",
            SchemaVersion: "1.0",
            GeneratedAt: report.Routes.Count == 0 ? DateTimeOffset.UnixEpoch : report.Routes.Max(x => x.LastModified),
            Documents: report.Routes.Select(route => new AgentManifestDocument(
                Id: route.SourceItemId ?? route.Url,
                CanonicalId: route.Canonical,
                Route: route.Url,
                Language: route.Language,
                ReviewStatus: route.ReviewStatus,
                Source: route.Source,
                Entities: route.EntityNames ?? Array.Empty<string>(),
                Representations: (route.RepresentationKinds ?? Array.Empty<string>())
                    .Select(kind => new AgentManifestRepresentation(kind, route.Url))
                    .ToArray())).ToArray());

        var json = JsonSerializer.Serialize(manifest, AgentManifestJsonContext.Default.AgentManifest);
        FileWriter.WriteUtf8(outputDir, "agent-manifest.json", json + Environment.NewLine);
    }

    internal static SeoAuditReport Build(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        CanonicalContentGraph? contentGraph = null,
        bool requireHreflangTargets = true)
        => MachineReadabilityTrustAuditBuilder.Build(config, outputDir, seoIndex, seoModels, contentGraph, requireHreflangTargets).SeoReport;

    internal static MachineReadabilityTrustAuditResult BuildMachineReadabilityTrustAudit(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        CanonicalContentGraph? contentGraph = null,
        bool requireHreflangTargets = true)
    {
        contentGraph ??= CanonicalContentGraph.Empty;
        var sitemapText = ReadOptional(Path.Combine(outputDir, "sitemap.xml"));
        var searchText = ReadOptional(Path.Combine(outputDir, "search.json"));
        var rssText = ReadOptional(Path.Combine(outputDir, "rss.xml"));
        var jsonFeedText = ReadOptional(Path.Combine(outputDir, config.Site.Feed.Path, "feed.json")) ??
                           ReadOptional(Path.Combine(outputDir, "feed.json"));
        var agentManifestText = ReadOptional(Path.Combine(outputDir, "agent-manifest.json"));
        var robotsText = ReadOptional(Path.Combine(outputDir, "robots.txt"));

        var issues = new List<SeoAuditIssue>();
        AnalyzeSitemapXml(sitemapText, issues);
        var routes = new List<SeoAuditRoute>();
        var publishDocuments = new List<PublishDocument>();
        var modelByCanonical = new Dictionary<string, (SeoIndexEntry Entry, SeoModel Model)>(StringComparer.OrdinalIgnoreCase);
        var recordsById = contentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in seoIndex.OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            seoModels.TryGetValue(key, out var model);
            recordsById.TryGetValue(entry.SourceItemId ?? string.Empty, out var record);
            var schemaTypes = model is null ? Array.Empty<string>() : SeoSchemaValidator.ExtractSchemaTypes(model.JsonLd, entry.Route.Url, issues);
            var document = PublishDocumentBuilder.Build(entry, model, record, schemaTypes);
            var outputPath = Path.Combine(outputDir, entry.Route.OutputPath);
            var outputExists = File.Exists(outputPath);
            if (!outputExists)
            {
                issues.Add(Error("seo.output_file_missing", entry.Route.Url, $"Output file is missing for route {entry.Route.Url}."));
            }
            else
            {
                var html = File.ReadAllText(outputPath);
                document = document with { SemanticOutline = SemanticHtmlAuditRules.ExtractSemanticOutline(html) };
                AnalyzeHtmlOutput(config, entry, document, html, issues);
            }

            var rssExpected = IsRssContent(config, entry);
            var sitemapIncluded = entry.Indexable && ContainsInvariant(sitemapText, entry.Canonical);
            var searchIncluded = entry.Indexable && ContainsInvariant(searchText, entry.Route.Url);
            var rssIncluded = entry.Indexable && rssExpected && ContainsInvariant(rssText, entry.Canonical);
            var jsonFeedExpected = IsJsonFeedContent(config, entry);
            var jsonFeedIncluded = entry.Indexable && jsonFeedExpected && ContainsInvariant(jsonFeedText, entry.Canonical);
            var manifestIncluded = !entry.Indexable ||
                                   agentManifestText is null ||
                                   ContainsInvariant(agentManifestText, entry.Route.Url) ||
                                   ContainsInvariant(agentManifestText, entry.Canonical);
            document = document with
            {
                SitemapIncluded = sitemapIncluded,
                SearchIncluded = searchIncluded,
                RssIncluded = rssIncluded,
                JsonFeedIncluded = jsonFeedIncluded,
                ManifestIncluded = manifestIncluded
            };

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

            AnalyzePublishDocument(document, issues);
            SeoCompatibilityAuditRules.Analyze(document, sitemapIncluded, searchIncluded, rssIncluded, rssExpected, jsonFeedIncluded, jsonFeedExpected, manifestIncluded, robotsText, issues);
            publishDocuments.Add(document);

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
                SchemaTypes: schemaTypes,
                Language: document.Language,
                Author: document.Author,
                Organization: document.Organization,
                Source: document.Source,
                OriginalSource: document.OriginalSource,
                ReviewStatus: document.ReviewStatus,
                EntityNames: document.EntityNames,
                RepresentationKinds: document.RepresentationKinds));
        }

        AnalyzePublishDocumentDuplicates(publishDocuments, issues);
        AnalyzeDuplicates(routes, issues);
        AnalyzeCanonicalTargets(routes, issues);
        AnalyzeHreflang(routes, modelByCanonical, issues, requireHreflangTargets);
        AnalyzeRobotsTxt(robotsText, routes, issues);

        var sortedRoutes = routes
            .OrderBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.OutputPath, StringComparer.OrdinalIgnoreCase)
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

        var sortedIssues = issues
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Route ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Message, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var geoScore = ComputeGeoScore(llmsTxtGenerated, llmsFullTxtGenerated, geoEnhancedRoutes, sortedRoutes);
        var publishIssueCount = sortedIssues.Count(x => x.Code.StartsWith("publish.", StringComparison.OrdinalIgnoreCase));
        var machineReadabilityIssueCount = sortedIssues.Count(x => IsMachineReadabilityIssue(x.Code));
        var trustIssueCount = sortedIssues.Count(x => IsTrustIssue(x.Code));
        var representationGapCount = sortedIssues.Count(x => string.Equals(x.Code, "publish.representation_missing", StringComparison.OrdinalIgnoreCase));

        var summary = new SeoAuditSummary(
            RouteCount: sortedRoutes.Count,
            IndexableCount: sortedRoutes.Count(x => x.Indexable),
            NonIndexableCount: sortedRoutes.Count(x => !x.Indexable),
            ErrorCount: sortedIssues.Count(x => string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase)),
            WarningCount: sortedIssues.Count(x => string.Equals(x.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            LlmsTxtGenerated: llmsTxtGenerated,
            LlmsFullTxtGenerated: llmsFullTxtGenerated,
            GeoEnhancedCount: geoEnhancedRoutes.Length,
            GeoScore: geoScore,
            PublishIssueCount: publishIssueCount,
            MachineReadabilityIssueCount: machineReadabilityIssueCount,
            TrustIssueCount: trustIssueCount,
            RepresentationGapCount: representationGapCount);

        var seoReport = new SeoAuditReport(
            Schema: SeoAuditModels.ReportSchema,
            SchemaVersion: SeoAuditModels.ReportSchemaVersion,
            GeneratedAt: DateTimeOffset.UtcNow,
            SiteName: config.Site.Name,
            SiteUrl: config.Site.Url,
            BaseUrl: config.Site.BaseUrl,
            Routes: sortedRoutes,
            Issues: sortedIssues,
            Summary: summary);
        return new MachineReadabilityTrustAuditResult(
            seoReport,
            PublishAuditBuilder.Build(seoReport, publishDocuments));
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

    private static void AnalyzeHtmlOutput(AppConfig config, SeoIndexEntry entry, PublishDocument document, string html, List<SeoAuditIssue> issues)
    {
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

        if (entry.Indexable)
        {
            SemanticHtmlAuditRules.Analyze(entry, document, html, issues);
        }
    }

}
