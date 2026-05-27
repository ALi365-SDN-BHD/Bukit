using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Engine;

internal static class MetaHelpers
{
    internal static string? GetString(IReadOnlyDictionary<string, object> meta, string key)
    {
        return meta.TryGetValue(key, out var v) && v is not null ? v.ToString() : null;
    }

    internal static string? GetEffectiveCollection(ContentItem item, string? defaultCollection = null)
    {
        if (item.Meta.TryGetValue("collection", out var c) && c is not null && !string.IsNullOrWhiteSpace(c.ToString()))
        {
            return c.ToString();
        }

        if (item.Meta.TryGetValue("type", out var t) && t is not null && !string.IsNullOrWhiteSpace(t.ToString()))
        {
            return t.ToString();
        }

        return defaultCollection;
    }

    internal static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var v) || v is null)
        {
            return null;
        }

        if (v is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (v is IEnumerable<object> seq)
        {
            var list = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return list.Count == 0 ? null : list;
        }

        return null;
    }

    internal static bool TryGetI18nKey(IReadOnlyDictionary<string, object> meta, out string key)
    {
        key = string.Empty;

        object? v = null;
        if (!meta.TryGetValue("i18nKey", out v) || v is null)
        {
            meta.TryGetValue("i18n_key", out v);
        }

        var s = v?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        key = s;
        return true;
    }

    internal static bool IsDataItem(ContentItem item)
    {
        return item.Meta.TryGetValue("sourceMode", out var v) &&
               v is not null &&
               string.Equals(v.ToString(), "data", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool? TryGetBoolField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null)
        {
            return null;
        }

        if (!fields.TryGetValue(key, out var field))
        {
            var alt = fields.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (alt is null || !fields.TryGetValue(alt, out field))
            {
                return null;
            }
        }

        return field.Value switch
        {
            null => null,
            bool b => b,
            string s when bool.TryParse(s, out var b) => b,
            int i => i != 0,
            long l => l != 0,
            double d => Math.Abs(d) > double.Epsilon,
            _ => null
        };
    }

    internal static string? TryGetTextField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null)
        {
            return null;
        }

        if (!fields.TryGetValue(key, out var field))
        {
            var alt = fields.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (alt is null || !fields.TryGetValue(alt, out field))
            {
                return null;
            }
        }

        return field.Value switch
        {
            null => null,
            string s => s,
            _ => field.Value.ToString()
        };
    }

    internal static double? TryGetNumberField(IReadOnlyDictionary<string, ContentField>? fields, string key)
    {
        if (fields is null)
        {
            return null;
        }

        if (!fields.TryGetValue(key, out var field))
        {
            var alt = fields.Keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            if (alt is null || !fields.TryGetValue(alt, out field))
            {
                return null;
            }
        }

        return field.Value switch
        {
            null => null,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            string s when double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) => d,
            _ => null
        };
    }
}
