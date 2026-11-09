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
}
