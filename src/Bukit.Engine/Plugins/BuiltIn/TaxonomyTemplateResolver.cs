using Bukit.Config;

namespace Bukit.Engine.Plugins.BuiltIn;

internal static class TaxonomyTemplateResolver
{
    internal static (string IndexTemplate, string TermTemplate) ResolveTemplates(TaxonomyConfig config, string layoutsDir, string kind, TaxonomyKindConfig? kindConfig = null)
    {
        var legacyKindConfig = kind.Equals("tags", StringComparison.OrdinalIgnoreCase)
            ? config.Templates.Tags
            : (kind.Equals("categories", StringComparison.OrdinalIgnoreCase) ? config.Templates.Categories : new TaxonomyKindTemplateConfig());
        var conventionalIndexTemplate = TemplateCapabilitiesResolver.SupportsTaxonomy(TemplateCapabilitiesResolver.TaxonomyIndexTemplatePath, layoutsDir)
            ? TemplateCapabilitiesResolver.TaxonomyIndexTemplatePath
            : null;
        var conventionalTermTemplate = TemplateCapabilitiesResolver.SupportsTaxonomy(TemplateCapabilitiesResolver.TaxonomyTermTemplatePath, layoutsDir)
            ? TemplateCapabilitiesResolver.TaxonomyTermTemplatePath
            : null;

        var baseTemplate = string.IsNullOrWhiteSpace(config.Template) ? "pages/page.html" : config.Template;
        var kindBaseTemplate = FirstNonEmpty(kindConfig?.Template, legacyKindConfig.Template, baseTemplate) ?? "pages/page.html";
        var indexTemplate = FirstNonEmpty(kindConfig?.IndexTemplate, legacyKindConfig.IndexTemplate, config.IndexTemplate, conventionalIndexTemplate, kindBaseTemplate)
            ?? kindBaseTemplate;
        var termTemplate = FirstNonEmpty(kindConfig?.TermTemplate, legacyKindConfig.TermTemplate, config.TermTemplate, conventionalTermTemplate, kindBaseTemplate)
            ?? kindBaseTemplate;

        indexTemplate = EnsureTemplateExists(indexTemplate, layoutsDir, "pages/page.html");
        termTemplate = EnsureTemplateExists(termTemplate, layoutsDir, "pages/page.html");

        return (indexTemplate, termTemplate);
    }

    internal static string EnsureTemplateExists(string template, string layoutsDir, string fallback)
    {
        var fullPath = Path.Combine(layoutsDir, template.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(fullPath) ? template : fallback;
    }

    internal static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var c in candidates)
        {
            if (!string.IsNullOrWhiteSpace(c))
            {
                return c!.Trim();
            }
        }

        return null;
    }
}
