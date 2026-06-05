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
    string? SourceItemId,
    DateTimeOffset LastModified,
    string? Title,
    string? Description,
    string? Language,
    string? Author,
    string? Organization,
    string? Source,
    string? OriginalSource,
    string? ReviewStatus,
    IReadOnlyList<string> EntityNames,
    IReadOnlyList<string> RepresentationKinds,
    IReadOnlyList<string> SchemaTypes,
    SeoModel? SeoModel,
    ContentRecord? ContentRecord);

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
            entry.SourceItemId,
            entry.LastModified,
            model?.Title,
            model?.Description,
            record?.Presentation.Language,
            record?.Ownership.Author ?? model?.Article.Author,
            record?.Ownership.Organization,
            record?.Provenance.Source,
            record?.Provenance.OriginalSource,
            string.IsNullOrWhiteSpace(record?.Trust.ReviewStatus) ? null : record.Trust.ReviewStatus,
            record?.Entities.Select(x => x.Name).ToArray() ?? Array.Empty<string>(),
            BuildRepresentationKinds(entry, model),
            schemaTypes,
            model,
            record);
    }

    internal static IReadOnlyList<string> BuildRepresentationKinds(SeoIndexEntry entry, SeoModel? model)
    {
        var values = new List<string> { "html", "json", "markdown" };
        if (model?.JsonLd.Count > 0)
        {
            values.Add("jsonld");
        }

        if (entry.Indexable)
        {
            values.Add("search");
            values.Add("sitemap");
        }

        return values;
    }
}
