using System.Text.RegularExpressions;

namespace Bukit.Importing;

/// <summary>
/// Turns demo file links ("about.html", "../news.html") into the site URLs the imported
/// pages are published at, and keeps root-relative URLs working under a sub-path deploy.
/// </summary>
internal static partial class InternalLinkRewriter
{
    /// <summary>
    /// Template markup (page bodies, partials, components): page links become
    /// "{{ base_url }}/about/" and other root-relative href/src values get the
    /// "{{ base_url }}" prefix. The template must set base_url before this markup renders.
    /// </summary>
    internal static string RewriteTemplate(string html, string sourceRelativePath, PageTemplatePlan plan)
        => UrlAttributePattern().Replace(html, match =>
        {
            var attr = match.Groups["attr"].Value;
            var value = match.Groups["value"].Value;
            string? rewritten = null;

            if (attr.Equals("href", StringComparison.OrdinalIgnoreCase) &&
                PageUrl(value, sourceRelativePath, plan) is { } url)
            {
                rewritten = "{{ base_url }}" + url;
            }
            else if (IsRootRelative(value))
            {
                rewritten = "{{ base_url }}" + value;
            }

            return rewritten is null
                ? match.Value
                : $"{attr}={match.Groups["q"].Value}{rewritten}{match.Groups["q"].Value}";
        });

    /// <summary>
    /// Body HTML that becomes Markdown content: page links become root-relative site
    /// URLs ("/about/"). <see cref="PrefixRootUrls"/> adds the base path later.
    /// </summary>
    internal static string RewriteContentLinks(string html, string sourceRelativePath, PageTemplatePlan plan)
        => UrlAttributePattern().Replace(html, match =>
        {
            if (!match.Groups["attr"].Value.Equals("href", StringComparison.OrdinalIgnoreCase) ||
                PageUrl(match.Groups["value"].Value, sourceRelativePath, plan) is not { } url)
            {
                return match.Value;
            }

            var quote = match.Groups["q"].Value;
            return $"{match.Groups["attr"].Value}={quote}{url}{quote}";
        });

    /// <summary>
    /// Record content (HTML that becomes the Markdown body) cannot read base_url, so
    /// root-relative href/src values get the site's base path ("/corp") written in.
    /// </summary>
    internal static string PrefixRootUrls(string html, string basePath)
        => basePath.Length == 0 || string.IsNullOrEmpty(html)
            ? html
            : UrlAttributePattern().Replace(html, match =>
            {
                var value = match.Groups["value"].Value;
                if (!IsRootRelative(value))
                    return match.Value;

                var quote = match.Groups["q"].Value;
                return $"{match.Groups["attr"].Value}={quote}{basePath}{value}{quote}";
            });

    /// <summary>The path part of a configured base URL without a trailing slash ("" for "/").</summary>
    internal static string BasePath(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return "";

        var path = Uri.TryCreate(baseUrl, UriKind.Absolute, out var absolute) && absolute.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? absolute.AbsolutePath
            : baseUrl.Trim();
        path = "/" + path.Trim('/');
        return path == "/" ? "" : path;
    }

    /// <summary>The site URL of the demo page an href points to, or null.</summary>
    internal static string? PageUrl(string href, string sourceRelativePath, PageTemplatePlan plan)
    {
        var target = DemoLinks.ResolvePath(sourceRelativePath, href);
        if (target is null ||
            !(target.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
              target.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var page = plan.PageAt(target);
        if (page is null && DemoLinks.AlternatePageExtension(target) is { } alternate)
            page = plan.PageAt(alternate);
        if (page is null || plan.UrlFor(page) is not { } url)
            return null;

        var fragment = href.IndexOf('#');
        return fragment < 0 ? url : url + href[fragment..];
    }

    private static bool IsRootRelative(string value)
        => value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal);

    [GeneratedRegex(@"\b(?<attr>href|src|data-src|poster|data-poster)\s*=\s*(?<q>[""'])(?<value>[^""']*)\k<q>", RegexOptions.IgnoreCase)]
    private static partial Regex UrlAttributePattern();
}
