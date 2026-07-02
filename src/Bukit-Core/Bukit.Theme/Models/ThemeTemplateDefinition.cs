namespace Bukit.Theme;

public sealed class ThemeTemplateAccept
{
    public string? Type { get; set; }
    public string? Collection { get; set; }
    public string? Kind { get; set; }
}

public sealed class ThemeTemplateDefinition
{
    public string Template { get; set; } = "";
    public bool Required { get; set; }
    public string? Label { get; set; }
    public ThemeTemplateAccept? Accepts { get; set; }
    public List<string>? RequiredFields { get; set; }
}
