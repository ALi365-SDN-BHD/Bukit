namespace Bukit.Engine.Abstractions.Content;

public sealed record RawContentDocument
{
    public RawContentDocument(
        string Id,
        string Title,
        string Slug,
        DateTimeOffset PublishAt,
        RawBody Body,
        IReadOnlyDictionary<string, RawContentValue>? Properties = null,
        ContentSourceInfo? Source = null,
        IReadOnlyDictionary<string, ContentField>? CustomFields = null)
    {
        this.Id = Id;
        this.Title = Title;
        this.Slug = Slug;
        this.PublishAt = PublishAt;
        this.Body = Body;
        this.Properties = Properties;
        this.Source = Source ?? ContentSourceInfo.Unknown;
        this.CustomFields = CustomFields;
    }

    public string Id { get; init; }
    public string Title { get; init; }
    public string Slug { get; init; }
    public DateTimeOffset PublishAt { get; init; }
    public RawBody Body { get; init; }
    public IReadOnlyDictionary<string, RawContentValue>? Properties { get; init; }
    public ContentSourceInfo Source { get; init; }
    public IReadOnlyDictionary<string, ContentField>? CustomFields { get; init; }
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
    string? Provider,
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
