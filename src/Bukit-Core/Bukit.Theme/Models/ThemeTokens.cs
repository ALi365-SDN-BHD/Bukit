namespace Bukit.Theme;

public sealed class ThemeTokens
{
    public Dictionary<string, string>? Colors { get; set; }
    public Dictionary<string, string>? Font { get; set; }
    public Dictionary<string, string>? Radius { get; set; }
    public Dictionary<string, string>? Spacing { get; set; }
    public Dictionary<string, string>? Layout { get; set; }

    public ThemeTokens Merge(ThemeTokens parent)
    {
        return new ThemeTokens
        {
            Colors = MergeDict(Colors, parent.Colors),
            Font = MergeDict(Font, parent.Font),
            Radius = MergeDict(Radius, parent.Radius),
            Spacing = MergeDict(Spacing, parent.Spacing),
            Layout = MergeDict(Layout, parent.Layout)
        };
    }

    public ThemeTokens DeepMerge(ThemeTokens parent)
    {
        return new ThemeTokens
        {
            Colors = DeepMergeDict(Colors, parent.Colors),
            Font = DeepMergeDict(Font, parent.Font),
            Radius = DeepMergeDict(Radius, parent.Radius),
            Spacing = DeepMergeDict(Spacing, parent.Spacing),
            Layout = DeepMergeDict(Layout, parent.Layout)
        };
    }

    private static Dictionary<string, string>? MergeDict(
        Dictionary<string, string>? child,
        Dictionary<string, string>? parent)
    {
        if (parent is null) return child;
        if (child is null) return parent;

        var merged = new Dictionary<string, string>(parent);
        foreach (var kv in child)
        {
            merged[kv.Key] = kv.Value;
        }
        return merged;
    }

    private static Dictionary<string, string>? DeepMergeDict(
        Dictionary<string, string>? child,
        Dictionary<string, string>? parent)
    {
        if (parent is null) return child;
        if (child is null) return parent;

        var childTree = BuildTokenTree(child);
        var parentTree = BuildTokenTree(parent);
        var mergedTree = MergeTokenTrees(childTree, parentTree);
        return FlattenTokenTree(mergedTree);
    }

    private static Dictionary<string, object> BuildTokenTree(Dictionary<string, string> flat)
    {
        var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in flat)
        {
            var parts = key.Split('.');
            var current = root;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                if (!current.TryGetValue(part, out var next) || next is not Dictionary<string, object> nextDict)
                {
                    nextDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    current[part] = nextDict;
                }
                current = nextDict;
            }
            current[parts[^1]] = value;
        }
        return root;
    }

    private static Dictionary<string, object> MergeTokenTrees(
        Dictionary<string, object> child,
        Dictionary<string, object> parent)
    {
        var merged = new Dictionary<string, object>(parent, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, childValue) in child)
        {
            if (!merged.TryGetValue(key, out var parentValue))
            {
                merged[key] = childValue;
                continue;
            }

            if (childValue is Dictionary<string, object> childDict &&
                parentValue is Dictionary<string, object> parentDict)
            {
                merged[key] = MergeTokenTrees(childDict, parentDict);
            }
            else
            {
                merged[key] = childValue;
            }
        }
        return merged;
    }

    private static Dictionary<string, string> FlattenTokenTree(Dictionary<string, object> tree)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlattenTree(tree, "", result);
        return result;
    }

    private static void FlattenTree(Dictionary<string, object> node, string prefix, Dictionary<string, string> output)
    {
        foreach (var (key, value) in node)
        {
            var fullKey = string.IsNullOrEmpty(prefix) ? key : prefix + "." + key;
            if (value is string strValue)
            {
                output[fullKey] = strValue;
            }
            else if (value is Dictionary<string, object> childNode)
            {
                FlattenTree(childNode, fullKey, output);
            }
        }
    }
}
