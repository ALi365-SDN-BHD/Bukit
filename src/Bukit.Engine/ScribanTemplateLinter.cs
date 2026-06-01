using Scriban;

namespace Bukit.Engine;

internal sealed record TemplateVariableWarning(
    string Variable,
    string Template,
    string Message);

internal static class ScribanTemplateLinter
{
    internal static List<TemplateVariableWarning> LintDirectory(string layoutsDir, string templateName)
    {
        var warnings = new List<TemplateVariableWarning>();
        var allHtmlFiles = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories);

        foreach (var filePath in allHtmlFiles)
        {
            var relativePath = Path.GetRelativePath(layoutsDir, filePath);
            if (!string.IsNullOrWhiteSpace(templateName) &&
                !string.Equals(relativePath, templateName, StringComparison.OrdinalIgnoreCase) &&
                !relativePath.EndsWith("/" + templateName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var text = File.ReadAllText(filePath);
                var template = Template.Parse(text, filePath);
                if (template.HasErrors) continue;

                var fileWarnings = LintTemplate(template, relativePath);
                warnings.AddRange(fileWarnings);
            }
            catch
            {
            }
        }

        return warnings;
    }

    internal static List<TemplateVariableWarning> LintTemplate(Template template, string templateRelativePath)
    {
        var warnings = new List<TemplateVariableWarning>();
        var variables = ScribanVariableCollector.Collect(template);

        foreach (var variable in variables)
        {
            var root = GetRootContext(variable);
            var fieldPath = GetFieldPath(variable, root);

            if (root is null)
            {
                if (!IsKnownBuiltin(variable))
                {
                    warnings.Add(new TemplateVariableWarning(
                        variable, templateRelativePath,
                        $"Unknown variable: '{{{{ {variable} }}}}' - variable has no known root context (page/site/list)"));
                }
                continue;
            }

            if (fieldPath is null) continue;

            if (!ScribanModelKnownFields.IsKnownField(root, fieldPath))
            {
                warnings.Add(new TemplateVariableWarning(
                    variable, templateRelativePath,
                    $"Unknown field: '{{{{ {variable} }}}}' - '{fieldPath}' is not a known field on the '{root}' model"));
            }
        }

        return warnings;
    }

    internal static string? GetRootContext(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName)) return null;

        var firstDot = variableName.IndexOf('.');
        var root = firstDot < 0 ? variableName : variableName[..firstDot];

        if (ScribanModelKnownFields.KnownRootContexts.Contains(root))
        {
            return root;
        }

        if (root is "p" or "item" or "post" or "page_item")
        {
            return root;
        }

        if (root is "section" or "items" or "pages" or "content" or "for")
        {
            return root;
        }

        return null;
    }

    internal static string? GetFieldPath(string variableName, string? root)
    {
        if (root is null) return null;

        var firstDot = variableName.IndexOf('.');
        if (firstDot < 0) return null;

        return variableName[(firstDot + 1)..];
    }

    private static bool IsKnownBuiltin(string variableName)
    {
        return variableName is "now" or "today" or "include" or "content"
            or "index" or "odd" or "even" or "for" or "if"
            or "len" or "count" or "size" or "base_url";
    }
}
