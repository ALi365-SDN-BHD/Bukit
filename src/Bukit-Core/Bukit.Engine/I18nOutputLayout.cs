using System.Runtime.CompilerServices;
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
    private static readonly ConditionalWeakTable<SiteConfig, I18nOutputLayout> PlacementCache = [];

    private readonly SiteConfig _site;
    private readonly List<string> _languages;
    private readonly string _rootBaseUrl;
    private readonly bool _defaultLanguageInSubdir;
    private readonly string _defaultLanguage;
    private bool _placementOnly;

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

    /// <summary>
    /// Cached layout for placement decisions (root language, prefixes, report and sitemap paths).
    /// It is keyed by the immutable <see cref="SiteConfig"/> instance and built from
    /// <c>site.BaseUrl</c>, which for a variant config is that variant's base URL, so callers that
    /// need root-relative base URLs must use <see cref="Create"/> with the root base URL instead.
    /// </summary>
    internal static I18nOutputLayout For(SiteConfig site)
        => PlacementCache.GetValue(site, key =>
        {
            var layout = Create(key, I18nOutputMerger.GetLanguages(key), key.BaseUrl);
            layout._placementOnly = true;
            return layout;
        });

    internal bool IsMultilingual => _languages.Count > 0;

    /// <summary>Whether the default language owns the output root.</summary>
    internal bool IsRootLayout => IsMultilingual && !_defaultLanguageInSubdir;

    internal bool IsRootLanguage(string language)
        => IsRootLayout && IsDefaultLanguage(language);

    internal string BaseUrl(string language)
        => _placementOnly
            ? throw new InvalidOperationException("A placement layout from For() has no root base URL; use Create with the root base URL.")
            : IsRootLanguage(language) || !IsMultilingual
            ? _rootBaseUrl
            : I18nLanguagePolicy.CombineBaseUrlWithLanguage(_rootBaseUrl, language);

    internal string RelativeOutputPrefix(string language)
        => IsRootLanguage(language) || !IsMultilingual ? string.Empty : language.Trim().Trim('/');

    internal string OutputDir(string root, string language)
    {
        var prefix = RelativeOutputPrefix(language);
        return string.IsNullOrEmpty(prefix) ? root : Path.Combine(root, prefix);
    }

    /// <summary>
    /// Where <paramref name="language"/> writes its own sitemap, relative to that language's output
    /// directory: <c>&lt;lang&gt;/sitemap.xml</c> for a root language in index mode, otherwise
    /// <c>sitemap.xml</c>. Whether a sitemap is written at all is decided by the caller.
    /// </summary>
    internal string SitemapRelativePath(string language)
        => IsRootLanguage(language) &&
           string.Equals((_site.SitemapMode ?? "split").Trim(), "index", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(language.Trim(), "sitemap.xml")
            : "sitemap.xml";

    /// <summary>
    /// Where <paramref name="language"/> writes its own search index, relative to that language's
    /// output directory: <c>&lt;lang&gt;/search.json</c> for a root language in merged mode, whose
    /// <c>search.json</c> is the root merged index, otherwise <c>search.json</c>.
    /// </summary>
    internal string SearchRelativePath(string language)
        => IsRootLanguage(language) && SiteModeResolver.ResolveSearchMode(_site) == "merged"
            ? Path.Combine(language.Trim(), "search.json")
            : "search.json";

    /// <summary>
    /// Absolute URL of a language's own sitemap, as robots.txt and the sitemap index advertise it.
    /// The root language's sitemap sits under the root base URL; any other language's under its
    /// variant base URL.
    /// </summary>
    internal string SitemapUrl(string siteUrl, string language, string variantBaseUrl)
        => _placementOnly
            ? throw new InvalidOperationException("A placement layout from For() has no root base URL; use Create with the root base URL.")
            : SitemapGenerator.BuildAbsoluteUrl(
                siteUrl,
                IsRootLanguage(language) ? _rootBaseUrl : variantBaseUrl,
                "/" + BuildPathUtils.NormalizeRelPath(SitemapRelativePath(language)));

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
        => IsRootLayout
            ? _languages.Where(language => !IsDefaultLanguage(language)).ToList()
            : Array.Empty<string>();

    /// <summary>
    /// Audit read locations for a root language. Root aggregate files are written after every
    /// language, so reading them here would make the audit depend on the previous build.
    /// </summary>
    internal AuditOutputReadPaths? AuditReadPaths(
        string outputRoot,
        string language,
        IReadOnlyList<string?> staticDirs,
        DirectoryCopyOptions staticOptions)
    {
        if (!IsRootLanguage(language))
        {
            return null;
        }

        var sitemapMode = (_site.SitemapMode ?? "split").Trim().ToLowerInvariant();
        var feedMode = SiteModeResolver.ResolveFeedMode(_site);
        var sitemap = sitemapMode is "split" or "index"
            ? Path.Combine(outputRoot, SitemapRelativePath(language))
            : null;
        var feedRoot = feedMode == "split" ? outputRoot : null;
        var feedPath = (_site.Feed.Path ?? "feed").Trim('/');
        var robots = staticDirs.Any(directory => directory is not null &&
                DirectoryCopy.StaticSourceExists(directory, "robots.txt", staticOptions))
            ? Path.Combine(outputRoot, "robots.txt")
            : null;
        return new AuditOutputReadPaths(
            Sitemap: sitemap,
            Search: Path.Combine(outputRoot, SearchRelativePath(language)),
            Rss: feedRoot is null ? null : Path.Combine(feedRoot, "rss.xml"),
            Atom: feedRoot is null ? null : FirstExisting(Path.Combine(feedRoot, feedPath, "atom.xml"), Path.Combine(feedRoot, "atom.xml")),
            JsonFeed: feedRoot is null ? null : FirstExisting(Path.Combine(feedRoot, feedPath, "feed.json"), Path.Combine(feedRoot, "feed.json")),
            AgentManifest: null,
            Robots: robots,
            Llms: null,
            LlmsFull: null);
    }

    // Same fallback as AuditOutputReadPaths.ForOutputDirectory: feeds may sit at the output root.
    private static string FirstExisting(string preferred, string fallback)
        => File.Exists(preferred) ? preferred : fallback;

    internal static string ManifestLayout(SiteConfig site)
        => For(site).IsRootLayout ? "root" : "subdir";

    /// <summary>
    /// Static copy rules from <c>build</c> (dotfiles and symlinks), for checks that must agree
    /// with what the asset copy step will publish.
    /// </summary>
    internal static DirectoryCopyOptions StaticCopyOptions(BuildConfig build) => new()
    {
        FollowSymlinks = build.FollowSymlinks,
        IgnoreDotPrefixedFiles = !build.PublishDotFiles
    };

    /// <summary>Whether the variant built with <paramref name="variantConfig"/> owns the output root.</summary>
    internal static bool IsRootLanguageVariant(AppConfig variantConfig)
        => For(variantConfig.Site).IsRootLanguage(variantConfig.Site.Language);

    /// <summary>Whether the site's default language owns the output root (root-level decisions).</summary>
    internal static bool IsDefaultLanguageAtRoot(AppConfig config)
        => For(config.Site).IsRootLayout;

    /// <summary>Sitemap path of the variant built with <paramref name="variantConfig"/>, relative to its output directory.</summary>
    internal static string VariantSitemapRelativePath(AppConfig variantConfig)
        => For(variantConfig.Site).SitemapRelativePath(variantConfig.Site.Language);

    /// <summary>Search index path of the variant built with <paramref name="variantConfig"/>, relative to its output directory.</summary>
    internal static string VariantSearchRelativePath(AppConfig variantConfig)
        => For(variantConfig.Site).SearchRelativePath(variantConfig.Site.Language);

    /// <summary>Prefixes a relative output path with a language directory; absolute roots use <see cref="OutputDir"/>.</summary>
    internal static string CombineRelative(string prefix, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"Expected a relative output path: {relativePath}", nameof(relativePath));
        }

        return string.IsNullOrEmpty(prefix) ? relativePath : Path.Combine(prefix, relativePath);
    }

    private bool IsDefaultLanguage(string language)
        => string.Equals(language.Trim(), _defaultLanguage.Trim(), StringComparison.OrdinalIgnoreCase);
}
