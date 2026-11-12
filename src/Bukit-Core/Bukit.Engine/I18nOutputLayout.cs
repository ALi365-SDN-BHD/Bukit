using Bukit.Config;
using Bukit.Engine.Primitives;

namespace Bukit.Engine;

/// <summary>
/// Single source of truth for per-language base URLs, output directories, report directories
/// and reserved path prefixes. With <c>defaultLanguageInSubdir: false</c> the default language
/// owns the output root; every other language keeps its code directory.
/// </summary>
internal sealed class I18nOutputLayout
{
    private readonly SiteConfig _site;
    private readonly List<string> _languages;
    private readonly string _rootBaseUrl;
    private readonly bool _defaultLanguageInSubdir;
    private readonly string _defaultLanguage;

    private I18nOutputLayout(
        SiteConfig site,
        IReadOnlyList<string> languages,
        string rootBaseUrl,
        bool defaultLanguageInSubdir)
    {
        _site = site;
        _languages =
        [
            .. languages
                .Where(language => !string.IsNullOrWhiteSpace(language))
                .Select(language => language.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
        _rootBaseUrl = BuildPathUtils.NormalizeBaseUrl(rootBaseUrl);
        _defaultLanguageInSubdir = defaultLanguageInSubdir;
        _defaultLanguage = I18nLanguagePolicy.GetDefaultLanguage(site, _languages);
    }

    internal static I18nOutputLayout Create(
        SiteConfig site,
        IReadOnlyList<string> languages,
        string rootBaseUrl,
        bool? defaultLanguageInSubdir = null)
        => new(site, languages, rootBaseUrl, defaultLanguageInSubdir ?? site.DefaultLanguageInSubdir);

    internal bool IsMultilingual => _languages.Count > 0;

    internal bool IsRootLanguage(string language)
        => IsMultilingual && !_defaultLanguageInSubdir && IsDefaultLanguage(language);

    internal string BaseUrl(string language)
        => IsRootLanguage(language) || !IsMultilingual
            ? _rootBaseUrl
            : I18nLanguagePolicy.CombineBaseUrlWithLanguage(_rootBaseUrl, language);

    internal string RelativeOutputPrefix(string language)
        => IsRootLanguage(language) || !IsMultilingual ? string.Empty : language.Trim().Trim('/');

    internal string OutputDir(string root, string language)
    {
        var prefix = RelativeOutputPrefix(language);
        return string.IsNullOrEmpty(prefix) ? root : Path.Combine(root, prefix);
    }

    internal string ReportDir(string root, string language)
        => IsRootLanguage(language)
            ? Path.Combine(root, ".bukit", "variants", language)
            : Path.Combine(OutputDir(root, language), ".bukit");

    /// <summary>Report directory inside a variant output directory (which is the root for a root language).</summary>
    internal string VariantReportDir(string variantOutputDir, string language)
        => IsRootLanguage(language)
            ? Path.Combine(variantOutputDir, ".bukit", "variants", language)
            : Path.Combine(variantOutputDir, ".bukit");

    internal IReadOnlyList<string> ReservedPrefixes
        => IsMultilingual && !_defaultLanguageInSubdir
            ? _languages.Where(language => !IsDefaultLanguage(language)).ToList()
            : Array.Empty<string>();

    /// <summary>
    /// Audit read locations for a root language. Root aggregate files are written after every
    /// language, so reading them here would make the audit depend on the previous build.
    /// </summary>
    internal AuditOutputReadPaths? AuditReadPaths(string outputRoot, string language, string? staticDir = null)
    {
        if (!IsRootLanguage(language))
        {
            return null;
        }

        var sitemapMode = (_site.SitemapMode ?? "split").Trim().ToLowerInvariant();
        var feedMode = SiteModeResolver.ResolveFeedMode(_site);
        var searchMode = SiteModeResolver.ResolveSearchMode(_site);
        var sitemap = sitemapMode switch
        {
            "split" => Path.Combine(outputRoot, "sitemap.xml"),
            "index" => Path.Combine(outputRoot, language, "sitemap.xml"),
            _ => null
        };
        var feedRoot = feedMode == "split" ? outputRoot : null;
        var feedPath = (_site.Feed.Path ?? "feed").Trim('/');
        var robots = staticDir is not null && File.Exists(Path.Combine(staticDir, "robots.txt"))
            ? Path.Combine(outputRoot, "robots.txt")
            : null;
        return new AuditOutputReadPaths(
            Sitemap: sitemap,
            Search: searchMode == "merged" ? null : Path.Combine(outputRoot, "search.json"),
            Rss: feedRoot is null ? null : Path.Combine(feedRoot, "rss.xml"),
            Atom: feedRoot is null ? null : Path.Combine(feedRoot, feedPath, "atom.xml"),
            JsonFeed: feedRoot is null ? null : Path.Combine(feedRoot, feedPath, "feed.json"),
            AgentManifest: null,
            Robots: robots,
            Llms: null,
            LlmsFull: null);
    }

    internal static string ManifestLayout(SiteConfig site)
    {
        var languages = I18nOutputMerger.GetLanguages(site);
        return languages.Count > 0 && !site.DefaultLanguageInSubdir ? "root" : "subdir";
    }

    /// <summary>Whether <paramref name="language"/> builds into the output root.</summary>
    internal static bool IsRootLanguageOutput(AppConfig config, string language)
    {
        var languages = I18nOutputMerger.GetLanguages(config.Site);
        return languages.Count > 0 &&
            I18nOutputLayout.Create(config.Site, languages, config.Site.BaseUrl).IsRootLanguage(language);
    }

    /// <summary>Whether the variant built with <paramref name="variantConfig"/> owns the output root.</summary>
    internal static bool IsRootLanguageVariant(AppConfig variantConfig)
        => IsRootLanguageOutput(variantConfig, variantConfig.Site.Language);

    /// <summary>Whether the site's default language owns the output root (root-level decisions).</summary>
    internal static bool IsDefaultLanguageAtRoot(AppConfig config)
    {
        var languages = I18nOutputMerger.GetLanguages(config.Site);
        return languages.Count > 0 &&
            IsRootLanguageOutput(config, I18nLanguagePolicy.GetDefaultLanguage(config.Site, languages));
    }

    internal static string CombineRelative(string prefix, string path)
    {
        if (string.IsNullOrEmpty(prefix))
        {
            return path;
        }

        return Path.IsPathRooted(path)
            ? Path.Combine(path, prefix)
            : Path.Combine(prefix, path);
    }

    private bool IsDefaultLanguage(string language)
        => string.Equals(language.Trim(), _defaultLanguage.Trim(), StringComparison.OrdinalIgnoreCase);
}
