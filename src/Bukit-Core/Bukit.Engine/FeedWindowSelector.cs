namespace Bukit.Engine;

internal static class FeedWindowSelector
{
    internal const int DefaultLimit = 20;

    internal static IReadOnlyList<T> Select<T>(
        IEnumerable<T> candidates,
        Func<T, DateTimeOffset> publishedAt,
        Func<T, string> canonicalUrl,
        int configuredLimit)
    {
        var limit = configuredLimit > 0 ? configuredLimit : DefaultLimit;
        return candidates
            .OrderByDescending(publishedAt)
            .ThenBy(canonicalUrl, StringComparer.OrdinalIgnoreCase)
            .GroupBy(canonicalUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(limit)
            .ToArray();
    }
}
