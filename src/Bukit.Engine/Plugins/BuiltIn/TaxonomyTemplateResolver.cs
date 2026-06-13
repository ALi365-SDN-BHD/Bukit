using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;
internal static class TaxonomyTemplateResolver
{
    internal static (string IndexTemplate, string TermTemplate) ResolveTemplates(
        TaxonomyConfig config,
        string layoutsDir,
        string kind,
        Func<string, string> resolveTemplateKind,
        TaxonomyKindConfig? kindConfig = null)
    {
        var kindBaseTemplate = FirstNonEmpty(kindConfig?.Template);
        var indexTemplate = FirstNonEmpty(kindConfig?.IndexTemplate, kindBaseTemplate)
            ?? resolveTemplateKind("taxonomy_index");
        var termTemplate = FirstNonEmpty(kindConfig?.TermTemplate, kindBaseTemplate)
            ?? resolveTemplateKind("taxonomy_term");

        indexTemplate = EnsureTemplateExists(indexTemplate, layoutsDir);
        termTemplate = EnsureTemplateExists(termTemplate, layoutsDir);

        return (indexTemplate, termTemplate);
    }

    internal static string EnsureTemplateExists(string template, string layoutsDir)
    {
        var fullPath = Path.Combine(layoutsDir, template.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new ConfigException($"Taxonomy template not found: {template}", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        return template;
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
