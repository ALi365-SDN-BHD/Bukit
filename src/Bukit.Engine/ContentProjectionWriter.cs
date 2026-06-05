using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine;

internal static class ContentProjectionWriter
{
    internal static void Write(
        string outputDir,
        CanonicalContentGraph graph,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> derivedRouted,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IReadOnlyDictionary<string, SeoModel> seoModels)
    {
        var recordsById = graph.Records
            .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var routedAll = routed.Concat(derivedRouted).ToList();
        var manifestEntries = new List<AgentManifestEntry>();

        foreach (var (item, route) in routedAll)
        {
            if (!recordsById.TryGetValue(item.Id, out var record))
            {
                record = CanonicalContentGraphBuilder.ToRecord(item);
            }

            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            seoIndex.TryGetValue(key, out var entry);
            seoModels.TryGetValue(key, out var model);

            var outputBase = GetContentProjectionBasePath(outputDir, record);
            WriteJsonProjection(outputBase + ".json", record, route, entry, model);
            WriteMarkdownProjection(outputBase + ".md", record, route, entry);

            manifestEntries.Add(new AgentManifestEntry(
                record.Identity.Id,
                record.Identity.CanonicalUrlKey,
                route.Url,
                record.Presentation.Language,
                record.Trust.ReviewStatus,
                record.Provenance.Source,
                record.Entities.Select(x => x.Name).ToArray(),
                new[]
                {
                    new RepresentationEntry("html", route.Url),
                    new RepresentationEntry("json", NormalizeContentProjectionUrl(route.Url, ".json")),
                    new RepresentationEntry("markdown", NormalizeContentProjectionUrl(route.Url, ".md")),
                    new RepresentationEntry("jsonld", model?.Canonical ?? entry?.Canonical ?? route.Url)
                },
                record.Lifecycle.UpdatedAt ?? record.Lifecycle.PublishedAt));
        }

        WriteAgentManifest(outputDir, manifestEntries);
    }

    private static void WriteJsonProjection(string path, ContentRecord record, RouteInfo route, SeoIndexEntry? entry, SeoModel? model)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var projection = new ContentProjectionDocument(
            Id: record.Identity.Id,
            Slug: record.Identity.Slug,
            CanonicalUrlKey: record.Identity.CanonicalUrlKey,
            Route: route.Url,
            Title: record.Presentation.Title,
            Summary: record.Presentation.Summary,
            Body: record.Presentation.Body,
            Language: record.Presentation.Language,
            Type: record.Identity.ContentType,
            Collection: record.Classification.Collection,
            Tags: record.Classification.Tags,
            Sections: record.Classification.Sections,
            Author: record.Ownership.Author,
            Organization: record.Ownership.Organization,
            PublishedAt: record.Lifecycle.PublishedAt,
            UpdatedAt: record.Lifecycle.UpdatedAt,
            ExpiresAt: record.Lifecycle.ExpiresAt,
            ReviewedAt: record.Lifecycle.ReviewedAt,
            Source: record.Provenance.Source,
            OriginalSource: record.Provenance.OriginalSource,
            Citations: record.Provenance.Citations,
            References: record.Provenance.References,
            SyncStatus: record.Provenance.SyncStatus,
            ReviewStatus: record.Trust.ReviewStatus,
            CredibilityScore: record.Trust.CredibilityScore,
            QualityFlags: record.Trust.QualityFlags,
            Entities: record.Entities,
            Relations: record.Relations,
            Media: record.Media,
            Canonical: model?.Canonical ?? entry?.Canonical);

