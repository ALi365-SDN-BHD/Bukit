using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.BuiltIn.MiniProgram;

/// <summary>
/// Absolute URLs for one variant's mini-program files. Site-root paths get the variant base path,
/// except files that <c>site.sharedAssets</c> publishes once at the root for every language: those
/// keep the root base path, as the website's own links to them do. Localized media is copied into
/// every variant, so it always keeps the variant base path.
/// </summary>
internal sealed class MiniProgramUrls
{
    private readonly SharedAssetScope? _sharedAssets;
    private readonly string _mediaUrlBase;
    // Base paths without the trailing slash: "" for the site root, "/zh-CN" or "/repo/zh-CN" otherwise.
    private readonly string _variantBase;
    private readonly string _rootBase;

    internal MiniProgramUrls(string siteUrl, string baseUrl, SharedAssetScope? sharedAssets, string mediaUrlBase)
    {
        SiteUrl = siteUrl;
        BaseUrl = baseUrl;
        _sharedAssets = sharedAssets;
        _mediaUrlBase = mediaUrlBase.TrimEnd('/');
        _variantBase = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        _rootBase = sharedAssets is null ? _variantBase : BuildPathUtils.NormalizeBaseUrl(sharedAssets.RootBaseUrl).TrimEnd('/');
        SiteRoot = SeoModelBuilder.BuildAbsoluteUrl(siteUrl, baseUrl, "/");
    }

    /// <summary>The URLs of one variant build; non-default languages see the shared asset scope.</summary>
    internal static MiniProgramUrls For(BuildContext context, AppConfig config, string siteUrl)
        => new(
            siteUrl,
            context.BaseUrl,
            context.TryGet(BuildDataKeys.SharedAssets, out var shared) ? shared.Scope : null,
            ContentProviderFactory.NormalizeMediaUrlBase(config.Content.Media.UrlBase));

    internal string SiteUrl { get; }
    internal string BaseUrl { get; }

    /// <summary>The variant's home page; records without a page resolve relative paths against it.</summary>
    internal string SiteRoot { get; }

    /// <summary>
    /// Makes <paramref name="value"/> absolute: site-root paths join the site URL, other relative paths
    /// resolve against <paramref name="relativeTo"/>. Fragments, <c>mailto:</c>, <c>tel:</c> and other
    /// non-HTTP schemes are returned unchanged.
    /// </summary>
    internal string Absolute(string value, string relativeTo)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return trimmed;
        }

        if (trimmed.StartsWith('/') && !trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            // A site-root path may still lack the base path, so it is checked before the base is added.
            return SharedRootUrl(trimmed, baseless: true) ?? SeoModelBuilder.ResolveCanonical(SiteUrl, BaseUrl, trimmed, trimmed);
        }

        string absolute;
        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            absolute = "https:" + trimmed;
        }
        else if (Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed) && !parsed.IsFile)
        {
            absolute = trimmed;
        }
        else if (Uri.TryCreate(new Uri(relativeTo), trimmed, out var resolved))
        {
            absolute = resolved.AbsoluteUri;
        }
        else
        {
            return trimmed;
        }

        // Relative paths and full URLs of this site can point at a shared file too.
        return _sharedAssets is not null && absolute.StartsWith(SiteUrl + "/", StringComparison.OrdinalIgnoreCase)
            ? SharedRootUrl(absolute[SiteUrl.Length..], baseless: false) ?? absolute
            : absolute;
    }

    /// <summary>
    /// The root-base URL of a shared root asset, or null. <paramref name="path"/> is a site path that
    /// carries the variant or root base path; <paramref name="baseless"/> also accepts one without it.
    /// </summary>
    private string? SharedRootUrl(string path, bool baseless)
    {
        if (_sharedAssets is null)
        {
            return null;
        }

        string sitePath;
        if (_variantBase.Length > 0 && path.StartsWith(_variantBase + "/", StringComparison.Ordinal))
        {
            sitePath = path[_variantBase.Length..];
        }
        else if (_rootBase.Length > 0 && path.StartsWith(_rootBase + "/", StringComparison.Ordinal))
        {
            sitePath = path[_rootBase.Length..];
        }
        else if (baseless || _rootBase.Length == 0)
        {
            sitePath = path;
        }
        else
        {
            return null;
        }

        var end = sitePath.IndexOfAny(['?', '#']);
        var bare = end < 0 ? sitePath : sitePath[..end];
        return bare.StartsWith(_mediaUrlBase + "/", StringComparison.Ordinal) || !_sharedAssets.ContainsSitePath(bare)
            ? null
            : SeoModelBuilder.BuildAbsoluteUrl(SiteUrl, _sharedAssets.RootBaseUrl, sitePath);
    }
}
