namespace Bukit.Content.CloudBase;

internal sealed record CloudBaseContentSourceOptions
{
    public required string EnvId { get; init; }
    public string Region { get; init; } = "cn";
    public required string Table { get; init; }
    public string IdField { get; init; } = "id";
    public int PageSize { get; init; } = 100;
    public int? MaxItems { get; init; }
    public string? FilterField { get; init; }
    public string? FilterValue { get; init; }
    public string? SortField { get; init; }
    public string SortDirection { get; init; } = "asc";
    public string TitleField { get; init; } = "title";
    public string SlugField { get; init; } = "slug";
    public string PublishAtField { get; init; } = "publish_at";
    public string UpdatedAtField { get; init; } = "updated_at";
    public string BodyField { get; init; } = string.Empty;
    public string Mode { get; init; } = "content";
    public string FieldPolicyMode { get; init; } = "whitelist";
    public IReadOnlyList<string>? AllowedFields { get; init; }
    public string PublishableKey { get; init; } = string.Empty;
    public int MaxRps { get; init; } = 5;
    public int MaxRetries { get; init; } = 3;
    public int MaxResponseBytes { get; init; } = 8 * 1024 * 1024;
}
