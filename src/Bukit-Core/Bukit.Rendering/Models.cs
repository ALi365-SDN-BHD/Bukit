using Bukit.Engine.Abstractions.Content;
namespace Bukit.Rendering;

public sealed record SiteModel
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    public string? Url { get; init; }
    public string? Description { get; init; }
    public required string BaseUrl { get; init; }
    public required string Language { get; init; }
    public int BuildYear { get; init; }
    public IReadOnlyDictionary<string, object>? Params { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? Modules { get; init; }
    public IReadOnlyDictionary<string, object>? Data { get; init; }
    public IReadOnlyDictionary<string, object>? DataIndex { get; init; }
}

public sealed record ModuleInfo
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string Content { get; init; }
    public IReadOnlyDictionary<string, ContentField>? Fields { get; init; }
}

public sealed record PageInfo
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public required string Content { get; init; }
    public string? Summary { get; init; }
    public IReadOnlyList<TableOfContentsEntry>? TableOfContents { get; init; }
    public DateTimeOffset? PublishDate { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public IReadOnlyDictionary<string, ContentField>? Fields { get; init; }
    public SeoModel? Seo { get; init; }
    public ContentRecord? ContentRecord { get; init; }
    public ContentRoutePolicy? Route { get; init; }
    public ContentPublishPolicy? Publish { get; init; }
    public IReadOnlyList<EntityRecord>? Entities { get; init; }
    public ProvenanceRecord? Provenance { get; init; }
    public TrustMetadata? Trust { get; init; }
    public IReadOnlyList<string>? Representations { get; init; }
}

public sealed record SeoModel
{
    public required string Title { get; init; }
    public string DocumentTitle { get; init; } = string.Empty;
    public string? Description { get; init; }
    public required string Canonical { get; init; }
    public string? Prev { get; init; }
    public string? Next { get; init; }
    public string? Robots { get; init; }
    internal SeoImageSource ImageSource { get; init; }
    public SeoOpenGraphModel Og { get; init; } = new();
    public SeoTwitterModel Twitter { get; init; } = new();
    public SeoArticleModel Article { get; init; } = new();
    public IReadOnlyList<SeoAlternateModel> Alternates { get; init; } = Array.Empty<SeoAlternateModel>();
    public IReadOnlyList<string> JsonLd { get; init; } = Array.Empty<string>();
    public string? SchemaType { get; init; }
    public IReadOnlyList<GeoFaqModel>? FaqItems { get; init; }
    public IReadOnlyList<GeoHowToStepModel>? HowToSteps { get; init; }
    public IReadOnlyList<GeoCitationModel>? Citations { get; init; }
    public GeoAuthorModel? GeoAuthor { get; init; }
    public string? SpeakableXPath { get; init; }
    public IReadOnlyList<string>? SameAs { get; init; }
}

internal enum SeoImageSource
{
    None,
    ExplicitField,
    ContentMedia,
    SiteDefault
}

public sealed record GeoFaqModel
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
}

public sealed record GeoHowToStepModel
{
    public required string Name { get; init; }
    public required string Text { get; init; }
    public string? Image { get; init; }
    public string? Url { get; init; }
}

public sealed record GeoCitationModel
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string Relation { get; init; } = "citation";
}

public sealed record GeoAuthorModel
{
    public required string Name { get; init; }
    public string? Url { get; init; }
    public IReadOnlyList<string> SameAs { get; init; } = Array.Empty<string>();
}

public sealed record SeoOpenGraphModel
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public string? Image { get; init; }
    public string Type { get; init; } = "website";
    public string? SiteName { get; init; }
    public string? Locale { get; init; }
}

public sealed record SeoTwitterModel
{
    public string Card { get; init; } = "summary";
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Image { get; init; }
    public string? Site { get; init; }
    public string? Creator { get; init; }
}

public sealed record SeoArticleModel
{
    public DateTimeOffset? PublishedTime { get; init; }
    public DateTimeOffset? ModifiedTime { get; init; }
    public string? Author { get; init; }
    public string? AuthorType { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

public sealed record SeoAlternateModel(string Hreflang, string Href);

public sealed record PageModel
{
    public required SiteModel Site { get; init; }
    public required PageInfo Page { get; init; }
    public IReadOnlyList<PageInfo> Pages { get; init; } = Array.Empty<PageInfo>();
}

public sealed record ListPaginationModel
{
    public int Page { get; init; } = 1;
    public int? PageSize { get; init; }
    public int TotalPages { get; init; } = 1;
    public int TotalItems { get; init; }
    public bool HasPrev { get; init; }
    public bool HasNext { get; init; }
    public string? PrevUrl { get; init; }
    public string? NextUrl { get; init; }
}

public sealed record ListCollectionModel
{
    public required string Key { get; init; }
}

public sealed record ListTaxonomyModel
{
    public required string Kind { get; init; }
    public string? Term { get; init; }
    public string? Slug { get; init; }
    public string? RoutePrefix { get; init; }
    public string? Url { get; init; }
    public bool IsIndex { get; init; }
}

public sealed record ListFilterModel
{
    public required string Field { get; init; }
    public string Operator { get; init; } = "equals";
    public string? Value { get; init; }
    public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();
}

public sealed record ListPageModel
{
    public required SiteModel Site { get; init; }
    public PageInfo? Page { get; init; }
    public required IReadOnlyList<PageInfo> Pages { get; init; }
    public IReadOnlyList<PageInfo>? Items { get; init; }
    public ListPaginationModel? Pagination { get; init; }
    public ListCollectionModel? Collection { get; init; }
    public ListTaxonomyModel? Taxonomy { get; init; }
    public ListFilterModel? Filter { get; init; }
    public SeoModel? Seo { get; init; }
}
