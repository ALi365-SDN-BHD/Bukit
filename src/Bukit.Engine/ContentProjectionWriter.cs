using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine;

internal interface IContentProjectionWriter
{
    IReadOnlyList<PublishProjectionResult> Write(PublishProjectionContext context);
}

internal sealed class DefaultContentProjectionWriter : IContentProjectionWriter
{
    private readonly JsonContentDocumentProjection _jsonProjection;
    private readonly MarkdownContentDocumentProjection _markdownProjection;
    private readonly AgentManifestProjection _agentManifestProjection;
    private readonly IReadOnlyList<IPublishProjection> _aggregateProjections;

    internal DefaultContentProjectionWriter()
        : this(
            new JsonContentDocumentProjection(),
            new MarkdownContentDocumentProjection(),
            new AgentManifestProjection(),
            PublishRepresentationRegistry.AggregateProjectionAdapters())
    {
    }

    internal DefaultContentProjectionWriter(
        JsonContentDocumentProjection jsonProjection,
        MarkdownContentDocumentProjection markdownProjection,
        AgentManifestProjection agentManifestProjection,
        IReadOnlyList<IPublishProjection> aggregateProjections)
    {
        _jsonProjection = jsonProjection;
        _markdownProjection = markdownProjection;
        _agentManifestProjection = agentManifestProjection;
        _aggregateProjections = aggregateProjections;
    }

    public IReadOnlyList<PublishProjectionResult> Write(PublishProjectionContext context)
    {
        var results = new List<PublishProjectionResult>
        {
            _jsonProjection.Project(context),
            _markdownProjection.Project(context),
            _agentManifestProjection.Project(context)
        };
        foreach (var projection in _aggregateProjections)
        {
            results.Add(projection.Project(context));
        }

        return results;
    }

    internal static IReadOnlyList<ContentProjectionDocumentContext> BuildDocumentContexts(PublishProjectionContext context)
    {
        var routedDocuments = context.RoutedDocuments.Concat(context.DerivedDocuments).ToList();
        if (routedDocuments.Count > 0)
        {
            return routedDocuments
                .Select(document =>
                {
                    var key = BuildPathUtils.NormalizeRelPath(document.Route.OutputPath);
                    context.SeoIndex.TryGetValue(key, out var entry);
                    context.SeoModels.TryGetValue(key, out var model);
                    return new ContentProjectionDocumentContext(
                        context.OutputDir,
                        document.Document.Record,
                        document.Route,
                        entry,
                        model);
                })
                .ToArray();
        }

        var routedAll = context.RoutedDocuments.Concat(context.DerivedDocuments).ToList();
        var documentContexts = new List<ContentProjectionDocumentContext>(routedAll.Count);
        foreach (var routedDocument in routedAll)
        {
            var record = routedDocument.Document.Record;
            var route = routedDocument.Route;

            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            context.SeoIndex.TryGetValue(key, out var entry);
            context.SeoModels.TryGetValue(key, out var model);

            var documentContext = new ContentProjectionDocumentContext(
                context.OutputDir,
                record,
                route,
                entry,
                model);
            documentContexts.Add(documentContext);
        }

        return documentContexts;
    }

    internal static IReadOnlyList<AgentManifestEntry> BuildAgentManifestEntries(PublishProjectionContext context)
    {
        var manifestEntries = new List<AgentManifestEntry>();
        foreach (var documentContext in BuildDocumentContexts(context))
        {
            var record = documentContext.Record;
            var route = documentContext.Route;
            var entry = documentContext.SeoIndexEntry;
            var model = documentContext.SeoModel;
            if (entry?.Indexable == false)
            {
                continue;
            }

            manifestEntries.Add(new AgentManifestEntry(
                record.Identity.Id,
                record.Identity.CanonicalUrlKey,
                route.Url,
                record.Presentation.Language,
                record.Trust.ReviewStatus,
                record.Provenance.Source,
                record.Entities.Select(x => x.Name).ToArray(),
                BuildAgentManifestRepresentationEntries(record, route.Url, entry, model),
                record.Lifecycle.UpdatedAt ?? record.Lifecycle.PublishedAt));
        }

        return manifestEntries;
    }

