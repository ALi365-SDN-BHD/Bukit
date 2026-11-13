using Bukit.Config;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

/// <summary>
/// Checks whether the placeholder that <c>content.media.onFailure: placeholder</c> substitutes for a
/// failed image download (<c>content.media.defaultImageUrl</c>) is provided by the theme or site.
/// Looks where the asset output plan copies files from: the theme's and parent theme's static
/// directories at the output root, and their assets directories under <c>assets/</c>.
/// </summary>
internal static class MediaPlaceholderProbe
{
    /// <summary>
    /// Returns the placeholder URL (for example <c>/assets/images/noneimg-news.jpg</c>) when
    /// <c>onFailure</c> is <c>placeholder</c> and no theme or site directory provides that file;
    /// otherwise null: the file exists, <c>onFailure</c> is <c>keep</c>, the placeholder is not a
    /// local path, or the theme paths cannot be resolved (the build reports that on its own).
    /// </summary>
    internal static string? FindMissing(AppConfig config, string rootDir, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        var media = config.Content.Media;
        if (!MediaLocalizationRules.TryParseOnFailure(media.OnFailure, out var keep) || keep)
        {
            return null;
        }

        var url = PlaceholderUrl(media);
        if (url is null || ToRelativePath(url) is not { } relative)
        {
            return null;
        }

        ResolvedThemePaths paths;
        try
        {
            paths = ThemePathResolver.Resolve(rootDir, config.Theme, logger);
        }
        catch (ConfigException)
        {
            return null;
        }

        return Provides(relative, paths.StaticDir, paths.AssetsDir) ||
               Provides(relative, paths.ParentStaticDir, paths.ParentAssetsDir)
            ? null
            : url;
    }

    /// <summary>
    /// The <c>config check</c> / <c>doctor</c> warning for a missing placeholder, or null when
    /// there is nothing to warn about (including <c>downloadToLocal: false</c>).
    /// </summary>
    internal static string? CheckWarning(AppConfig config, string rootDir, ILogger logger)
    {
        if (!config.Content.Media.DownloadToLocal || FindMissing(config, rootDir, logger) is not { } missing)
        {
            return null;
        }

        return $"⚠ content.media.onFailure is placeholder, but the placeholder {missing} (content.media.defaultImageUrl) " +
            "is not in the theme or site. Images that cannot be downloaded will keep their original remote URL. " +
            $"To show a placeholder instead, add {missing.TrimStart('/')} to the theme or site (under assets/ or static/); " +
            "to keep the original URL without this warning, set content.media.onFailure: keep.";
    }

    /// <summary>The site-relative placeholder URL the localizer substitutes, or null when it is not a local path.</summary>
    private static string? PlaceholderUrl(MediaConfig media)
    {
        var raw = (media.DefaultImageUrl ?? string.Empty).Trim();
        if (raw.StartsWith("//", StringComparison.Ordinal) ||
            (Uri.TryCreate(raw, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https"))
        {
            return null;
        }

        // Same normalization as ContentProviderFactory.BuildEffectiveMediaConfig.
        if (raw.Length == 0)
        {
            raw = "/assets/images/noneimg-news.jpg";
        }

        return raw.StartsWith('/') ? raw : "/" + raw;
    }

    private static string? ToRelativePath(string url)
    {
        var suffixAt = url.IndexOfAny(['?', '#']);
        var path = Uri.UnescapeDataString(suffixAt < 0 ? url : url[..suffixAt]).TrimStart('/');
        var segments = path.Split('/');
        if (path.Length == 0 || path.Contains('\\') || segments.Any(segment => segment is "" or "." or ".."))
        {
            return null;
        }

        return path;
    }

    private static bool Provides(string relative, string? staticDir, string? assetsDir)
    {
        if (staticDir is not null && File.Exists(Path.Combine(staticDir, relative)))
        {
            return true;
        }

        const string assetsPrefix = "assets/";
        return assetsDir is not null &&
               relative.StartsWith(assetsPrefix, StringComparison.Ordinal) &&
               File.Exists(Path.Combine(assetsDir, relative[assetsPrefix.Length..]));
    }
}
