namespace Bukit.Theme;

public sealed class ThemeDataBindingDefinition
{
    public string? Source { get; set; }
    public string? Mode { get; set; }
    public int? Limit { get; set; }
    public string? Sort { get; set; }
    public Dictionary<string, object?>? Filters { get; set; }
}
