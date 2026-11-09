using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Primitives;

namespace Bukit.Engine;

internal static class FeedWindowSelector
{
    internal const int DefaultLimit = 20;

    internal static IReadOnlyList<T> Select<T>(
        IEnumerable<T> candidates,
        Func<T, DateTimeOffset> publishedAt,
        Func<T, string> canonicalUrl,
        int configuredLimit,
        Func<T, string?>? tieBreakValue = null,
        ContentTieBreak? tieBreak = null)
    {
        var limit = configuredLimit > 0 ? configuredLimit : DefaultLimit;
        var byDate = candidates.OrderByDescending(publishedAt);
        // content.ordering decides which same-time entries make the window, as on list pages.
        var ordered = tieBreakValue is null || tieBreak is null
            ? byDate
            : byDate.ThenBy(tieBreakValue, tieBreak.Comparer);
        return ordered
            .ThenBy(canonicalUrl, StringComparer.Ordinal)
            .GroupBy(canonicalUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(limit)
            .ToArray();
    }

    /// <summary>
    /// Predicts the window the feed writers produce when some documents may be missing. An entry without
    /// its document has an unknown content.ordering value, so when one shares the window's edge time, which
    /// of the entries at that edge made the feed cannot be known: they are left out instead of guessed.
    /// Without content.ordering every value is known and this matches <see cref="Select{T}"/>.
    /// </summary>
    internal static IReadOnlyList<T> PredictWindow<T>(
        IEnumerable<T> candidates,
        Func<T, DateTimeOffset> publishedAt,
        Func<T, string> canonicalUrl,
        int configuredLimit,
        AppConfig config,
        Func<T, ContentDocument?> document,
        Func<T, string> routeUrl)
    {
        var tieBreak = ContentOrdering.Resolve(config);
        (T Candidate, bool Known, string? Value)[] keyed =
        [
            .. candidates.Select(candidate =>
            {
                var source = tieBreak is null ? null : document(candidate);
                var value = tieBreak is not null && source is not null
                    ? ContentOrdering.ReadValue(source, routeUrl(candidate), tieBreak.Field)
                    : null;
                return (candidate, tieBreak is null || source is not null, value);
            })
        ];
        var window = Select(keyed, x => publishedAt(x.Candidate), x => canonicalUrl(x.Candidate), configuredLimit, x => x.Value, tieBreak);
        if (window.Count > 0)
        {
            var edge = publishedAt(window[^1].Candidate);
            var tiedAtEdge = keyed.Count(x => publishedAt(x.Candidate) == edge);
            if (tiedAtEdge > window.Count(x => publishedAt(x.Candidate) == edge) &&
                keyed.Any(x => !x.Known && publishedAt(x.Candidate) == edge))
            {
                return [.. window.Where(x => publishedAt(x.Candidate) != edge).Select(x => x.Candidate)];
            }
        }

        return [.. window.Select(x => x.Candidate)];
    }
}
