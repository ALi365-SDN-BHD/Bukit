namespace Bukit.Config;

/// <summary>
/// The key of each <c>content.sources</c> entry: its trimmed <c>name</c>, else its lower-case type,
/// numbered (<c>notion1</c>, <c>notion2</c>) when several unnamed sources share a type.
/// </summary>
internal static class ContentSourceKeys
{
    /// <summary>Keys in source order; an entry without a type gets an empty key.</summary>
    public static string[] Compute(IReadOnlyList<ContentSourceConfig> sources)
    {
        var typeCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            var type = (source.Type ?? string.Empty).Trim();
            if (type.Length > 0)
            {
                typeCounts[type] = typeCounts.GetValueOrDefault(type) + 1;
            }
        }

        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var keys = new string[sources.Count];
        for (var i = 0; i < sources.Count; i++)
        {
            var source = sources[i];
            var type = (source.Type ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(source.Name))
            {
                keys[i] = source.Name.Trim();
            }
            else if (type.Length == 0)
            {
                keys[i] = string.Empty;
            }
            else if (typeCounts[type] > 1)
            {
                seen[type] = seen.GetValueOrDefault(type) + 1;
                keys[i] = $"{type.ToLowerInvariant()}{seen[type]}";
            }
            else
            {
                keys[i] = type.ToLowerInvariant();
            }
        }

        return keys;
    }
}
