using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class I18nLanguagePolicy
{
    internal static IReadOnlyList<string> GetLanguages(SiteConfig site)
    {
        if (site.Languages is not { Count: > 0 } langs)
        {
            return Array.Empty<string>();
        }

        var cleaned = langs.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
        if (cleaned.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in cleaned)
        {
            if (seen.Add(language))
            {
                result.Add(language);
            }
        }

        return result;
    }

    internal static string GetDefaultLanguage(SiteConfig site, IReadOnlyList<string> languages)
    {
        if (languages.Count == 0)
        {
            return site.Language;
        }

        if (string.IsNullOrWhiteSpace(site.DefaultLanguage))
        {
            return languages[0];
        }

        var defaultLanguage = site.DefaultLanguage.Trim();
        return languages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase)
            ? defaultLanguage
            : languages[0];
    }

    internal static string CombineBaseUrlWithLanguage(string baseUrl, string language)
    {
        var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl);
        var normalizedLanguage = language.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalizedLanguage))
        {
            return normalizedBaseUrl;
        }

        if (normalizedBaseUrl == "/")
        {
            return "/" + normalizedLanguage;
        }

        return normalizedBaseUrl.TrimEnd('/') + "/" + normalizedLanguage;
    }

    internal static IReadOnlyList<ContentDocument> FilterDocumentsByLanguage(
        IReadOnlyList<ContentDocument> documents,
        string language,
        string defaultLanguage)
    {
        return documents.Where(document =>
        {
            if (ContentFieldReader.IsDataItem(document))
            {
                var locale = ContentFieldReader.GetText(document.CustomFields, "locale");
                return string.IsNullOrWhiteSpace(locale) || string.Equals(locale, language, StringComparison.OrdinalIgnoreCase);
            }

            var documentLanguage = document.Record.Presentation.Language;
            if (!string.IsNullOrWhiteSpace(documentLanguage) &&
                !string.Equals(documentLanguage, "und", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(documentLanguage, language, StringComparison.OrdinalIgnoreCase);
            }

            documentLanguage = ContentFieldReader.GetText(document, "language");
            if (!string.IsNullOrWhiteSpace(documentLanguage))
            {
                return string.Equals(documentLanguage, language, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }
}
