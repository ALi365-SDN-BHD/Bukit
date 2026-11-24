using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Feeds;
using Bukit.Engine.Primitives;

namespace Bukit.Engine.Audit;

/// <summary>
/// Answers whether a route is included in each site-wide publish output: projection results
/// win when present, otherwise the output text is searched for the route URL or canonical.
/// An output text is scanned at most once, on its first lookup, for all routes together.
/// </summary>
internal sealed class PublishOutputInclusion
{
    private readonly Dictionary<string, Dictionary<string, PublishRepresentationOutput>> _projections;
    private readonly SeoIndexEntry[] _entries;
    private readonly Dictionary<string, HashSet<string>> _hitsByOutput = new(ReferenceEqualityComparer.Instance);
    private OrdinalIgnoreCaseMultiMatcher.CompiledPatterns? _routeNeedles;
    private HashSet<string>? _knownNeedles;

    internal PublishOutputInclusion(
        IEnumerable<SeoIndexEntry> entries,
        IReadOnlyList<PublishProjectionResult>? projectionResults)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [.. entries];
        _projections = BuildProjections(projectionResults);
    }

    internal static string SitemapLocNeedle(string canonical) => $"<loc>{SitemapUri.Encode(canonical)}</loc>";

    /// <summary>
    /// Same result as <c>outputText.Contains(needle, StringComparison.OrdinalIgnoreCase)</c> for
    /// a route URL, canonical or <see cref="SitemapLocNeedle"/>; false when either is null.
    /// </summary>
    internal bool OutputContains(string? outputText, string? needle)
    {
        if (outputText is null || needle is null)
        {
            return false;
        }

        if (!_hitsByOutput.TryGetValue(outputText, out var hits))
        {
            EnsureRouteNeedles();
            hits = _routeNeedles!.FindPresent(outputText);
            _hitsByOutput[outputText] = hits;
        }

        // Needles outside the indexed routes are not in the finder; answer them directly.
        return _knownNeedles!.Contains(needle)
            ? hits.Contains(needle)
            : outputText.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    internal bool TryGetProjected(string kind, SeoIndexEntry entry, out bool included)
    {
        ArgumentNullException.ThrowIfNull(entry);
        included = false;
        if (!_projections.TryGetValue(kind, out var byUrl))
        {
            return false;
        }

        if (byUrl.TryGetValue(entry.Route.Url, out var output) ||
            byUrl.TryGetValue(entry.Canonical, out output))
        {
            included = output.Exists;
            return true;
        }

        return false;
    }

    private void EnsureRouteNeedles()
    {
        if (_routeNeedles is not null)
        {
            return;
        }

        _knownNeedles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in _entries)
        {
            AddKnown(entry.Route?.Url);
            AddKnown(entry.Canonical);
            AddKnown(entry.Canonical is null ? null : SitemapLocNeedle(entry.Canonical));
        }

        _routeNeedles = OrdinalIgnoreCaseMultiMatcher.Compile(_knownNeedles);
    }

    private void AddKnown(string? needle)
    {
        if (needle is not null)
        {
            _knownNeedles!.Add(needle);
        }
    }

    private static Dictionary<string, Dictionary<string, PublishRepresentationOutput>> BuildProjections(
        IReadOnlyList<PublishProjectionResult>? projectionResults)
    {
        var result = new Dictionary<string, Dictionary<string, PublishRepresentationOutput>>(StringComparer.OrdinalIgnoreCase);
        if (projectionResults is null)
        {
            return result;
        }

        foreach (var group in projectionResults.SelectMany(x => x.Outputs).GroupBy(x => x.Kind, StringComparer.OrdinalIgnoreCase))
        {
            result[group.Key] = group
                .Where(x => !string.IsNullOrWhiteSpace(x.Url))
                .GroupBy(x => x.Url, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);
        }

        return result;
    }
}
