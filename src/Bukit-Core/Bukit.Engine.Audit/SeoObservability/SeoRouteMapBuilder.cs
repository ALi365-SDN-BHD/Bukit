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
        var normalizedSiteUrl = string.IsNullOrWhiteSpace(siteUrl) ? string.Empty : siteUrl.Trim();
        _siteUrl = normalizedSiteUrl.Length == 0 || IsAbsoluteHttpUrlWithoutCredentials(normalizedSiteUrl)
            ? normalizedSiteUrl
            : string.Empty;
        _baseUrl = baseUrl;
    }

    internal void Add(SeoIndexEntry entry, SeoModel? model, ContentRecord? record)
    {
        var requestedCanonical = string.IsNullOrWhiteSpace(model?.Canonical)
            ? entry.Canonical
            : model.Canonical;
        var canonical = IsValidObservabilityCanonical(requestedCanonical)
            ? requestedCanonical
            : SafeRelativeCanonical(entry.Route.Url);
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

    private static bool IsValidObservabilityCanonical(string value)
        => value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal) ||
           IsAbsoluteHttpUrlWithoutCredentials(value);

    private static bool IsAbsoluteHttpUrlWithoutCredentials(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
           uri.Scheme is "http" or "https" &&
           !string.IsNullOrWhiteSpace(uri.Host) &&
           string.IsNullOrEmpty(uri.UserInfo);

    private static string SafeRelativeCanonical(string route)
        => route.StartsWith("/", StringComparison.Ordinal) && !route.StartsWith("//", StringComparison.Ordinal)
            ? route
            : "/";
}
