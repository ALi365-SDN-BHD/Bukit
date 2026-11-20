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

    internal MiniProgramUrls(string siteUrl, string baseUrl, SharedAssetScope? sharedAssets, string mediaUrlBase)
    {
        SiteUrl = siteUrl;
        BaseUrl = baseUrl;
        _sharedAssets = sharedAssets;
        _mediaUrlBase = mediaUrlBase.TrimEnd('/');
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

        if (trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            return "https:" + trimmed;
        }

        if (trimmed.StartsWith('/'))
        {
            return SharedRootPath(trimmed) is { } shared
                ? SeoModelBuilder.BuildAbsoluteUrl(SiteUrl, _sharedAssets!.RootBaseUrl, shared)
                : SeoModelBuilder.ResolveCanonical(SiteUrl, BaseUrl, trimmed, trimmed);
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) && !absolute.IsFile)
        {
            return trimmed;
        }

        return Uri.TryCreate(new Uri(relativeTo), trimmed, out var resolved) ? resolved.AbsoluteUri : trimmed;
    }

    /// <summary>The site path of a shared root asset (without the variant base path), or null.</summary>
    private string? SharedRootPath(string path)
    {
        if (_sharedAssets is null)
        {
            return null;
        }

        var variantBase = BuildPathUtils.NormalizeBaseUrl(BaseUrl).TrimEnd('/');
        var sitePath = variantBase.Length > 0 && path.StartsWith(variantBase + "/", StringComparison.Ordinal)
            ? path[variantBase.Length..]
            : path;
        var end = sitePath.IndexOfAny(['?', '#']);
        var bare = end < 0 ? sitePath : sitePath[..end];
        return bare.StartsWith(_mediaUrlBase + "/", StringComparison.Ordinal) || !_sharedAssets.ContainsSitePath(bare)
            ? null
            : sitePath;
    }
}
