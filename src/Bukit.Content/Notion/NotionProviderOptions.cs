namespace Bukit.Content.Notion;

public sealed record NotionProviderOptions
{
    public required string DatabaseId { get; init; }
    public required string Token { get; init; }
    public int PageSize { get; init; } = 50;
    public int? MaxItems { get; init; }
    public int RequestDelayMs { get; init; }
    public int MaxRetries { get; init; } = 5;
    public int? RenderConcurrency { get; init; }
    public int? MaxRps { get; init; }
    public string FieldPolicyMode { get; init; } = "whitelist";
    public IReadOnlyList<string>? AllowedFields { get; init; }
    public string FilterProperty { get; init; } = "Published";
    public string FilterType { get; init; } = "checkbox_true";
    public string? SortProperty { get; init; }
    public string SortDirection { get; init; } = "ascending";
    public bool RenderContent { get; init; } = true;
    public IReadOnlyList<string>? IncludeSlugs { get; init; }
    public string IncludeSlugProperty { get; init; } = "Slug";
    public string CacheMode { get; init; } = "off";
    public string? CacheDir { get; init; }
}
