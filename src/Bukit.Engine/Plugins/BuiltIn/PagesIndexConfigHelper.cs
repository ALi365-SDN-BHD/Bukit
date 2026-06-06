using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Routing;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class PagesIndexConfigHelper
{
    public static bool TryGetMap(IReadOnlyDictionary<string, object> map, string key, out IReadOnlyDictionary<string, object> value)
    {
        value = null!;
        if (!map.TryGetValue(key, out var obj) || obj is null)
        {
            return false;
        }

        if (obj is IReadOnlyDictionary<string, object> ro)
        {
            value = ro;
            return true;
        }

        if (obj is IDictionary<string, object> dict)
        {
            value = new Dictionary<string, object>(dict, StringComparer.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }

    public static string? TryGetString(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var obj) || obj is null)
        {
            return null;
        }

        if (obj is string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        var text = obj.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public static bool TryGetBool(IReadOnlyDictionary<string, object> map, string key, bool defaultValue)
    {
        if (!map.TryGetValue(key, out var obj) || obj is null)
        {
            return defaultValue;
        }

        if (obj is bool b)
        {
            return b;
        }

        if (bool.TryParse(obj.ToString(), out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    public static int TryGetInt(IReadOnlyDictionary<string, object> map, string key, int defaultValue)
    {
        if (!map.TryGetValue(key, out var obj) || obj is null)
        {
            return defaultValue;
        }

        if (obj is int i)
        {
            return i;
        }

        if (obj is long l)
        {
            return (int)l;
        }

        if (int.TryParse(obj.ToString(), out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    public static int? TryGetNullableInt(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var obj) || obj is null)
        {
            return null;
        }

        if (obj is int i)
        {
            return i;
        }

        if (obj is long l)
        {
            return (int)l;
        }

        return int.TryParse(obj.ToString(), out var parsed) ? parsed : null;
    }

    public static IReadOnlyList<string> TryGetStringList(IReadOnlyDictionary<string, object> map, string key)
    {
        if (!map.TryGetValue(key, out var obj) || obj is null)
        {
            return Array.Empty<string>();
        }

        if (obj is IEnumerable<object> seq)
        {
            var items = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            return items;
        }

        if (obj is string s)
        {
            var items = s.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
            return items;
        }

        return Array.Empty<string>();
    }

    public static bool HasNotionContent(AppConfig config)
    {
        if (string.Equals(config.Content.Provider, "notion", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (config.Content.Sources is null || config.Content.Sources.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < config.Content.Sources.Count; i++)
        {
            if (config.Content.Sources[i].Notion is not null)
            {
                return true;
            }
        }

        return false;
    }

    public static List<string> CollectRelationIds(
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlyList<string> fieldKeys,
        Dictionary<string, object> index,
        int maxItems)
    {
        var knownRawIds = BuildKnownRawIdSet(index);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        for (var i = 0; i < routed.Count; i++)
        {
            var fields = routed[i].Document.Fields;
            if (fields is null || fields.Count == 0)
            {
                continue;
            }

            for (var k = 0; k < fieldKeys.Count; k++)
            {
                var key = fieldKeys[k];
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (!fields.TryGetValue(key, out var f) || f.Value is null)
                {
                    continue;
                }

                if (f.Value is not IEnumerable<string> ids)
                {
                    continue;
                }

                foreach (var raw in ids)
                {
                    var id = (raw ?? string.Empty).Trim();
                    if (id.Length == 0 || index.ContainsKey(id) || knownRawIds.Contains(id))
                    {
                        continue;
                    }

                    if (set.Add(id))
                    {
                        list.Add(id);
                        if (list.Count >= maxItems)
                        {
                            return list;
                        }
                    }
                }
            }
        }

        return list;
    }

    public static HashSet<string> BuildKnownRawIdSet(Dictionary<string, object> index)
    {
        var rawIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in index.Keys)
        {
            rawIds.Add(key);
            var colonIdx = key.IndexOf(':');
            if (colonIdx >= 0 && colonIdx < key.Length - 1)
            {
                rawIds.Add(key.Substring(colonIdx + 1));
            }
        }

        return rawIds;
    }
}
