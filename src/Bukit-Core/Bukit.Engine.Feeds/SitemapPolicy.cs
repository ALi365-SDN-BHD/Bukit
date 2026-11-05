using System.Text.RegularExpressions;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine;

internal enum SitemapLocDecision
{
    Accepted,
    OutOfScope,
    Duplicate
}

/// <summary>
/// Sitemap locs are canonical URLs, but a sitemap may only list URLs under its own
/// location (same host and base path), and each URL once. Pages whose canonical points
/// to another host or outside the base path are left out; duplicate canonicals keep
/// the first entry. The publish audit uses <see cref="IsInScope"/> so it does not
/// expect those pages in the sitemap.
/// </summary>
internal sealed class SitemapLocFilter
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    private readonly string _scopeOrigin;
    private readonly string _scopePath;

    internal SitemapLocFilter(string siteUrl, string baseUrl)
    {
        Scope = SitemapGenerator.BuildAbsoluteUrl(siteUrl, baseUrl, "/");
        (_scopeOrigin, _scopePath, _) = SplitOrigin(Scope);
    }

    internal string Scope { get; }

    /// <summary>
    /// Scheme and host compare case-insensitively; the path compares case-sensitively,
    /// because <c>/REPO/</c> is a different directory from <c>/repo/</c> on most hosts.
    /// </summary>
    internal bool IsInScope(string? canonical)
    {
        if (string.IsNullOrWhiteSpace(canonical))
        {
            return false;
        }

        var (origin, path, _) = SplitOrigin(canonical);
        return string.Equals(origin, _scopeOrigin, StringComparison.OrdinalIgnoreCase) &&
               path.StartsWith(_scopePath, StringComparison.Ordinal);
    }

    // Normalizes the origin so default ports, host case and a trailing host dot do not
    // make a same-site URL look foreign. The path and query keep their case.
    private static (string Origin, string Path, string Query) SplitOrigin(string url)
    {
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            var host = uri.IdnHost.TrimEnd('.');
            var origin = uri.IsDefaultPort ? $"{uri.Scheme}://{host}" : $"{uri.Scheme}://{host}:{uri.Port}";
            return (origin, uri.AbsolutePath, uri.Query);
        }

        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        var pathStart = schemeEnd < 0 ? -1 : url.IndexOf('/', schemeEnd + 3);
        return pathStart < 0 ? (url, "/", string.Empty) : (url[..pathStart], url[pathStart..], string.Empty);
    }

    // Two canonicals that differ only by origin spelling (case, default port) are the same URL.
    private static string DedupeKey(string canonical)
    {
        var (origin, path, query) = SplitOrigin(canonical);
        return origin.ToLowerInvariant() + path + query;
    }

    internal SitemapLocDecision Evaluate(string? canonical)
    {
        if (!IsInScope(canonical))
        {
            return SitemapLocDecision.OutOfScope;
        }

        return _seen.Add(DedupeKey(canonical!)) ? SitemapLocDecision.Accepted : SitemapLocDecision.Duplicate;
    }

    /// <summary>Evaluates the canonical and logs why a route was left out.</summary>
    internal bool TryAccept(string routeUrl, string? canonical, ILogger? logger)
    {
        var decision = Evaluate(canonical);
        switch (decision)
        {
            case SitemapLocDecision.OutOfScope:
                logger?.Info($"event=sitemap.canonical_out_of_scope route={routeUrl} canonical={canonical} scope={Scope}");
                break;
            case SitemapLocDecision.Duplicate:
                logger?.Debug($"event=sitemap.canonical_duplicate route={routeUrl} canonical={canonical}");
                break;
        }

        return decision == SitemapLocDecision.Accepted;
    }
}

internal static partial class SitemapPolicy
{
    internal static DateTimeOffset ResolveLastModified(ContentDocument document)
    {
        if (document.Record.Lifecycle.UpdatedAt is { } updatedAt)
        {
            return updatedAt;
        }

        if (document.CustomFields is not null && document.CustomFields.TryGetValue("update_time", out var field) && field is not null)
        {
            if (TryReadDate(field.Value, out var dt))
            {
                return dt;
            }
        }

        return document.PublishAt;
    }

    internal static bool ShouldExcludeFromSitemap(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        foreach (Match match in MetaTagRegex().Matches(html))
        {
            var tag = match.Value;
            var name = ReadAttributeValue(tag, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var content = ReadAttributeValue(tag, "content") ?? string.Empty;
            if (name.Equals("robots", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsToken(content, "noindex") || ContainsToken(content, "none"))
                {
                    return true;
                }
            }
            else if (name.Equals("sitemap", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsToken(content, "exclude") || ContainsToken(content, "noindex") || EqualsToken(content, "false") || EqualsToken(content, "0"))
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool ShouldExcludeFromSitemapFile(string absoluteHtmlPath, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(absoluteHtmlPath))
        {
            return false;
        }

        if (!File.Exists(absoluteHtmlPath))
        {
            return false;
        }

        try
        {
            var html = File.ReadAllText(absoluteHtmlPath);
            return ShouldExcludeFromSitemap(html);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Warn($"event=sitemap.meta_read_failed path={absoluteHtmlPath} error={ex.Message}");
            return false;
        }
    }

    private static bool TryReadDate(object? value, out DateTimeOffset date)
    {
        if (value is DateTimeOffset dto)
        {
            date = dto;
            return true;
        }

        if (value is DateTime dt)
        {
            date = dt.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Local)).ToUniversalTime()
                : new DateTimeOffset(dt).ToUniversalTime();
            return true;
        }

        var text = value?.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            date = default;
            return false;
        }

        return DateTimeOffset.TryParse(text.Trim(), out date);
    }

    private static string? ReadAttributeValue(string tag, string key)
    {
        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var m = Regex.Match(tag, $@"\b{Regex.Escape(key)}\s*=\s*(?:""(?<q>[^""]*)""|'(?<q>[^']*)'|(?<u>[^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
        {
            return null;
        }

        var q = m.Groups["q"].Value;
        if (!string.IsNullOrEmpty(q))
        {
            return q.Trim();
        }

        var u = m.Groups["u"].Value;
        return string.IsNullOrWhiteSpace(u) ? null : u.Trim();
    }

    private static bool ContainsToken(string content, string token)
    {
        return content.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    private static bool EqualsToken(string content, string token)
    {
        return string.Equals(content.Trim(), token, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("<meta\\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetaTagRegex();
}
