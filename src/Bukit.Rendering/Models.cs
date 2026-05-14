using Bukit.Content;

namespace Bukit.Rendering;

public sealed record SiteModel
{
    public required string Name { get; init; }
    public required string Title { get; init; }
    public string? Url { get; init; }
    public string? Description { get; init; }
    public required string BaseUrl { get; init; }
    public required string Language { get; init; }
    public IReadOnlyDictionary<string, object>? Params { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<ModuleInfo>>? Modules { get; init; }
    public IReadOnlyDictionary<string, object>? Data { get; init; }
    public AnalyticsModel Analytics { get; init; } = new();
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
    public DateTimeOffset? PublishDate { get; init; }
    public IReadOnlyDictionary<string, ContentField>? Fields { get; init; }
    public SeoModel? Seo { get; init; }
}

public sealed record AnalyticsModel
{
    public bool Enabled { get; init; } = true;
    public string? GoogleAnalyticsId { get; init; }
}

public sealed record SeoModel
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string Canonical { get; init; }
    public string? Robots { get; init; }
    public SeoOpenGraphModel Og { get; init; } = new();
    public SeoTwitterModel Twitter { get; init; } = new();
    public IReadOnlyList<SeoAlternateModel> Alternates { get; init; } = Array.Empty<SeoAlternateModel>();
    public IReadOnlyList<string> JsonLd { get; init; } = Array.Empty<string>();
}

public sealed record SeoOpenGraphModel
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Url { get; init; }
    public string? Image { get; init; }
    public string Type { get; init; } = "website";
}

public sealed record SeoTwitterModel
{
    public string Card { get; init; } = "summary";
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Image { get; init; }
    public string? Site { get; init; }
}

public sealed record SeoAlternateModel(string Hreflang, string Href);

public sealed record PageModel
{
    public required SiteModel Site { get; init; }
    public required PageInfo Page { get; init; }
}

public sealed record ListPageModel
{
    public required SiteModel Site { get; init; }
    public PageInfo? Page { get; init; }
    public required IReadOnlyList<PageInfo> Pages { get; init; }
}
