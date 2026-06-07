namespace Bukit.Engine.Abstractions.Content;

public sealed record RawContentDocument
{
    public RawContentDocument(
        string SourceId,
        string SourceKind,
        string Title,
        string Slug,
        DateTimeOffset? PublishedAt,
        RawBody Body,
        IReadOnlyDictionary<string, RawContentValue>? Properties = null,
        ContentSourceInfo? Source = null,
        IReadOnlyDictionary<string, ContentField>? CustomFields = null)
    {
        this.SourceId = SourceId;
        this.SourceKind = string.IsNullOrWhiteSpace(SourceKind)
            ? Source?.Provider ?? "unknown"
            : SourceKind;
        this.Title = Title;
        this.Slug = Slug;
        this.PublishedAt = PublishedAt;
        this.Body = Body;
        this.Properties = Properties;
        this.Source = Source ?? ContentSourceInfo.Unknown;
        this.CustomFields = CustomFields;
    }

    public RawContentDocument(
        string Id,
        string Title,
        string Slug,
        DateTimeOffset PublishAt,
        RawBody Body,
        IReadOnlyDictionary<string, RawContentValue>? Properties = null,
        ContentSourceInfo? Source = null,
        IReadOnlyDictionary<string, ContentField>? CustomFields = null)
        : this(
            SourceId: Id,
            SourceKind: Source?.Provider ?? "unknown",
            Title: Title,
            Slug: Slug,
            PublishedAt: PublishAt,
            Body: Body,
            Properties: Properties,
            Source: Source,
            CustomFields: CustomFields)
    {
    }

    public string SourceId { get; init; }
    public string SourceKind { get; init; }
    public string Title { get; init; }
    public string Slug { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }
    public RawBody Body { get; init; }
    public IReadOnlyDictionary<string, RawContentValue>? Properties { get; init; }
    public ContentSourceInfo Source { get; init; }
    public IReadOnlyDictionary<string, ContentField>? CustomFields { get; init; }

    public string Id => SourceId;
    public DateTimeOffset PublishAt => PublishedAt ?? DateTimeOffset.UnixEpoch;
}

public sealed record RawBody(
    string? InlineHtml = null,
    string? BodyKey = null,
    string? Markdown = null,
    string? PlainText = null);

public sealed record RawContentValue(
    string Kind,
    object? Value)
{
    public static IReadOnlyDictionary<string, RawContentValue>? FromFields(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null)
        {
            return null;
        }

        return fields.ToDictionary(
            x => x.Key,
            x => new RawContentValue(x.Value.Type, x.Value.Value),
            StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record ContentSourceInfo(
    string Provider,
    string? SourceKey = null,
    string? SourcePath = null,
    string? ExternalId = null,
    Uri? ExternalUrl = null,
    DateTimeOffset? SyncedAt = null,
    string? SyncStatus = null)
{
    public static readonly ContentSourceInfo Unknown = new("unknown");
}

public sealed record RawContentLoadResult(
    IReadOnlyList<RawContentDocument> Documents,
    IContentBodyStore BodyStore);
