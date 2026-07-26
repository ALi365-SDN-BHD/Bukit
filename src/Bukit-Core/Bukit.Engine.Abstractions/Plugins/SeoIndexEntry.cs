using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Abstractions.Plugins;

public sealed record SeoIndexEntry(
    RouteInfo Route,
    string Canonical,
    string? Robots,
    bool Indexable,
    DateTimeOffset LastModified,
    string? SourceItemId,
    string? ContentType,
    bool IsDerived = false,
    string? Collection = null);
