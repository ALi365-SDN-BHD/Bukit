namespace Bukit.Config;

/// <summary>
/// Derives the site language from <c>site.languages</c>, so a site has one language source.
/// </summary>
internal static class SiteLanguageResolver
{
    /// <summary>
    /// Returns the configured languages with blank entries removed, values trimmed and
    /// duplicates dropped (first occurrence wins, compared case-insensitively).
    /// </summary>
    internal static IReadOnlyList<string> Clean(IReadOnlyList<string>? languages)
    {
        if (languages is not { Count: > 0 })
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in languages)
        {
            if (!string.IsNullOrWhiteSpace(language) && seen.Add(language.Trim()))
            {
                result.Add(language.Trim());
            }
        }

        return result;
    }

    /// <summary>
    /// Sets <see cref="SiteConfig.Language"/> to the default language when <c>site.languages</c>
    /// is set: <c>defaultLanguage</c> when it is one of the languages, otherwise the first one.
    /// With one language this is that language. Without <c>site.languages</c> the site is returned as is.
    /// </summary>
    internal static SiteConfig WithDerivedLanguage(SiteConfig site)
    {
        var languages = Clean(site.Languages);
        if (languages.Count == 0)
        {
            return site;
        }

        var defaultLanguage = site.DefaultLanguage?.Trim();
        var language = !string.IsNullOrEmpty(defaultLanguage) &&
                       languages.Contains(defaultLanguage, StringComparer.OrdinalIgnoreCase)
            ? languages.First(item => string.Equals(item, defaultLanguage, StringComparison.OrdinalIgnoreCase))
            : languages[0];
        return string.Equals(site.Language, language, StringComparison.Ordinal) ? site : site with { Language = language };
    }
}
