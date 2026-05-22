namespace Bukit.Theme;

public sealed class ThemeComponentDefinition
{
    public string Template { get; set; } = "";
    public Dictionary<string, string>? Props { get; set; }
}
