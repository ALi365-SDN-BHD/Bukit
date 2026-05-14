using Bukit.Routing;

namespace Bukit.Engine.Plugins;

public sealed record SeoIndexEntry(
    RouteInfo Route,
    string Canonical,
    string? Robots,
    bool Indexable,
    DateTimeOffset LastModified,
    string? SourceItemId,
    string? ContentType);
