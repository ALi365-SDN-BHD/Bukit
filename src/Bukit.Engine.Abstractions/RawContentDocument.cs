namespace Bukit.Engine.Abstractions.Content;

public sealed record RawContentDocument(
    string Id,
    string Title,
    string Slug,
    DateTimeOffset PublishAt,
    string? ContentHtml,
    IReadOnlyDictionary<string, ContentField>? Fields = null,
    string? BodyKey = null,
    string? SourceKind = null,
    string? SourcePath = null);

public sealed record RawContentLoadResult(
    IReadOnlyList<RawContentDocument> Documents,
    IContentBodyStore BodyStore);
