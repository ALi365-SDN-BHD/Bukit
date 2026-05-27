namespace Bukit.Engine.Abstractions.Content;

public sealed record ContentItem(
    string Id,
    string Title,
    string Slug,
    DateTimeOffset PublishAt,
    string? ContentHtml,
    IReadOnlyDictionary<string, object> Meta,
    IReadOnlyDictionary<string, ContentField>? Fields = null,
    string? BodyKey = null
);
