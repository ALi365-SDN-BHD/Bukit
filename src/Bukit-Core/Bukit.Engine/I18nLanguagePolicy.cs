using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class I18nLanguagePolicy
{
    /// <summary>
    /// Returns the languages of a multilingual site. A site with fewer than two languages is a
    /// single-language site and gets an empty list; its language is <see cref="SiteConfig.Language"/>.
    /// </summary>
    internal static IReadOnlyList<string> GetLanguages(SiteConfig site)
    {
        var languages = SiteLanguageResolver.Clean(site.Languages);
        return languages.Count < 2 ? Array.Empty<string>() : languages;
    }

    /// <summary>
    /// Derives <see cref="SiteConfig.Language"/> from <c>site.languages</c> for configs built in
    /// code rather than by <see cref="ConfigLoader"/>, so every build reads the same site language.
    /// </summary>
    internal static SiteConfig NormalizeSite(SiteConfig site) => SiteLanguageResolver.WithDerivedLanguage(site);

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

        // Return the list's spelling, matching SiteLanguageResolver.
        var defaultLanguage = site.DefaultLanguage.Trim();
        return languages.FirstOrDefault(language => string.Equals(language, defaultLanguage, StringComparison.OrdinalIgnoreCase))
            ?? languages[0];
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

    internal static void WarnLayoutSettingsWithoutLanguages(SiteConfig site, ILogger logger)
    {
        if (GetLanguages(site).Count > 0 ||
            (site.DefaultLanguageInSubdir && site.DefaultLanguageRedirect && !site.DefaultLanguageLegacyRedirects))
        {
            return;
        }

        logger.Warn(
            $"event=i18n.config.default_language_layout_ignored defaultLanguageInSubdir={site.DefaultLanguageInSubdir.ToString().ToLowerInvariant()} defaultLanguageRedirect={site.DefaultLanguageRedirect.ToString().ToLowerInvariant()} defaultLanguageLegacyRedirects={site.DefaultLanguageLegacyRedirects.ToString().ToLowerInvariant()}");
    }

    /// <summary>Legacy redirects only exist once the default language of a multilingual site has moved to the root.</summary>
    internal static void WarnIgnoredLegacyRedirects(SiteConfig site, ILogger logger)
    {
        if (GetLanguages(site).Count > 0 && site.DefaultLanguageInSubdir && site.DefaultLanguageLegacyRedirects)
        {
            logger.Warn(
                "event=i18n.config.legacy_redirects_ignored defaultLanguageInSubdir=true defaultLanguageLegacyRedirects=true");
        }
    }

    internal static void WarnDocumentLanguageIssues(
        IReadOnlyList<ContentDocument> documents,
        SiteConfig site,
        ILogger logger)
    {
        var languages = GetLanguages(site);
        if (languages.Count == 0 && !string.IsNullOrWhiteSpace(site.DefaultLanguage))
        {
            logger.Warn(
                $"event=i18n.config.default_language_ignored defaultLanguage={site.DefaultLanguage.Trim()} language={site.Language}");
        }

        var configured = languages.Count > 0
            ? languages
            : [site.Language.Trim()];
        var defaultLanguage = GetDefaultLanguage(site, languages);
        var translations = new Dictionary<string, (string Language, string Key, List<string> Ids)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var document in documents.Where(document => !ContentFieldReader.IsDataItem(document)))
        {
            var language = document.Record.Presentation.Language;
            if (string.IsNullOrWhiteSpace(language) ||
                string.Equals(language, "und", StringComparison.OrdinalIgnoreCase))
            {
                language = ContentFieldReader.GetText(document, "language");
            }

            language = string.IsNullOrWhiteSpace(language) ? defaultLanguage : language.Trim();
            if (!configured.Contains(language, StringComparer.OrdinalIgnoreCase))
            {
                logger.Warn(
                    $"event=i18n.document.language_unknown id={document.Id} language={language} configured={string.Join(',', configured)}");
                continue;
            }

            if (!ContentFieldReader.TryGetI18nKey(document.CustomFields, out var key))
            {
                continue;
            }

            var groupKey = language + "\n" + key;
            if (!translations.TryGetValue(groupKey, out var group))
            {
                group = (language, key, []);
                translations[groupKey] = group;
            }

            group.Ids.Add(document.Id);
        }

        foreach (var (_, group) in translations.Where(entry => entry.Value.Ids.Count > 1))
        {
            logger.Warn(
                $"event=i18n.document.i18n_key_duplicate language={group.Language} key={group.Key} ids={string.Join(',', group.Ids)}");
        }
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
