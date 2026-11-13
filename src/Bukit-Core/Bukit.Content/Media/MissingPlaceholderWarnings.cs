using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Bukit.Shared;

namespace Bukit.Content.Media;

/// <summary>
/// Warnings for failed images that keep their original URL because the placeholder
/// (<c>content.media.defaultImageUrl</c>) is not in the theme or site. Each image is reported once
/// per page. One instance belongs to one <see cref="ImageAssetLocalizer"/>.
/// </summary>
internal sealed class MissingPlaceholderWarnings
{
    internal const string Event = "event=media.placeholder_missing";

    // (source, page) pairs already reported.
    private readonly ConcurrentDictionary<string, byte> _reported = new(StringComparer.Ordinal);

    internal void Warn(ILogger? logger, string sourceUrl, string placeholder)
    {
        var source = KeptUrlForLog(sourceUrl);
        var page = MediaPageContext.Current is { Length: > 0 } current ? current : "unknown";
        if (logger is null || !_reported.TryAdd(source + "\n" + page, 0))
        {
            return;
        }

        logger.Warn(
            $"{Event} source={source} page=\"{page}\" placeholder={placeholder} " +
            "result=kept_original_url " +
            $"hint=\"the image could not be downloaded and the placeholder {placeholder} is not in the theme or site; " +
            "the page links to the remote image instead. To fix: add that file to the theme or site (under assets/ or static/), " +
            "or set content.media.onFailure: keep\"");
    }

    // Other media events log only scheme and host (UrlRedactor). This URL is about to be written
    // into the published page, so its path is no secret and is what lets the user find the image;
    // userinfo, query and fragment (where signed URLs carry their tokens) are still left out.
    internal static string KeptUrlForLog(string url)
    {
        var text = url.Contains("&amp;", StringComparison.Ordinal) ? WebUtility.HtmlDecode(url) : url;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host))
        {
            return UrlRedactor.Redact(url);
        }

        var port = uri.IsDefaultPort ? string.Empty : ":" + uri.Port.ToString(CultureInfo.InvariantCulture);
        var query = uri.Query.Length > 0 ? "?<redacted-query>" : string.Empty;
        return $"{uri.Scheme}://{uri.Host}{port}{uri.AbsolutePath}{query}";
    }
}
