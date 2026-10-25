using System.Text.Json.Serialization;

namespace Bukit.Engine;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(SeoRouteMap))]
internal sealed partial class SeoRouteMapJsonContext : JsonSerializerContext;

internal sealed record SeoRouteMap(
    string Schema,
    string SchemaVersion,
    DateTimeOffset GeneratedAt,
    string SiteUrl,
    string BaseUrl,
    IReadOnlyList<SeoRouteMapEntry> Routes);

internal sealed record SeoRouteMapEntry(
    string RouteKey,
    string? ContentKey,
    string Route,
    string Canonical,
    string? Language,
    string? ContentType,
    string? Collection,
    bool Indexable,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? UpdatedAt);
