using System.Collections.ObjectModel;
using Bukit.Engine;

namespace Bukit.Cli.Commands.SeoInsights;

internal enum SeoObservationMatchKind
{
    Matched,
    Unmatched,
    Ambiguous
}

internal sealed record SeoObservationRouteCandidate(
    string RouteKey,
    string? ContentKey,
    string Route,
    string Canonical);

internal sealed record SeoObservationRouteMatch(
    SeoObservationMatchKind Kind,
    string ObservedUrl,
    string? NormalizedUrl,
    string? RouteKey,
    string? ContentKey,
    IReadOnlyList<SeoObservationRouteCandidate> Candidates,
    string? ErrorCode);

internal sealed class SeoObservationRouteMatcher
{
    private static readonly IReadOnlyList<SeoObservationRouteCandidate> NoCandidates =
        Array.Empty<SeoObservationRouteCandidate>();

    private readonly SeoObservationUrlOptions _options;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<SeoObservationRouteCandidate>> _index;

    internal SeoObservationRouteMatcher(SeoRouteMap routeMap, SeoObservationUrlOptions options)
    {
        _options = Snapshot(options);
        var candidatesByMatchKey = new Dictionary<string, List<SeoObservationRouteCandidate>>(StringComparer.Ordinal);
        foreach (var route in routeMap.Routes)
        {
            var matchKey = NormalizeCanonical(route.Canonical, _options);
            if (!candidatesByMatchKey.TryGetValue(matchKey, out var candidates))
            {
                candidates = [];
                candidatesByMatchKey.Add(matchKey, candidates);
            }

            candidates.Add(new SeoObservationRouteCandidate(
                route.RouteKey,
                route.ContentKey,
                route.Route,
                route.Canonical));
        }

        _index = new ReadOnlyDictionary<string, IReadOnlyList<SeoObservationRouteCandidate>>(
            candidatesByMatchKey.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<SeoObservationRouteCandidate>)Array.AsReadOnly(pair.Value
                    .OrderBy(candidate => candidate.RouteKey, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.ContentKey, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Route, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Canonical, StringComparer.Ordinal)
                    .ToArray()),
                StringComparer.Ordinal));
    }

    internal SeoObservationRouteMatch Match(string observedUrl)
    {
        var normalization = SeoObservationUrlNormalizer.Normalize(observedUrl, _options);
        if (!normalization.Success)
        {
            return new SeoObservationRouteMatch(
                SeoObservationMatchKind.Unmatched,
                observedUrl,
                null,
                null,
                null,
                NoCandidates,
                normalization.ErrorCode);
        }

        if (!_index.TryGetValue(normalization.MatchKey!, out var candidates))
        {
            return new SeoObservationRouteMatch(
                SeoObservationMatchKind.Unmatched,
                observedUrl,
                normalization.NormalizedUrl,
                null,
                null,
                NoCandidates,
                null);
        }

        if (candidates.Count != 1)
        {
            return new SeoObservationRouteMatch(
                SeoObservationMatchKind.Ambiguous,
                observedUrl,
                normalization.NormalizedUrl,
                null,
                null,
                candidates,
                null);
        }

        var candidate = candidates[0];
        return new SeoObservationRouteMatch(
            SeoObservationMatchKind.Matched,
            observedUrl,
            normalization.NormalizedUrl,
            candidate.RouteKey,
            candidate.ContentKey,
            candidates,
            null);
    }

    private static SeoObservationUrlOptions Snapshot(SeoObservationUrlOptions options)
        => new(
            options.SiteHost,
            new HashSet<string>(options.HostAliases, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(options.IgnoredQueryParameters, StringComparer.OrdinalIgnoreCase));

    private static string NormalizeCanonical(string value, SeoObservationUrlOptions options)
    {
        string absolute;
        SeoObservationUrlOptions canonicalOptions;
        if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal))
        {
            absolute = "https://seo-canonical.invalid" + value;
            canonicalOptions = options with
            {
                SiteHost = "seo-canonical.invalid",
                HostAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };
        }
        else if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            absolute = value;
            canonicalOptions = options with
            {
                SiteHost = uri.IdnHost,
                HostAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            };
        }
        else
        {
            throw new InvalidDataException($"Invalid SEO route-map canonical '{value}'.");
        }

        var normalized = SeoObservationUrlNormalizer.Normalize(absolute, canonicalOptions);
        if (!normalized.Success)
        {
            throw new InvalidDataException(
                $"Invalid SEO route-map canonical '{value}' ({normalized.ErrorCode}).");
        }

        return normalized.MatchKey!;
    }
}
