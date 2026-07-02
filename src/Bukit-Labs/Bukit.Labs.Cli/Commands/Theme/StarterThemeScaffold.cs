using System.Text;

namespace Bukit.Labs.Cli.Commands;

internal static class StarterThemeScaffold
{
    public static void WriteTo(string rootDir)
        => WriteTo(rootDir, "starter", primaryColor: null, accentColor: null);

    public static void WriteTo(string rootDir, string themeName, string? primaryColor, string? accentColor)
    {
        var styleCss = ThemeTemplateResource.Get("StyleCss");
        styleCss = ThemeTemplateResource.ApplyColorOverrides(styleCss, primaryColor, accentColor);

        var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["primary-color"] = primaryColor ?? "#0b5fff",
            ["accent-color"] = accentColor ?? "#0f7b6c",
            ["brand"] = themeName
        };

        styleCss = ThemeTemplateResource.ProcessPlaceholders(styleCss, placeholders);

        WriteFile(rootDir, Path.Combine("themes", themeName, "assets", "style.css"), styleCss);
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "layouts", "base.html"),
            ThemeTemplateResource.ProcessPlaceholders(ThemeTemplateResource.Get("BaseLayout"), placeholders));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "partials", "seo.html"), ThemeTemplateResource.Get("SeoPartial"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "partials", "analytics.html"), ThemeTemplateResource.Get("AnalyticsPartial"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "partials", "header.html"),
            ThemeTemplateResource.ProcessPlaceholders(ThemeTemplateResource.Get("HeaderPartial"), placeholders));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "partials", "footer.html"),
            ThemeTemplateResource.ProcessPlaceholders(ThemeTemplateResource.Get("FooterPartial"), placeholders));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "partials", "list-card.html"), ThemeTemplateResource.Get("ListCardPartial"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "partials", "pagination-nav.html"), ThemeTemplateResource.Get("PaginationNavPartial"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "page.html"), ThemeTemplateResource.Get("PageTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "post.html"), ThemeTemplateResource.Get("PostTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "index.html"), ThemeTemplateResource.Get("IndexTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "list.html"), ThemeTemplateResource.Get("ListTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "pagination.html"), ThemeTemplateResource.Get("PaginationTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "taxonomy-index.html"), ThemeTemplateResource.Get("TaxonomyIndexTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "taxonomy-term.html"), ThemeTemplateResource.Get("TaxonomyTermTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "pages", "search.html"), ThemeTemplateResource.Get("SearchTemplate"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "layouts", "bukit.templates.yaml"), ThemeTemplateResource.Get("TemplateCapabilities"));
        WriteFile(rootDir, Path.Combine("themes", themeName, "theme.yaml"), ThemeTemplateResource.Get("ThemeYaml"));
    }

    public static string ApplyColorOverrides(string styleCss, string? primaryColor, string? accentColor)
    {
        if (!string.IsNullOrWhiteSpace(primaryColor))
        {
            styleCss = styleCss.Replace("--primary: #0b5fff;", $"--primary: {primaryColor.Trim()};", StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(accentColor))
        {
            styleCss = styleCss.Replace("--accent: #0f7b6c;", $"--accent: {accentColor.Trim()};", StringComparison.Ordinal);
        }

        return styleCss;
    }

    private static void WriteFile(string rootDir, string relativePath, string content)
    {
        var path = Path.Combine(rootDir, relativePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
