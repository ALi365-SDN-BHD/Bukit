using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
namespace Bukit.Engine;

public static class RssGenerator
{
    public sealed record Post(
        string Title,
        string AbsoluteUrl,
        DateTimeOffset PublishAt,
        string? Description,
        IReadOnlyList<string>? Categories,
        string? ContentHtml,
        string? Author = null,
        string? Language = null,
        string? Source = null,
        string? ReviewStatus = null,
        IReadOnlyList<string>? Entities = null);

    public static void Generate(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        IReadOnlyList<RoutedContentDocument> routed,
        IContentBodyStore bodyStore,
        IReadOnlyDictionary<string, SeoIndexEntry>? seoIndex = null,
        int maxItems = 20,
        string? siteDescription = null)
    {
        var normalizedSiteUrl = InternalNormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = InternalNormalizeBaseUrl(baseUrl);

        var rssCollections = ResolveRssCollections(collections);
        var posts = BuildPostsFromSeoIndex(seoIndex, routed, rssCollections, bodyStore)
            ?? routed
                .Where(x => rssCollections.Contains(GetCollection(x.Document)))
                .OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase)
                .Select(x => ToPost(x.Document, BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, x.Route.Url), bodyStore))
                .ToList();

        posts = posts
            .OrderByDescending(x => x.PublishAt)
            .Take(maxItems)
            .ToList();

        var feedUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/rss.xml");
        var homeUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/");
        FileWriter.WriteUtf8(outputDir, "rss.xml", RenderFeed(siteTitle, siteDescription, homeUrl, feedUrl, posts));
    }

    internal static List<Post> CollectPosts(
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        IReadOnlyList<RoutedContentDocument> routed,
        IContentBodyStore bodyStore,
        CanonicalContentGraph? contentGraph,
        IReadOnlyDictionary<string, SeoIndexEntry>? seoIndex,
        string siteUrl,
        string baseUrl,
        string? collectionKey)
    {
        var normalizedSiteUrl = InternalNormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = InternalNormalizeBaseUrl(baseUrl);

        var rssCollections = ResolveRssCollections(collections);
        if (collectionKey is not null)
        {
            rssCollections = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { collectionKey };
        }

        var posts = BuildPostsFromSeoIndex(seoIndex, routed, rssCollections, bodyStore)
            ?? routed
                .Where(x => rssCollections.Contains(GetCollection(x.Document)))
                .OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase)
                .Select(x => ToPost(x.Document, BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, x.Route.Url), bodyStore))
                .ToList();

        return posts;
    }

    internal static List<Post> CollectAllPosts(
        IReadOnlyDictionary<string, CollectionConfig>? collections,
        IReadOnlyList<RoutedContentDocument> routed,
        IContentBodyStore bodyStore,
        CanonicalContentGraph? contentGraph,
        IReadOnlyDictionary<string, SeoIndexEntry>? seoIndex,
        string siteUrl,
        string baseUrl)
    {
        return CollectPosts(collections, routed, bodyStore, contentGraph, seoIndex, siteUrl, baseUrl, null);
    }

    public static void GenerateMerged(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        IReadOnlyList<Post> posts,
        int maxItems = 20,
        string? siteDescription = null)
    {
        var normalizedSiteUrl = InternalNormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = InternalNormalizeBaseUrl(baseUrl);

        var sorted = posts
            .OrderByDescending(x => x.PublishAt)
            .GroupBy(x => x.AbsoluteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(maxItems)
            .ToList();

        var feedUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/rss.xml");
        var homeUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/");
        FileWriter.WriteUtf8(outputDir, "rss.xml", RenderFeed(siteTitle, siteDescription, homeUrl, feedUrl, sorted));
    }

    internal static void GenerateAtPath(
        string outputDir,
        string siteUrl,
        string baseUrl,
        string siteTitle,
        IReadOnlyList<Post> posts,
        string feedPath,
        int maxItems = 20,
        string? siteDescription = null)
    {
        if (string.IsNullOrWhiteSpace(feedPath))
        {
            throw new ArgumentException("RSS feed path must be non-empty.", nameof(feedPath));
        }

        var normalizedSiteUrl = InternalNormalizeSiteUrl(siteUrl);
        var normalizedBaseUrl = InternalNormalizeBaseUrl(baseUrl);
        var normalizedFeedPath = NormalizeRelativeFeedPath(feedPath);

        var sorted = posts
            .OrderByDescending(x => x.PublishAt)
            .GroupBy(x => x.AbsoluteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(maxItems)
            .ToList();

        var feedUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/" + normalizedFeedPath);
        var homeUrl = BuildAbsoluteUrl(normalizedSiteUrl, normalizedBaseUrl, "/");
        FileWriter.WriteUtf8(outputDir, normalizedFeedPath, RenderFeed(siteTitle, siteDescription, homeUrl, feedUrl, sorted));
    }

    private static string NormalizeRelativeFeedPath(string feedPath)
    {
        var normalized = feedPath.Trim().Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            throw new ArgumentException("RSS feed path must be a safe relative path.", nameof(feedPath));
        }

        return normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : normalized.TrimEnd('/') + "/rss.xml";
    }

    private static string RenderFeed(string siteTitle, string? siteDescription, string homeUrl, string feedUrl, IReadOnlyList<Post> posts)
    {
        var channelDescription = string.IsNullOrWhiteSpace(siteDescription) ? siteTitle : siteDescription.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\" xmlns:content=\"http://purl.org/rss/1.0/modules/content/\">");
        sb.AppendLine("  <channel>");
        sb.AppendLine($"    <title>{EscapeXml(siteTitle)}</title>");
        sb.AppendLine($"    <link>{EscapeXml(homeUrl)}</link>");
        sb.AppendLine($"    <description>{EscapeXml(channelDescription)}</description>");
        var lastBuildDate = posts.Count == 0 ? DateTimeOffset.UnixEpoch : posts.Max(x => x.PublishAt);
        sb.AppendLine($"    <lastBuildDate>{lastBuildDate:R}</lastBuildDate>");
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

            if (!string.IsNullOrWhiteSpace(post.Author))
            {
                sb.AppendLine($"      <author>{EscapeXml(post.Author!)}</author>");
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

    internal static List<Post>? BuildPostsFromSeoIndex(
        IReadOnlyDictionary<string, SeoIndexEntry>? seoIndex,
        IReadOnlyList<RoutedContentDocument> routed,
        IReadOnlySet<string> rssCollections,
        IContentBodyStore bodyStore)
    {
        if (seoIndex is null || seoIndex.Count == 0)
        {
            return null;
        }

        var documentsByPath = routed
            .GroupBy(x => BuildPathUtils.NormalizeRelPath(x.Route.OutputPath), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Document, StringComparer.OrdinalIgnoreCase);
        var posts = new List<Post>();
        foreach (var (key, entry) in seoIndex
                     .Where(x => x.Value.Indexable)
                     .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!documentsByPath.TryGetValue(key, out var document) ||
                !rssCollections.Contains(GetCollection(document)))
            {
                continue;
            }

            posts.Add(ToPost(document, entry.Canonical, bodyStore));
        }

        return posts;
    }

    internal static Post ToPost(ContentDocument document, string absoluteUrl, IContentBodyStore bodyStore)
    {
#pragma warning disable CS0618
        var html = ContentBodyResolver.GetHtml(document, bodyStore);
#pragma warning restore CS0618
        return new Post(
            Title: document.Record.Presentation.Title,
            AbsoluteUrl: absoluteUrl,
            PublishAt: document.Record.Lifecycle.PublishedAt,
            Description: document.Record.Presentation.Summary ?? ContentFieldReader.GetText(document.CustomFields, "summary"),
            Categories: MergeCategories(document.Record.Classification.Tags, document.Record.Classification.Sections, ContentFieldReader.GetTextList(document.CustomFields, "tags"), ContentFieldReader.GetTextList(document.CustomFields, "categories")),
            ContentHtml: html,
            Author: document.Record.Ownership.Author,
            Language: document.Record.Presentation.Language,
            Source: document.Record.Provenance.Source,
            ReviewStatus: string.IsNullOrWhiteSpace(document.Record.Trust.ReviewStatus) ? null : document.Record.Trust.ReviewStatus,
            Entities: document.Record.Entities.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static IReadOnlyList<string>? MergeCategories(IReadOnlyList<string>? tags, IReadOnlyList<string>? categories, IReadOnlyList<string>? fallbackTags = null, IReadOnlyList<string>? fallbackCategories = null)
    {
        tags ??= fallbackTags;
        categories ??= fallbackCategories;
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
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, cfg) in collections)
        {
            if (cfg.Output.Rss)
            {
                set.Add(key);
            }
        }

        return set;
    }

    private static string GetCollection(ContentDocument document)
    {
        return ContentFieldReader.GetCollection(document);
    }

    public static string BuildAbsoluteUrl(string siteUrl, string baseUrl, string url)
    {
        siteUrl = InternalNormalizeSiteUrl(siteUrl);
        baseUrl = InternalNormalizeBaseUrl(baseUrl);
        var u = url.StartsWith('/') ? url : "/" + url;
        var path = baseUrl == "/" ? u : $"{baseUrl}{u}";
        return siteUrl + path;
    }

    internal static string InternalNormalizeSiteUrl(string siteUrl)
    {
        var trimmed = siteUrl.Trim();
        if (trimmed.EndsWith('/'))
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    internal static string InternalNormalizeBaseUrl(string baseUrl)
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
