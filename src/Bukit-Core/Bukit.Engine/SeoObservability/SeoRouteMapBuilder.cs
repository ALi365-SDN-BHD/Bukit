using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Rendering;

namespace Bukit.Engine;

internal sealed class SeoRouteMapBuilder
{
    internal const string Schema = "https://bukit.dev/schemas/seo-route-map.v1.json";
    internal const string SchemaVersion = "1.0";

    private readonly string _siteUrl;
    private readonly string _baseUrl;
    private readonly List<SeoRouteMapEntry> _routes = [];

    internal SeoRouteMapBuilder(string? siteUrl, string baseUrl)
    {
        _siteUrl = siteUrl ?? string.Empty;
        _baseUrl = baseUrl;
    }

    internal void Add(SeoIndexEntry entry, SeoModel? model, ContentRecord? record)
    {
        var canonical = string.IsNullOrWhiteSpace(model?.Canonical)
            ? entry.Canonical
            : model.Canonical;
        var language = record?.Presentation.Language;
        _routes.Add(new SeoRouteMapEntry(
            SeoObservationIdentity.CreateRouteKey(entry.Route.Url, canonical),
            SeoObservationIdentity.CreateContentKey(record, language ?? string.Empty),
            entry.Route.Url,
            canonical,
            language,
            record?.Identity.ContentType,
            record?.Classification.Collection,
            entry.Indexable,
            record?.Lifecycle.PublishedAt,
            record?.Lifecycle.UpdatedAt));
    }

    internal SeoRouteMap Build(DateTimeOffset generatedAt)
        => new(
            Schema,
            SchemaVersion,
            generatedAt,
            _siteUrl,
            _baseUrl,
            _routes
                .OrderBy(route => route.Canonical, StringComparer.Ordinal)
                .ThenBy(route => route.RouteKey, StringComparer.Ordinal)
                .ToArray());
}
