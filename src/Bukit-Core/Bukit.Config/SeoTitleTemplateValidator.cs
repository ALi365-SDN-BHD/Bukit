using Bukit.Shared;

namespace Bukit.Config;

internal static class SeoTitleTemplateValidator
{
    private static readonly HashSet<string> SupportedPlaceholders = new(StringComparer.OrdinalIgnoreCase)
    {
        "pageTitle",
        "siteTitle",
        "separator"
    };

    internal static void Validate(SeoConfig seo)
    {
        var homePlaceholders = ValidateTemplate(seo.HomeTitleTemplate, "site.seo.homeTitleTemplate");
        if (!homePlaceholders.Contains("pageTitle") && !homePlaceholders.Contains("siteTitle"))
        {
            throw new ConfigException(
                "site.seo.homeTitleTemplate must contain {pageTitle} or {siteTitle}.",
                DiagnosticCode.ConfigInvalidValue);
        }

        var pagePlaceholders = ValidateTemplate(seo.PageTitleTemplate, "site.seo.pageTitleTemplate");
        if (!pagePlaceholders.Contains("pageTitle"))
        {
            throw new ConfigException(
                "site.seo.pageTitleTemplate must contain {pageTitle}.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static HashSet<string> ValidateTemplate(string? template, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ConfigException(
                $"{fieldName} must be a non-empty string.",
                DiagnosticCode.ConfigInvalidValue);
        }

        var placeholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < template.Length; index++)
        {
            if (template[index] == '}')
            {
                throw new ConfigException(
                    $"{fieldName} contains an unopened placeholder.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (template[index] != '{')
            {
                continue;
            }

            var end = template.IndexOf('}', index + 1);
            if (end < 0)
            {
                throw new ConfigException(
                    $"{fieldName} contains an unclosed placeholder.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            var placeholder = template[(index + 1)..end];
            if (!SupportedPlaceholders.Contains(placeholder))
            {
                throw new ConfigException(
                    $"{fieldName} contains unsupported placeholder {{{placeholder}}}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            placeholders.Add(placeholder);
            index = end;
        }

        return placeholders;
    }
}
