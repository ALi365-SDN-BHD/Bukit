namespace Bukit.Theme;

public sealed class ThemeSectionDefinition
{
    public string Template { get; set; } = "";
    public string? Schema { get; set; }
    public string? Preview { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, ThemeVariantDefinition>? Variants { get; set; }
    public ThemeDataBindingDefinition? Data { get; set; }
}
