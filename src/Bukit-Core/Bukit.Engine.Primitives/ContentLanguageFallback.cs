using Bukit.Config;

namespace Bukit.Engine.Primitives;

/// <summary>
/// Language for a page that declares none (<c>und</c>) in its per-page public projections and in the
/// publish audit that checks them. A single-language site owns every page, so such a page uses the site
/// language, the same as its search records. A multilingual site keeps
/// <c>und</c>, because the merged audit compares all languages' pages against one configuration.
/// </summary>
internal static class ContentLanguageFallback
{
    internal const string Undetermined = "und";

    /// <summary>The site language for a single-language site; <see langword="null"/> for a multilingual one.</summary>
    internal static string? ForSite(SiteConfig site)
    {
        return SiteLanguageResolver.Clean(site.Languages).Count < 2 && !string.IsNullOrWhiteSpace(site.Language)
            ? site.Language.Trim()
            : null;
    }

    /// <summary>Returns <paramref name="fallback"/> when <paramref name="language"/> is blank or <c>und</c>.</summary>
    internal static string? Resolve(string? language, string? fallback)
        => !string.IsNullOrWhiteSpace(fallback) &&
           (string.IsNullOrWhiteSpace(language) || string.Equals(language, Undetermined, StringComparison.OrdinalIgnoreCase))
            ? fallback
            : language;
}
