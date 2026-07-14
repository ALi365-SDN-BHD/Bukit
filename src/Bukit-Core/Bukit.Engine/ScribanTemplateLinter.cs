using Scriban;

namespace Bukit.Engine;

public sealed record TemplateVariableWarning(
    string Variable,
    string Template,
    string Message);

public static class ScribanTemplateLinter
{
    public static List<TemplateVariableWarning> LintDirectory(string layoutsDir, string templateName)
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
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Scriban.Syntax.ScriptRuntimeException)
            {
                warnings.Add(new TemplateVariableWarning(
                    string.Empty,
                    relativePath,
                    $"Unable to lint template '{relativePath}': {ex.Message}"));
            }
        }

        return warnings;
    }

    public static List<TemplateVariableWarning> LintTemplate(Template template, string templateRelativePath)
    {
        var warnings = new List<TemplateVariableWarning>();
        var analysis = ScribanSymbolAnalyzer.Analyze(template);

        foreach (var reference in analysis.References)
        {
            var validation = ScribanTemplateContextContract.Validate(reference);
            if (validation.Status != ScribanPathStatus.Invalid)
            {
                continue;
            }

            if (validation.IsCurrentContext)
            {
                warnings.Add(new TemplateVariableWarning(
                    reference.Path,
                    templateRelativePath,
                    $"Unknown field: '{{{{ {reference.Path} }}}}' - '{validation.FieldPath}' is not available on the current template context"));
                continue;
            }

            if (validation.IsPageItem)
            {
                warnings.Add(new TemplateVariableWarning(
                    reference.Path,
                    templateRelativePath,
                    $"Unknown field: '{{{{ {reference.Path} }}}}' - '{validation.FieldPath}' is not a known page item field"));
                continue;
            }

            warnings.Add(new TemplateVariableWarning(
                reference.Path,
                templateRelativePath,
                $"Unknown field: '{{{{ {reference.Path} }}}}' - '{validation.FieldPath}' is not a known field on the '{validation.Root}' model"));
        }

        return warnings;
    }

    public static string? GetRootContext(string variableName)
    {
        if (string.IsNullOrWhiteSpace(variableName)) return null;

        var firstDot = variableName.IndexOf('.');
        var root = firstDot < 0 ? variableName : variableName[..firstDot];

        if (ScribanModelKnownFields.KnownRootContexts.Contains(root))
        {
            return root;
        }

        if (root is "p" or "item")
        {
            return root;
        }

        if (root is "section" or "items" or "pages" or "content" or "for")
        {
            return root;
        }

        return null;
    }

    public static string? GetFieldPath(string variableName, string? root)
    {
        if (root is null) return null;

        var firstDot = variableName.IndexOf('.');
        if (firstDot < 0) return null;

        return variableName[(firstDot + 1)..];
    }

}
