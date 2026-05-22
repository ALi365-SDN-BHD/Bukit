namespace Bukit.Theme;

public sealed class ThemePageTemplateAccept
{
    public string? Type { get; set; }
    public string? Collection { get; set; }
}

public sealed class ThemePageTemplateDefinition
{
    public string Template { get; set; } = "";
    public string? Label { get; set; }
    public ThemePageTemplateAccept? Accepts { get; set; }
    public List<string>? RequiredFields { get; set; }
}
