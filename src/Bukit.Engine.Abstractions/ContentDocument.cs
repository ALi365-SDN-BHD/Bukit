namespace Bukit.Engine.Abstractions.Content;

public sealed record ContentDocument(
    ContentRecord Record,
    ContentBodyRef Body,
    ContentRoutePolicy Route,
    ContentPublishPolicy Publish,
    IReadOnlyDictionary<string, ContentField> CustomFields,
    IReadOnlyList<ContentDiagnostic> Diagnostics);

public sealed record ContentBodyRef(
    string? Html,
    string? BodyKey,
    string? Markdown,
    string? PlainText);

public sealed record ContentRoutePolicy(
    string? Url,
    string? OutputPath,
    string? Template,
    string? PermalinkPattern,
    string? ListGroup);

public sealed record ContentPublishPolicy(
    bool Draft,
    bool NoIndex,
    bool NoFollow,
    bool ExcludeFromFeed,
    bool ExcludeFromSearch,
    bool ExcludeFromSitemap,
    bool IsDataModule);

public sealed record ContentDiagnostic(
    string Code,
    string Severity,
    string Message,
    string? Field,
    string? SourceId);
