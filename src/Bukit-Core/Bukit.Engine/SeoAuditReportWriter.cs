using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Shared;

namespace Bukit.Engine;

internal static partial class SeoAuditReportWriter
{
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
        ILogger logger,
        IReadOnlyList<PublishProjectionResult>? projectionResults = null)
    {
        var result = MachineReadabilityTrustAuditBuilder.Build(config, outputDir, seoIndex, seoModels, contentGraph, requireHreflangTargets: false, projectionResults);
        WriteReport(outputDir, result, logger);
        return result.SeoReport;
    }

    internal static SeoAuditReport WriteMerged(
        AppConfig config,
        string outputDir,
        IReadOnlyList<BuildVariantResult> results,
        ILogger logger,
        IReadOnlyList<PublishProjectionResult>? projectionResults = null)
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

        var auditResult = MachineReadabilityTrustAuditBuilder.Build(config, outputDir, seoIndex, seoModels, new CanonicalContentGraph(records, entities), requireHreflangTargets: true, projectionResults);
        WriteReport(outputDir, auditResult, logger);
        return auditResult.SeoReport;
    }

    private static void WriteReport(string outputDir, MachineReadabilityTrustAuditResult result, ILogger logger)
    {
        var report = result.SeoReport;
        var json = JsonSerializer.Serialize(report, SeoAuditReportJsonContext.Default.SeoAuditReport);
        FileWriter.WriteUtf8(outputDir, Path.Combine(BuildReporter.ReportDirectoryName, "seo-report.json"), json + Environment.NewLine);
        PublishAuditReportWriter.Write(outputDir, result.PublishReport);
        SeoRouteMapWriter.Write(outputDir, result.RouteMap);

        WriteGeoReport(outputDir, report, logger);

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
                    t is "FAQPage" or "HowTo" or "BlogPosting" or "Person" or "Article" or "NewsArticle" or "SpeakableSpecification"))
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
        CanonicalContentGraph? contentGraph = null,
        bool requireHreflangTargets = true)
        => MachineReadabilityTrustAuditBuilder.Build(config, outputDir, seoIndex, seoModels, contentGraph, requireHreflangTargets).SeoReport;


}
