namespace Bukit.Labs.Cli.Commands;

/// <summary>
/// Controls which templates are written during theme generation.
/// </summary>
public enum TemplateScope
{
    /// <summary>Write all templates (page, post, list, pagination, taxonomy, search, etc.).</summary>
    Full,

    /// <summary>Write only base layout, partials, assets, index, and theme.yaml. No content-type templates.</summary>
    Bare,

    /// <summary>Write no theme files at all.</summary>
    None
}

public static class TemplateScopeExtensions
{
    /// <summary>Whether page-type templates (page.html, post.html, list.html, etc.) should be written.</summary>
    public static bool ShouldWritePageTemplates(this TemplateScope scope) => scope == TemplateScope.Full;

    public static TemplateScope Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TemplateScope.Full;

        return value.Trim().ToLowerInvariant() switch
        {
            "bare" => TemplateScope.Bare,
            "none" => TemplateScope.None,
            _ => TemplateScope.Full
        };
    }

    /// <summary>Whether any theme files should be written at all.</summary>
    public static bool ShouldWriteAnyTheme(this TemplateScope scope) => scope != TemplateScope.None;
}
