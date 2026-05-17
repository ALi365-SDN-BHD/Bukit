using System.Reflection;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands;

internal static class ThemeTemplateResource
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.OrdinalIgnoreCase);

    static ThemeTemplateResource()
    {
        LoadEmbeddedResources();
    }

    private static void LoadEmbeddedResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();
        const string prefix = "Bukit.Cli.Resources.StarterTheme.";
        foreach (var name in resourceNames)
        {
            if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var keyWithExt = name[prefix.Length..];
            var key = Path.GetFileNameWithoutExtension(keyWithExt);

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) continue;

            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            Templates[key] = reader.ReadToEnd();
        }
    }

    public static string Get(string name)
    {
        if (Templates.TryGetValue(name, out var content))
            return content;

        return FallbackTemplates.GetValueOrDefault(name, "");
    }

    public static string ProcessPlaceholders(string content, Dictionary<string, string> replacements)
    {
        if (replacements is null || replacements.Count == 0)
            return content;

        return Regex.Replace(content, @"\{\{--\s*bukit:(\S+?)\s*--\}\}",
            match =>
            {
                var key = match.Groups[1].Value;
                return replacements.TryGetValue(key, out var value) ? value : match.Value;
            });
    }

    public static string ApplyColorOverrides(string styleCss, string? primaryColor, string? accentColor)
    {
        if (!string.IsNullOrWhiteSpace(primaryColor))
            styleCss = styleCss.Replace("--primary: #0b5fff;", $"--primary: {primaryColor.Trim()};", StringComparison.Ordinal);

        if (!string.IsNullOrWhiteSpace(accentColor))
            styleCss = styleCss.Replace("--accent: #0f7b6c;", $"--accent: {accentColor.Trim()};", StringComparison.Ordinal);

        return styleCss;
    }

    private static readonly Dictionary<string, string> FallbackTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StyleCss"] = StarterThemeScaffold.StyleCss,
        ["BaseLayout"] = StarterThemeScaffold.BaseLayout,
        ["SeoPartial"] = StarterThemeScaffold.SeoPartial,
        ["AnalyticsPartial"] = StarterThemeScaffold.AnalyticsPartial,
        ["HeaderPartial"] = StarterThemeScaffold.HeaderPartial,
        ["FooterPartial"] = StarterThemeScaffold.FooterPartial,
        ["ListCardPartial"] = StarterThemeScaffold.ListCardPartial,
        ["PaginationNavPartial"] = StarterThemeScaffold.PaginationNavPartial,
        ["PageTemplate"] = StarterThemeScaffold.PageTemplate,
        ["PostTemplate"] = StarterThemeScaffold.PostTemplate,
        ["IndexTemplate"] = StarterThemeScaffold.IndexTemplate,
        ["ListTemplate"] = StarterThemeScaffold.ListTemplate,
        ["PaginationTemplate"] = StarterThemeScaffold.PaginationTemplate,
        ["TaxonomyIndexTemplate"] = StarterThemeScaffold.TaxonomyIndexTemplate,
        ["TaxonomyTermTemplate"] = StarterThemeScaffold.TaxonomyTermTemplate,
        ["SearchTemplate"] = StarterThemeScaffold.SearchTemplate,
        ["TemplateCapabilities"] = StarterThemeScaffold.TemplateCapabilities,
        ["ThemeYaml"] = StarterThemeScaffold.ThemeYaml,
    };
}
