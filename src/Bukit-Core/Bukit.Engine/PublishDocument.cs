using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed record PublishDocument(
    string RouteUrl,
    string OutputPath,
    string Canonical,
    bool Indexable,
    string? ContentType,
    bool IsDerived,
    string? SourceItemId,
    DateTimeOffset? LastModified,
    string? Title,
    string? Description,
    string? Language,
    string? Author,
    string? Organization,
    string? Source,
    string? OriginalSource,
    string? ReviewStatus,
    string? Summary,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<string> SourceReferences,
    IReadOnlyList<string> EntityNames,
    IReadOnlyList<PublishEntitySummary> EntitySummaries,
    IReadOnlyList<string> RepresentationKinds,
    IReadOnlyList<string> SchemaTypes,
    IReadOnlyList<PublishSemanticOutlineItem> SemanticOutline,
    bool SitemapIncluded,
    bool SearchIncluded,
    bool RssIncluded,
    bool AtomFeedIncluded,
    bool JsonFeedIncluded,
    bool LlmsIncluded,
    bool LlmsFullIncluded,
    bool RobotsIncluded,
    bool ManifestIncluded,
    SeoModel? SeoModel,
    ContentRecord? ContentRecord);

internal sealed record PublishEntitySummary(string Type, string Name, string? Description);

internal sealed record PublishSemanticOutlineItem(int Level, string Text);

internal static class PublishDocumentBuilder
{
    internal static PublishDocument Build(
        SeoIndexEntry entry,
        SeoModel? model,
        ContentRecord? record,
        IReadOnlyList<string> schemaTypes)
    {
        return new PublishDocument(
            entry.Route.Url,
            entry.Route.OutputPath,
            entry.Canonical,
            entry.Indexable,
            entry.ContentType,
            entry.IsDerived,
            entry.SourceItemId,
            NormalizeLastModified(entry.LastModified),
            model?.Title,
            model?.Description,
            record?.Presentation.Language,
            record?.Ownership.Author ?? model?.Article.Author,
            record?.Ownership.Organization,
            record?.Provenance.Source,
            record?.Provenance.OriginalSource,
            string.IsNullOrWhiteSpace(record?.Trust.ReviewStatus) ? null : record.Trust.ReviewStatus,
            record?.Presentation.Summary,
            record?.Lifecycle.UpdatedAt,
            BuildSourceReferences(record),
            record?.Entities.Select(x => x.Name).ToArray() ?? Array.Empty<string>(),
            record?.Entities.Select(x => new PublishEntitySummary(x.Type, x.Name, x.Description)).ToArray() ?? Array.Empty<PublishEntitySummary>(),
            BuildRepresentationKinds(entry, model),
            schemaTypes,
            Array.Empty<PublishSemanticOutlineItem>(),
            SitemapIncluded: false,
            SearchIncluded: false,
            RssIncluded: false,
            AtomFeedIncluded: false,
            JsonFeedIncluded: false,
            LlmsIncluded: false,
            LlmsFullIncluded: false,
            RobotsIncluded: false,
            ManifestIncluded: false,
            model,
            record);
    }

    internal static DateTimeOffset? NormalizeLastModified(DateTimeOffset lastModified)
        => lastModified == DateTimeOffset.UnixEpoch ? null : lastModified;

    private static IReadOnlyList<string> BuildSourceReferences(ContentRecord? record)
    {
        if (record is null)
        {
            return Array.Empty<string>();
        }

        return new[] { record.Provenance.OriginalSource }
            .Concat(record.Provenance.Citations)
            .Concat(record.Provenance.References)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal static IReadOnlyList<string> BuildRepresentationKinds(SeoIndexEntry entry, SeoModel? model)
    {
        var values = PublishRepresentationRegistry.DocumentKinds(model?.JsonLd.Count > 0).ToList();
        if (entry.Indexable)
        {
            values.Add("search");
            values.Add("sitemap");
        }

        return values;
    }
}
