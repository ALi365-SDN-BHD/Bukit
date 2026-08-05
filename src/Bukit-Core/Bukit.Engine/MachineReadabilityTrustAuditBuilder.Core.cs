using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.PublishAuditRules;
using Bukit.Rendering;

namespace Bukit.Engine;

internal static partial class MachineReadabilityTrustAuditBuilder
{
    private const int TitleMaxLength = 60;
    private const int DescriptionMaxLength = 160;

    internal static MachineReadabilityTrustAuditResult BuildPublishAuditCore(
        AppConfig config,
        string outputDir,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels,
        CanonicalContentGraph? contentGraph = null,
        bool requireHreflangTargets = true,
        IReadOnlyList<PublishProjectionResult>? projectionResults = null,
        IReadOnlyDictionary<string, ContentDocument>? documentsByOutputPath = null)
    {
        contentGraph ??= CanonicalContentGraph.Empty;
        var projectionLookup = BuildProjectionLookup(projectionResults);
        var sitemapText = ReadOptional(Path.Combine(outputDir, "sitemap.xml"));
        var searchText = ReadOptional(Path.Combine(outputDir, "search.json"));
        var rssText = ReadOptional(Path.Combine(outputDir, "rss.xml"));
        var atomFeedText = ReadOptional(Path.Combine(outputDir, config.Site.Feed.Path, "atom.xml")) ??
                           ReadOptional(Path.Combine(outputDir, "atom.xml"));
        var jsonFeedText = ReadOptional(Path.Combine(outputDir, config.Site.Feed.Path, "feed.json")) ??
                           ReadOptional(Path.Combine(outputDir, "feed.json"));
        var agentManifestText = ReadOptional(Path.Combine(outputDir, "agent-manifest.json"));
        var robotsText = ReadOptional(Path.Combine(outputDir, "robots.txt"));
        var llmsText = ReadOptional(Path.Combine(outputDir, "llms.txt"));
        var llmsFullText = ReadOptional(Path.Combine(outputDir, "llms-full.txt"));

        var seoIssues = new List<SeoAuditIssue>();
        var publishIssues = new List<PublishAuditIssue>();
        AnalyzeSitemapXml(sitemapText, seoIssues);
        var routes = new List<SeoAuditRoute>();
        var documentTitles = new List<DocumentTitleAuditEntry>();
        var publishDocuments = new List<PublishDocument>();
        var routeMapBuilder = new SeoRouteMapBuilder(config.Site.Url, config.Site.BaseUrl);
        var modelByCanonical = new Dictionary<string, (SeoIndexEntry Entry, SeoModel Model)>(StringComparer.OrdinalIgnoreCase);
        var recordsById = contentGraph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        var trustRequirements = TrustAuditRequirements.From(ContentModelSchemaFactory.FromConfig(config));
        var feedWindowRoutes = BuildFeedWindowRoutes(config, seoIndex.Values, recordsById);
        var searchActionExpected = config.Site.Seo.Enabled &&
                                   config.Site.Seo.Schema.SearchAction &&
                                   !string.IsNullOrWhiteSpace(config.Site.Search.Route);

        foreach (var (key, entry) in seoIndex.OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            seoModels.TryGetValue(key, out var model);
            var record = ResolveRecordForEntry(recordsById, entry, config.Site.Language);
            routeMapBuilder.Add(entry, model, record);
            var schemaTypes = model is null
                ? Array.Empty<string>()
                : SeoSchemaValidator.ExtractSchemaTypes(model.JsonLd, entry.Route.Url, seoIssues, searchActionExpected);
            var document = PublishDocumentBuilder.Build(entry, model, record, schemaTypes);
            var outputPath = Path.Combine(outputDir, entry.Route.OutputPath);
            var outputExists = File.Exists(outputPath);
            HtmlDocumentTitleInspection? titleInspection = null;
            if (!outputExists)
            {
                seoIssues.Add(Error("seo.output_file_missing", entry.Route.Url, $"Output file is missing for route {entry.Route.Url}."));
            }
            else
            {
                var html = File.ReadAllText(outputPath);
                titleInspection = HtmlDocumentTitleInspector.Inspect(html);
                var semanticInspection = SemanticLandmarkHeadingInspector.Inspect(html);
                document = document with { SemanticOutline = semanticInspection.PrimaryHeadings };
                AnalyzeHtmlOutput(config, entry, model, document, html, semanticInspection, titleInspection, seoIssues, publishIssues);
            }

            var rssExpected = IsFeedFormatEnabled(config, "rss") && feedWindowRoutes.Contains(entry.Route.Url);
            var sitemapIncluded = TryGetProjectionIncluded(projectionLookup, "sitemap", entry, out var projectedSitemap)
                ? projectedSitemap
                : entry.Indexable && ContainsInvariant(sitemapText, $"<loc>{entry.Canonical}</loc>");
            var searchIncluded = TryGetProjectionIncluded(projectionLookup, "search", entry, out var projectedSearch)
                ? projectedSearch
                : entry.Indexable && ContainsInvariant(searchText, entry.Route.Url);
            var rssIncluded = TryGetProjectionIncluded(projectionLookup, "feed", entry, out var projectedRss)
                ? projectedRss
                : entry.Indexable && rssExpected && ContainsInvariant(rssText, entry.Canonical);
            var atomFeedExpected = IsFeedFormatEnabled(config, "atom") && feedWindowRoutes.Contains(entry.Route.Url);
            var atomFeedIncluded = TryGetProjectionIncluded(projectionLookup, "atom", entry, out var projectedAtom)
                ? projectedAtom
                : entry.Indexable && atomFeedExpected && ContainsInvariant(atomFeedText, entry.Canonical);
            var jsonFeedExpected = IsFeedFormatEnabled(config, "json") && feedWindowRoutes.Contains(entry.Route.Url);
            var jsonFeedIncluded = TryGetProjectionIncluded(projectionLookup, "jsonfeed", entry, out var projectedJsonFeed)
                ? projectedJsonFeed
                : entry.Indexable && jsonFeedExpected && ContainsInvariant(jsonFeedText, entry.Canonical);
            var llmsExpected = IsLlmsContent(config, entry);
            var llmsKindExpected = llmsExpected || (entry.Indexable && llmsText is not null);
            var llmsIncluded = TryGetProjectionIncluded(projectionLookup, "llms", entry, out var projectedLlms)
                ? projectedLlms
                : entry.Indexable &&
                  llmsKindExpected &&
                  (ContainsInvariant(llmsText, entry.Route.Url) || ContainsInvariant(llmsText, entry.Canonical));
            var llmsFullExpected = IsLlmsFullContent(config, entry);
            var llmsFullKindExpected = llmsFullExpected || (entry.Indexable && llmsFullText is not null);
            var llmsFullIncluded = TryGetProjectionIncluded(projectionLookup, "llms-full", entry, out var projectedLlmsFull)
                ? projectedLlmsFull
                : entry.Indexable &&
                  llmsFullKindExpected &&
                  (ContainsInvariant(llmsFullText, entry.Route.Url) || ContainsInvariant(llmsFullText, entry.Canonical));
            var robotsExpected = config.Site.Seo.RobotsTxt.Enabled || robotsText is not null;
            var agentManifestExpected = entry.Indexable;
            var manifestIncluded = TryGetProjectionIncluded(projectionLookup, "agent-manifest", entry, out var projectedManifest)
                ? projectedManifest
                : !entry.Indexable ||
                  (agentManifestText is not null &&
                   (ContainsInvariant(agentManifestText, entry.Route.Url) ||
                    ContainsInvariant(agentManifestText, entry.Canonical)));
            var robotsIncluded = TryGetProjectionIncluded(projectionLookup, "robots", entry, out var projectedRobots)
                ? projectedRobots
                : robotsText is not null;
            document = document with
            {
                RepresentationKinds = BuildAggregateRepresentationKinds(document.RepresentationKinds, new PublishRepresentationExpectation(
                    Feed: rssExpected,
                    Atom: atomFeedExpected,
                    JsonFeed: jsonFeedExpected,
                    Sitemap: entry.Indexable,
                    Search: entry.Indexable,
                    Llms: llmsKindExpected,
                    LlmsFull: llmsFullKindExpected,
                    Robots: robotsExpected,
                    AgentManifest: agentManifestExpected))
            };
            document = document with
            {
                SitemapIncluded = sitemapIncluded,
                SearchIncluded = searchIncluded,
                RssIncluded = rssIncluded,
                AtomFeedIncluded = atomFeedIncluded,
                JsonFeedIncluded = jsonFeedIncluded,
                LlmsIncluded = llmsIncluded,
                LlmsFullIncluded = llmsFullIncluded,
                RobotsIncluded = robotsIncluded,
                ManifestIncluded = manifestIncluded
            };

            if (entry.Indexable && sitemapText is not null && !sitemapIncluded)
            {
                seoIssues.Add(Warning("seo.sitemap_missing_url", entry.Route.Url, $"Indexable route is missing from sitemap: {entry.Canonical}."));
            }

            if (!entry.Indexable && sitemapText is not null && ContainsInvariant(sitemapText, $"<loc>{entry.Canonical}</loc>"))
            {
                seoIssues.Add(Error("seo.noindex_in_sitemap", entry.Route.Url, $"Noindex route appears in sitemap: {entry.Canonical}."));
            }

            if (documentsByOutputPath is not null &&
                documentsByOutputPath.TryGetValue(key, out var curationDocument))
            {
                var curation = LlmsCurationPolicyParser.Parse(curationDocument);
                if (curation.Valid && curation.Policy.Visibility == LlmsVisibility.Include && !entry.Indexable)
                {
                    seoIssues.Add(Warning("geo.llms_include_nonindexable", entry.Route.Url,
                        $"llms visibility include cannot override noindex; route stays excluded from llms output: {entry.Canonical}."));
                }

                if (curation.Valid &&
                    curation.Policy.Visibility == LlmsVisibility.Exclude &&
                    (llmsIncluded || llmsFullIncluded))
                {
                    publishIssues.Add(new PublishAuditIssue("warning", "publish.llms_excluded_route_present", entry.Route.Url,
                        $"Route explicitly excluded from llms appears in llms output: {entry.Canonical}."));
                }
            }

            if (model is not null)
            {
                AnalyzeRouteModel(config, entry, model, outputDir, seoIssues);
                if (!string.Equals(model.Canonical, entry.Canonical, StringComparison.OrdinalIgnoreCase))
                {
                    seoIssues.Add(Warning("seo.canonical_sitemap_mismatch", entry.Route.Url, $"Model canonical does not match SeoIndex canonical: {model.Canonical} != {entry.Canonical}."));
                }

                modelByCanonical[model.Canonical] = (entry, model);
            }

            AnalyzePublishDocument(document, trustRequirements, outputDir, publishIssues);
            SeoCompatibilityAuditRules.Analyze(document, sitemapIncluded, searchIncluded, rssIncluded, rssExpected, atomFeedIncluded, atomFeedExpected, jsonFeedIncluded, jsonFeedExpected, llmsIncluded, llmsExpected, llmsFullIncluded, llmsFullExpected, manifestIncluded, robotsText, publishIssues);
            publishDocuments.Add(document);

            var auditRoute = new SeoAuditRoute(
                Url: entry.Route.Url,
                OutputPath: entry.Route.OutputPath,
                Title: model?.Title,
                Description: model?.Description,
                Canonical: entry.Canonical,
                Robots: entry.Robots,
                Indexable: entry.Indexable,
                LastModified: PublishDocumentBuilder.NormalizeLastModified(entry.LastModified),
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
                RepresentationKinds: document.RepresentationKinds);
            routes.Add(auditRoute);

            if (titleInspection is { Count: 1, PrimaryTitle: { } actualTitle } &&
                !string.IsNullOrWhiteSpace(actualTitle))
            {
                documentTitles.Add(new DocumentTitleAuditEntry(auditRoute, actualTitle));
            }
        }

        AnalyzePublishDocumentDuplicates(publishDocuments, publishIssues);
        AnalyzeDuplicates(routes, seoIssues);
        AnalyzeDocumentTitleDuplicates(documentTitles, seoIssues);
        AnalyzeCanonicalTargets(routes, seoIssues);
        AnalyzeHreflang(routes, modelByCanonical, seoIssues, requireHreflangTargets);
        AnalyzeRobotsTxt(robotsText, routes, seoIssues);

        var sortedRoutes = routes
            .OrderBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.OutputPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var geoEnhancedRoutes = sortedRoutes
            .Where(x => x.SchemaTypes.Any(t =>
                t is "FAQPage" or "HowTo" or "BlogPosting" or "Person" or "Article" or "NewsArticle" or "SpeakableSpecification"))
            .ToArray();
        var llmsTxtGenerated = File.Exists(Path.Combine(outputDir, "llms.txt"));
        var llmsFullTxtGenerated = File.Exists(Path.Combine(outputDir, "llms-full.txt"));

        if (config.Site.Seo.Geo.Enabled && config.Site.Seo.Geo.LlmsTxt && !llmsTxtGenerated)
        {
            seoIssues.Add(new SeoAuditIssue("warning", "geo.llms_txt_missing", null,
                "llms.txt was not generated. Ensure GEO is enabled and content has indexable routes."));
        }

        if (config.Site.Seo.Geo.Enabled && config.Site.Seo.Geo.LlmsFullTxt && !llmsFullTxtGenerated)
        {
            seoIssues.Add(new SeoAuditIssue("warning", "geo.llms_full_txt_missing", null,
                "llms-full.txt was not generated. Check that llmsFullTxt is enabled and content is indexable."));
        }

        var sortedPublishIssues = publishIssues
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Route ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Message, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sortedIssues = seoIssues
            .Concat(sortedPublishIssues.Select(x => x.ToSeoIssue()))
            .OrderBy(x => SeverityRank(x.Severity))
            .ThenBy(x => x.Route ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Message, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasValidArticleAuthor = seoModels.Values.Any(model =>
            !string.IsNullOrWhiteSpace(model.Article.Author) &&
            AuthorSchemaType.IsValid(model.Article.AuthorType));
        var geoScore = ComputeGeoScore(
            llmsTxtGenerated,
            llmsFullTxtGenerated,
            geoEnhancedRoutes,
            sortedRoutes,
            hasValidArticleAuthor);
        var publishIssueCount = sortedIssues.Count(x => x.Code.StartsWith("publish.", StringComparison.OrdinalIgnoreCase));
        var machineReadabilityIssueCount = sortedIssues.Count(x => IsMachineReadabilityIssue(x.Code));
        var trustIssueCount = sortedIssues.Count(x => IsTrustIssue(x.Code));
        var representationGapCount = sortedIssues.Count(x =>
            x.Code is "publish.representation_missing"
                or "publish.atom_feed_missing_route"
                or "publish.llms_full_missing_route"
                or "publish.representation_file_missing"
                or "publish.representation_json_mismatch"
                or "publish.representation_markdown_mismatch"
                or "publish.representation_json_invalid"
                or "publish.manifest_mismatch"
                or "publish.manifest_invalid");

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
            PublishAuditBuilder.Build(seoReport, publishDocuments, outputDir, sortedPublishIssues),
            routeMapBuilder.Build(seoReport.GeneratedAt));
    }

}
