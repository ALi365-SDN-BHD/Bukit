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
}
