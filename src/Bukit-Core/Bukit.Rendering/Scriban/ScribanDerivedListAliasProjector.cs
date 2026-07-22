using Bukit.Engine.Abstractions.Content;
using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

internal static class ScribanDerivedListAliasProjector
{
    internal static void AddAliases(ScriptObject root, IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (!IsDerivedListLikePage(fields))
        {
            return;
        }

        if (TryGetFieldValue(fields, "items", out var items))
        {
            var itemsValue = ScribanDynamicValueMapper.ToScribanValue(items);
            root.SetValue("items", itemsValue, readOnly: true);
            root.SetValue("pages", itemsValue, readOnly: true);
        }

        if (TryGetFieldValue(fields, "pagination", out var pagination))
        {
            root.SetValue("pagination", ScribanDynamicValueMapper.ToScribanValue(pagination), readOnly: true);
        }

        if (TryGetFieldValue(fields, "taxonomy", out var taxonomy))
        {
            root.SetValue("taxonomy", ScribanDynamicValueMapper.ToScribanValue(taxonomy), readOnly: true);
        }

        if (TryGetFieldValue(fields, "filter", out var filter))
        {
            root.SetValue("filter", ScribanDynamicValueMapper.ToScribanValue(filter), readOnly: true);
        }

        if (TryGetFieldValue(fields, "collection", out var collection))
        {
            root.SetValue("collection", ToCollectionAlias(collection), readOnly: true);
        }
    }

    private static object ToCollectionAlias(object? value)
    {
        if (value is IReadOnlyDictionary<string, object> or IDictionary<string, object>)
        {
            return ScribanDynamicValueMapper.ToScribanValue(value);
        }

        var obj = new ScriptObject();
        obj.SetValue("key", value?.ToString(), readOnly: true);
        return obj;
    }

    private static bool IsDerivedListLikePage(IReadOnlyDictionary<string, ContentField>? fields)
    {
        if (fields is null)
        {
            return false;
        }

        if (!TryGetFieldValue(fields, "items", out _))
        {
            return false;
        }

        if (!TryGetFieldValue(fields, "pagination", out _) && !TryGetFieldValue(fields, "taxonomy", out _))
        {
            return false;
        }

        return TryGetFieldValue(fields, "type", out var type) &&
               string.Equals(type?.ToString(), "derived", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFieldValue(
        IReadOnlyDictionary<string, ContentField>? fields,
        string key,
        out object? value)
    {
        value = null;
        if (fields is null)
        {
            return false;
        }

        foreach (var kv in fields)
        {
            if (!string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = kv.Value.Value;
            return true;
        }

        return false;
    }
}