    internal static IReadOnlyList<RepresentationEntry> BuildAgentManifestRepresentationEntries(
        ContentRecord record,
        string routeUrl,
        SeoIndexEntry? entry,
        SeoModel? model)
    {
        var canonical = model?.Canonical ?? entry?.Canonical ?? routeUrl;
        return PublishRepresentationRegistry.DocumentRepresentationsFor(includeJsonLd: model?.JsonLd.Count > 0)
            .Select(representation => representation.Kind switch
            {
                "html" => new RepresentationEntry(representation.Kind, routeUrl),
                "semantic-html" => new RepresentationEntry(representation.Kind, routeUrl),
                "json" => new RepresentationEntry(representation.Kind, GetContentProjectionUrl(record, ".json")),
                "markdown" => new RepresentationEntry(representation.Kind, GetContentProjectionUrl(record, ".md")),
                "jsonld" => new RepresentationEntry(representation.Kind, canonical),
                _ => new RepresentationEntry(representation.Kind, routeUrl)
            })
            .ToArray();
    }

    internal static string GetContentProjectionBasePath(string outputDir, ContentRecord record)
    {
        var fileName = BuildPathUtils.SanitizeFileSegment(record.Identity.Slug);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = BuildPathUtils.SanitizeFileSegment(record.Identity.Id);
        }

        return Path.Combine(outputDir, "content", fileName);
    }

    internal static string GetContentProjectionUrl(ContentRecord record, string extension)
    {
        var fileName = BuildPathUtils.SanitizeFileSegment(record.Identity.Slug);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = BuildPathUtils.SanitizeFileSegment(record.Identity.Id);
        }

        return $"/content/{fileName}{extension}";
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

internal sealed record ContentProjectionDocumentContext(
    string OutputDir,
    ContentRecord Record,
    RouteInfo Route,
    SeoIndexEntry? SeoIndexEntry,
    SeoModel? SeoModel);

internal sealed class JsonContentDocumentProjection : IPublishProjection
{
    public PublishRepresentation Representation => PublishRepresentationRegistry.Json;

    public PublishProjectionResult Project(PublishProjectionContext context)
    {
        var outputs = DefaultContentProjectionWriter.BuildDocumentContexts(context)
            .Select(Project)
            .ToArray();
        return new PublishProjectionResult(Representation, outputs);
    }

    internal PublishRepresentationOutput Project(ContentProjectionDocumentContext context)
    {
        var record = context.Record;
        var route = context.Route;
        var entry = context.SeoIndexEntry;
        var model = context.SeoModel;
        var path = DefaultContentProjectionWriter.GetContentProjectionBasePath(context.OutputDir, record) + ".json";
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
        var relPath = DefaultContentProjectionWriter.GetContentProjectionUrl(record, ".json").TrimStart('/');
        return new PublishRepresentationOutput(
            Representation.Kind,
            DefaultContentProjectionWriter.GetContentProjectionUrl(record, ".json"),
            relPath,
            File.Exists(path),
            entry?.Indexable != false);
    }
}

internal sealed class MarkdownContentDocumentProjection : IPublishProjection
{
    public PublishRepresentation Representation => PublishRepresentationRegistry.Markdown;

    public PublishProjectionResult Project(PublishProjectionContext context)
    {
        var outputs = DefaultContentProjectionWriter.BuildDocumentContexts(context)
            .Select(Project)
            .ToArray();
        return new PublishProjectionResult(Representation, outputs);
    }

    internal PublishRepresentationOutput Project(ContentProjectionDocumentContext context)
    {
        var record = context.Record;
        var route = context.Route;
        var entry = context.SeoIndexEntry;
        var path = DefaultContentProjectionWriter.GetContentProjectionBasePath(context.OutputDir, record) + ".md";
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
        var relPath = DefaultContentProjectionWriter.GetContentProjectionUrl(record, ".md").TrimStart('/');
        return new PublishRepresentationOutput(
            Representation.Kind,
            DefaultContentProjectionWriter.GetContentProjectionUrl(record, ".md"),
            relPath,
            File.Exists(path),
            entry?.Indexable != false);
    }
}

internal sealed class AgentManifestProjection : IPublishProjection
{
    public PublishRepresentation Representation => PublishRepresentationRegistry.AggregateRepresentations()
        .Single(x => x.Kind == "agent-manifest");

    public PublishProjectionResult Project(PublishProjectionContext context)
    {
        var path = Project(context.OutputDir, DefaultContentProjectionWriter.BuildAgentManifestEntries(context));
        return new PublishProjectionResult(
            Representation,
            [new PublishRepresentationOutput(Representation.Kind, "/" + Representation.Path, Representation.Path, File.Exists(path), Indexable: false)]);
    }

    internal string Project(string outputDir, IReadOnlyList<DefaultContentProjectionWriter.AgentManifestEntry> entries)
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
        var path = Path.Combine(outputDir, "agent-manifest.json");
        File.WriteAllText(path, json + Environment.NewLine, Encoding.UTF8);
        return path;
    }
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
    IReadOnlyList<DefaultContentProjectionWriter.AgentManifestEntry> Documents);
