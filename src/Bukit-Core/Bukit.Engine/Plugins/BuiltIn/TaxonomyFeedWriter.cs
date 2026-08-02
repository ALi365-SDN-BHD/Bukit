using System.Text;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
namespace Bukit.Engine.Plugins.BuiltIn;

using Bukit.Engine.Abstractions.Plugins;

internal static class TaxonomyFeedWriter
{
    internal static void WriteFeeds(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        Dictionary<string, TaxonomyTerm> terms,
        string kind,
        string? routePrefix = null)
    {
        var normalizedSiteUrl = NormalizeFeedUrl(siteUrl);
        var normalizedBaseUrl = NormalizeFeedUrl(baseUrl);
        var normalizedRoutePrefix = TaxonomyPageCreator.NormalizeRoutePrefix(kind, routePrefix);
        var outputPrefix = normalizedRoutePrefix.Trim('/');

        foreach (var term in terms.Values)
        {
            if (term.Pages.Count == 0)
            {
                continue;
            }

            var posts = term.Pages
                .OrderByDescending(p => p.PublishAt)
                .Take(20)
                .ToList();

            if (posts.Count == 0)
            {
                continue;
            }

            var termPath = BuildTermPath(normalizedRoutePrefix, term.Slug);
            var termUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, termPath);
            var feedUrl = $"{termUrl}feed.xml";
            var xml = RenderFeed($"{siteTitle}: {term.DisplayName}", term.Description, termUrl, feedUrl, posts, normalizedSiteUrl, normalizedBaseUrl);

            var feedDir = string.IsNullOrWhiteSpace(outputPrefix)
                ? Path.Combine(outputDir, term.Slug)
                : Path.Combine(outputDir, Path.Combine(outputPrefix.Split('/')), term.Slug);
            Directory.CreateDirectory(feedDir);
            File.WriteAllText(Path.Combine(feedDir, "feed.xml"), xml, Encoding.UTF8);
        }
    }

    private static string BuildTermPath(string routePrefix, string slug)
    {
        var prefix = routePrefix == "/" ? string.Empty : routePrefix;
        return $"{prefix}/{slug}/";
    }

    private static string BuildAbsoluteUrl(string siteUrl, string baseUrl, string path)
    {
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        if (baseUrl == "/")
        {
            return siteUrl + normalizedPath;
        }

        return siteUrl + baseUrl + normalizedPath;
    }

    private static string RenderFeed(
        string title,
        string? description,
        string homeUrl,
        string feedUrl,
        IReadOnlyList<TaxonomyPage> posts,
        string siteUrl,
        string baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\">");
        sb.AppendLine("<channel>");
        sb.Append("  <title>").Append(EscapeXml(title)).AppendLine("</title>");
        sb.Append("  <link>").Append(EscapeXml(homeUrl)).AppendLine("</link>");
        if (!string.IsNullOrWhiteSpace(description))
        {
            sb.Append("  <description>").Append(EscapeXml(description!)).AppendLine("</description>");
        }
        sb.Append("  <atom:link href=\"").Append(EscapeXml(feedUrl)).AppendLine("\" rel=\"self\" type=\"application/rss+xml\"/>");

        var latest = posts.Count > 0 ? posts[0].PublishAt : DateTimeOffset.UnixEpoch;
        sb.Append("  <lastBuildDate>").Append(latest.ToString("R")).AppendLine("</lastBuildDate>");

        foreach (var post in posts)
        {
            var absoluteUrl = BuildAbsoluteUrl(siteUrl, baseUrl, post.Url);
            sb.AppendLine("  <item>");
            sb.Append("    <title>").Append(EscapeXml(post.Title)).AppendLine("</title>");
            sb.Append("    <link>").Append(EscapeXml(absoluteUrl)).AppendLine("</link>");
            sb.Append("    <guid isPermaLink=\"true\">").Append(EscapeXml(absoluteUrl)).AppendLine("</guid>");
            sb.Append("    <pubDate>").Append(post.PublishAt.ToString("R")).AppendLine("</pubDate>");
            if (!string.IsNullOrWhiteSpace(post.Summary))
            {
                sb.Append("    <description>").Append(EscapeXml(post.Summary!)).AppendLine("</description>");
            }
            sb.AppendLine("  </item>");
        }

        sb.AppendLine("</channel>");
        sb.AppendLine("</rss>");
        return sb.ToString();
    }

    private static string NormalizeFeedUrl(string url)
    {
        var trimmed = (url ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        if (trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        if (!trimmed.StartsWith('/') && !trimmed.Contains("://"))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed;
    }

    private static string EscapeXml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
