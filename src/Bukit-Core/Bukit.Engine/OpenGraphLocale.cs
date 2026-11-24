namespace Bukit.Engine;

/// <summary>
/// Maps a site or hreflang language code to an Open Graph locale (<c>ll_CC</c>). Open Graph needs a
/// region, so a code with one keeps it (<c>zh-CN</c> → <c>zh_CN</c>), a common bare code gets its usual
/// region (<c>en</c> → <c>en_US</c>), and any other code maps to an empty string, which omits the tag.
/// </summary>
internal static class OpenGraphLocale
{
    // Keyed by primary subtag, or primary-script for Chinese script subtags.
    private static readonly Dictionary<string, string> DefaultRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ar"] = "ar_AR",
        ["de"] = "de_DE",
        ["en"] = "en_US",
        ["es"] = "es_ES",
        ["fr"] = "fr_FR",
        ["hi"] = "hi_IN",
        ["id"] = "id_ID",
        ["it"] = "it_IT",
        ["ja"] = "ja_JP",
        ["ko"] = "ko_KR",
        ["ms"] = "ms_MY",
        ["nl"] = "nl_NL",
        ["pl"] = "pl_PL",
        ["pt"] = "pt_BR",
        ["ru"] = "ru_RU",
        ["ta"] = "ta_IN",
        ["th"] = "th_TH",
        ["tr"] = "tr_TR",
        ["vi"] = "vi_VN",
        ["zh"] = "zh_CN",
        ["zh-hans"] = "zh_CN",
        ["zh-hant"] = "zh_TW"
    };

    internal static string From(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return string.Empty;
        }

        var subtags = language.Trim().Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries);
        if (subtags.Length == 0 || subtags[0].Length is < 2 or > 3 || !subtags[0].All(char.IsAsciiLetter))
        {
            return string.Empty;
        }

        var primary = subtags[0].ToLowerInvariant();
        string? script = null;
        foreach (var subtag in subtags.Skip(1))
        {
            // A singleton starts an extension or private-use part (de-u-co-phonebk, en-x-custom).
            if (subtag.Length == 1)
            {
                break;
            }

            if (subtag.Length == 2 && subtag.All(char.IsAsciiLetter))
            {
                return $"{primary}_{subtag.ToUpperInvariant()}";
            }

            if (subtag.Length == 4 && subtag.All(char.IsAsciiLetter))
            {
                script ??= subtag;
            }
        }

        return (script is not null && DefaultRegions.TryGetValue($"{primary}-{script}", out var locale)) ||
               DefaultRegions.TryGetValue(primary, out locale)
            ? locale
            : string.Empty;
    }

    /// <summary>
    /// Locales of the page's other languages, from its hreflang alternates, without <c>x-default</c>,
    /// the page's own locale, codes with no Open Graph locale, and duplicates. A page without its own
    /// locale gets none, since Open Graph would then read the alternates against a default of en_US.
    /// </summary>
    internal static IEnumerable<string> Alternates(string? pageLocale, IEnumerable<Bukit.Rendering.SeoAlternateModel> alternates)
        => string.IsNullOrWhiteSpace(pageLocale)
            ? []
            : alternates
                .Where(alternate => !string.Equals(alternate.Hreflang, "x-default", StringComparison.OrdinalIgnoreCase))
                .Select(alternate => From(alternate.Hreflang))
                .Where(locale => locale.Length > 0 && !string.Equals(locale, pageLocale, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase);
}