        var json = JsonSerializer.Serialize(projection, ContentProjectionJsonContext.Default.ContentProjectionDocument);
        File.WriteAllText(path, json + Environment.NewLine, Encoding.UTF8);
    }

    private static void WriteMarkdownProjection(string path, ContentRecord record, RouteInfo route, SeoIndexEntry? entry)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var sb = new StringBuilder();
        sb.AppendLine($"# {record.Presentation.Title}");
        sb.AppendLine();
        sb.AppendLine($"- Route: {route.Url}");
        sb.AppendLine($"- Language: {record.Presentation.Language}");
        sb.AppendLine($"- Type: {record.Identity.ContentType}");
        sb.AppendLine($"- Review Status: {record.Trust.ReviewStatus}");
        if (!string.IsNullOrWhiteSpace(record.Provenance.Source))
        {
            sb.AppendLine($"- Source: {record.Provenance.Source}");
        }

        if (!string.IsNullOrWhiteSpace(entry?.Canonical))
        {
            sb.AppendLine($"- Canonical: {entry.Canonical}");
        }

        if (!string.IsNullOrWhiteSpace(record.Presentation.Summary))
        {
            sb.AppendLine();
            sb.AppendLine(record.Presentation.Summary);
        }

        if (record.Entities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Entities");
            sb.AppendLine();
            foreach (var entity in record.Entities)
            {
                sb.AppendLine($"- {entity.Type}: {entity.Name}");
            }
        }

        if (!string.IsNullOrWhiteSpace(record.Presentation.Body))
        {
            sb.AppendLine();
            sb.AppendLine("## Body");
            sb.AppendLine();
            sb.AppendLine(SearchIndexBuilder.StripHtmlToText(record.Presentation.Body));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteAgentManifest(string outputDir, IReadOnlyList<AgentManifestEntry> entries)
    {
        var generatedAt = entries.Count == 0
            ? DateTimeOffset.UnixEpoch
            : entries.Max(x => x.PublishedAt);
        var orderedEntries = entries.OrderBy(x => x.Route, StringComparer.OrdinalIgnoreCase).ToArray();
        var manifest = new ContentProjectionAgentManifest(
            Schema: "https://bukit.dev/schemas/agent-manifest.v1.json",
            SchemaVersion: "1.0",
            GeneratedAt: generatedAt,
            Documents: orderedEntries);

        var json = JsonSerializer.Serialize(manifest, ContentProjectionJsonContext.Default.ContentProjectionAgentManifest);
        File.WriteAllText(Path.Combine(outputDir, "agent-manifest.json"), json + Environment.NewLine, Encoding.UTF8);
    }

    private static string GetContentProjectionBasePath(string outputDir, ContentRecord record)
    {
        var fileName = BuildPathUtils.SanitizeFileSegment(record.Identity.Slug);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = BuildPathUtils.SanitizeFileSegment(record.Identity.Id);
        }

        return Path.Combine(outputDir, "content", fileName);
    }

    private static string NormalizeContentProjectionUrl(string routeUrl, string extension)
    {
        var trimmed = routeUrl.Trim('/');
        var slug = string.IsNullOrWhiteSpace(trimmed) ? "index" : trimmed.Replace('/', '-');
        return $"/content/{slug}{extension}";
    }

    internal sealed record AgentManifestEntry(
        string Id,
        string CanonicalId,
        string Route,
        string Language,
        string ReviewStatus,
        string? Source,
        IReadOnlyList<string> Entities,
        IReadOnlyList<RepresentationEntry> Representations,
        DateTimeOffset PublishedAt);

    internal sealed record RepresentationEntry(string Kind, string Url);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(ContentProjectionDocument))]
[JsonSerializable(typeof(ContentProjectionAgentManifest))]
internal sealed partial class ContentProjectionJsonContext : JsonSerializerContext;

internal sealed record ContentProjectionDocument(
    string Id,
    string Slug,
    string CanonicalUrlKey,
    string Route,
    string Title,
    string? Summary,
    string? Body,
    string Language,
    string Type,
    string Collection,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Sections,
    string? Author,
    string? Organization,
    DateTimeOffset PublishedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ReviewedAt,
    string? Source,
    string? OriginalSource,
    IReadOnlyList<string> Citations,
    IReadOnlyList<string> References,
    string? SyncStatus,
    string ReviewStatus,
    double? CredibilityScore,
    IReadOnlyList<string> QualityFlags,
    IReadOnlyList<EntityRecord> Entities,
    IReadOnlyList<ContentRelation> Relations,
    IReadOnlyList<MediaAsset> Media,
    string? Canonical);

internal sealed record ContentProjectionAgentManifest(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<ContentProjectionWriter.AgentManifestEntry> Documents);
