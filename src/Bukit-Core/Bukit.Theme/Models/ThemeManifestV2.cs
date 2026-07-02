namespace Bukit.Theme;

public sealed class ThemeCapabilities
{
    public bool I18n { get; set; }
    public bool Seo { get; set; }
    public bool Geo { get; set; }
    public bool DarkMode { get; set; }
    public bool Search { get; set; }
    public bool Taxonomy { get; set; }
}

public sealed class ThemeAssetsConfig
{
    public List<string>? Css { get; set; }
    public List<string>? Js { get; set; }
}

public sealed class ThemeManifestV2
{
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Version { get; set; }
    public string? Engine { get; set; }
    public string? MinEngineVersion { get; set; }
    public string? Description { get; set; }
    public string? Extends { get; set; }
    public ThemeCapabilities Capabilities { get; set; } = new();
    public Dictionary<string, string>? Layouts { get; set; }
    public Dictionary<string, ThemeTemplateDefinition>? Templates { get; set; }
    public Dictionary<string, ThemePageTemplateDefinition>? PageTemplates { get; set; }
    public Dictionary<string, ThemeSectionDefinition>? Sections { get; set; }
    public Dictionary<string, ThemeComponentDefinition>? Components { get; set; }
    public ThemeAssetsConfig Assets { get; set; } = new();
    public string? Tokens { get; set; }
}
