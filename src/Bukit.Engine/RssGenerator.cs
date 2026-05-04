using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;

namespace Bukit.Engine;

public static class RssGenerator
{
    public sealed record Post(
        string Title,
        string AbsoluteUrl,
        DateTimeOffset PublishAt,
        string? Description,
        IReadOnlyList<string>? Categories,
        string? ContentHtml);

    public static void Generate(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> routed,
        IContentBodyStore bodyStore,
        int maxItems = 20)
    {
        var normalizedSiteUrl = NormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

        var rssCollections = ResolveRssCollections(collections);
        var posts = routed
            .Where(x => rssCollections.Contains(GetCollection(x.Item)))
            .OrderByDescending(x => x.Item.PublishAt)
            .Take(maxItems)
            .Select(x => new Post(
                Title: x.Item.Title,
                AbsoluteUrl: BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, x.Route.Url),
                PublishAt: x.Item.PublishAt,
                Description: GetString(x.Item.Meta, "summary"),
                Categories: MergeCategories(GetStringList(x.Item.Meta, "tags"), GetStringList(x.Item.Meta, "categories")),
                ContentHtml: ContentBodyResolver.GetHtml(x.Item, bodyStore)))
            .ToList();

        var feedUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/rss.xml");
        var homeUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/");
        FileWriter.WriteUtf8(outputDir, "rss.xml", RenderFeed(siteTitle, homeUrl, feedUrl, posts));
    }

    public static void GenerateMerged(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        IReadOnlyList<Post> posts,
        int maxItems = 20)
    {
        var normalizedSiteUrl = NormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);

        var sorted = posts
            .OrderByDescending(x => x.PublishAt)
            .GroupBy(x => x.AbsoluteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(maxItems)
            .ToList();

        var feedUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/rss.xml");
        var homeUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/");
        FileWriter.WriteUtf8(outputDir, "rss.xml", RenderFeed(siteTitle, homeUrl, feedUrl, sorted));
    }

    private static string RenderFeed(string siteTitle, string homeUrl, string feedUrl, IReadOnlyList<Post> posts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\" xmlns:content=\"http://purl.org/rss/1.0/modules/content/\">");
        sb.AppendLine("  <channel>");
        sb.AppendLine($"    <title>{EscapeXml(siteTitle)}</title>");
        sb.AppendLine($"    <link>{EscapeXml(homeUrl)}</link>");
        sb.AppendLine($"    <description>{EscapeXml(siteTitle)}</description>");
        sb.AppendLine($"    <lastBuildDate>{DateTimeOffset.UtcNow:R}</lastBuildDate>");
        sb.AppendLine("    <generator>bukit</generator>");
        sb.AppendLine($"    <atom:link href=\"{EscapeXml(feedUrl)}\" rel=\"self\" type=\"application/rss+xml\" />");

        foreach (var post in posts)
        {
            sb.AppendLine("    <item>");
            sb.AppendLine($"      <title>{EscapeXml(post.Title)}</title>");
            sb.AppendLine($"      <link>{EscapeXml(post.AbsoluteUrl)}</link>");
            sb.AppendLine($"      <guid>{EscapeXml(post.AbsoluteUrl)}</guid>");
            sb.AppendLine($"      <pubDate>{post.PublishAt:R}</pubDate>");

            if (!string.IsNullOrWhiteSpace(post.Description))
            {
                sb.AppendLine($"      <description>{EscapeXml(post.Description!)}</description>");
            }

            if (post.Categories is { Count: > 0 } cats)
            {
                foreach (var c in cats)
                {
                    sb.AppendLine($"      <category>{EscapeXml(c)}</category>");
                }
            }

            if (!string.IsNullOrWhiteSpace(post.ContentHtml))
            {
                sb.AppendLine($"      <content:encoded><![CDATA[{ToCData(post.ContentHtml!)}]]></content:encoded>");
            }

            sb.AppendLine("    </item>");
        }

        sb.AppendLine("  </channel>");
        sb.AppendLine("</rss>");
        return sb.ToString();
    }

    private static string ToCData(string value)
    {
        return value.Replace("]]>", "]]]]><![CDATA[>", StringComparison.Ordinal);
    }

    private static string? GetString(IReadOnlyDictionary<string, object> meta, string key)
    {
        return meta.TryGetValue(key, out var v) && v is not null ? v.ToString() : null;
    }

    private static IReadOnlyList<string>? GetStringList(IReadOnlyDictionary<string, object> meta, string key)
    {
        if (!meta.TryGetValue(key, out var v) || v is null)
        {
            return null;
        }

        if (v is string s)
        {
            var parts = s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 0 ? null : parts;
        }

        if (v is IEnumerable<object> seq)
        {
            var list = seq.Select(x => x?.ToString() ?? string.Empty)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return list.Count == 0 ? null : list;
        }

        return null;
    }

    private static IReadOnlyList<string>? MergeCategories(IReadOnlyList<string>? tags, IReadOnlyList<string>? categories)
    {
        if (tags is null && categories is null)
        {
            return null;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void Add(IReadOnlyList<string>? items)
        {
            if (items is null)
            {
                return;
            }

            foreach (var v in items)
            {
                var t = (v ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(t) && seen.Add(t))
                {
                    list.Add(t);
                }
            }
        }

        Add(tags);
        Add(categories);
        return list.Count == 0 ? null : list;
    }

    private static HashSet<string> ResolveRssCollections(IReadOnlyDictionary<string, CollectionConfig>? collections)
    {
        if (collections is null || collections.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "post" };
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, cfg) in collections)
        {
            if (cfg.Output.Rss)
            {
                set.Add(key);
            }
        }

        return set.Count == 0 ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "post" } : set;
    }

    private static string GetCollection(ContentItem item)
    {
        if (item.Meta.TryGetValue("collection", out var collection) && collection is not null && !string.IsNullOrWhiteSpace(collection.ToString()))
        {
            return collection.ToString()!;
        }

        if (item.Meta.TryGetValue("type", out var type) && type is not null && !string.IsNullOrWhiteSpace(type.ToString()))
        {
            return type.ToString()!;
        }

        return "page";
    }

    public static string BuildAbsoluteUrl(string siteUrl, string baseUrl, string url)
    {
        var u = url.StartsWith('/') ? url : "/" + url;
        var path = baseUrl == "/" ? u : $"{baseUrl}{u}";
        return siteUrl + path;
    }

    private static string NormalizeSiteUrl(string siteUrl)
    {
        var trimmed = siteUrl.Trim();
        if (trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "/";
        }

        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        if (trimmed.Length > 1 && trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }
}
