namespace Bukit.Engine.RouteMetadata;

internal sealed record RouteMetadataEntry(
    string Route,
    string Title,
    string Summary,
    string? SeoTitle,
    string? SeoDescription);
