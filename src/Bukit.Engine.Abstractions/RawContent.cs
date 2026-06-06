namespace Bukit.Engine.Abstractions.Content;

public sealed record RawContentDocument(
    string SourceId,
    string SourceKind,
    string Title,
    string? Slug,
    DateTimeOffset? PublishedAt,
    RawBody Body,
    IReadOnlyDictionary<string, RawContentValue> Properties,
    ContentSourceInfo Source,
    IReadOnlyDictionary<string, ContentField> CustomFields);

public sealed record RawBody(
    string? InlineHtml,
    string? BodyKey,
    string? Markdown,
    string? PlainText);

public sealed record RawContentValue(
    string Kind,
    object? Value);

public sealed record ContentSourceInfo(
    string Provider,
    string? SourceKey,
    string? SourcePath,
    string? ExternalId,
    Uri? ExternalUrl,
    DateTimeOffset? SyncedAt,
    string? SyncStatus);
