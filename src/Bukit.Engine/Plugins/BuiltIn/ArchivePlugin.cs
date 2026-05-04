using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class ArchivePlugin : IBukitPlugin, IDerivePagesPlugin
{
    public string Name => "archive";
    public string Version => "2.0.0";

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var archiveCollection = ResolveArchiveCollection(context.Config);
        var collectionKey = archiveCollection?.Key ?? "post";
        var listRoute = archiveCollection?.Config.ListRoute ?? "/blog/";
        var archiveBaseUrl = $"{NormalizeRoute(listRoute)}archive/";
        var posts = CollectionRouteIndex.GetOrBuild(context).GetByCollection(collectionKey);

        if (posts.Count == 0)
        {
            return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
        }

        var prefix = context.BaseUrl == "/" ? string.Empty : context.BaseUrl;

        var byYear = posts
            .GroupBy(x => x.Item.PublishAt.Year)
            .OrderByDescending(g => g.Key)
            .ToList();

        var derived = new List<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();

        derived.Add(CreateArchiveIndex(prefix, archiveBaseUrl, byYear, collectionKey));

        foreach (var yearGroup in byYear)
        {
            var yearPosts = yearGroup.ToList();
            derived.Add(CreateYearPage(prefix, archiveBaseUrl, yearGroup.Key, yearPosts, collectionKey));

            var byMonth = yearPosts
                .GroupBy(x => x.Item.PublishAt.Month)
                .OrderByDescending(g => g.Key)
                .ToList();

            foreach (var monthGroup in byMonth)
            {
                derived.Add(CreateMonthPage(prefix, archiveBaseUrl, yearGroup.Key, monthGroup.Key, monthGroup.ToList(), collectionKey));
            }
        }

        return derived;
    }

    private static (ContentItem Item, RouteInfo Route, DateTimeOffset LastModified) CreateArchiveIndex(
        string baseUrlPrefix,
        string archiveBaseUrl,
        IReadOnlyList<IGrouping<int, (ContentItem Item, RouteInfo Route)>> byYear,
        string collectionKey)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var g in byYear)
        {
            var href = $"{baseUrlPrefix}{archiveBaseUrl}{g.Key}/";
            sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{g.Key}</a></li>");
        }
        sb.AppendLine("</ul>");

        var now = DateTimeOffset.UtcNow;
        var route = new RouteInfo(archiveBaseUrl, BuildOutputPath(archiveBaseUrl), "pages/page.html");
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page", ["collection"] = collectionKey };
        var item = new ContentItem("blog-archive-index", "Archive", "archive", now, sb.ToString(), meta);
        return (item, route, now);
    }

    private static (ContentItem Item, RouteInfo Route, DateTimeOffset LastModified) CreateYearPage(
        string baseUrlPrefix,
        string archiveBaseUrl,
        int year,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> yearPosts,
        string collectionKey)
    {
        var byMonth = yearPosts
            .GroupBy(x => x.Item.PublishAt.Month)
            .OrderByDescending(g => g.Key)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var g in byMonth)
        {
            var href = $"{baseUrlPrefix}{archiveBaseUrl}{year}/{g.Key:D2}/";
            sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{year}-{g.Key:D2}</a> <small>({g.Count()})</small></li>");
        }
        sb.AppendLine("</ul>");

        var publishAt = yearPosts.OrderByDescending(x => x.Item.PublishAt).First().Item.PublishAt;
        var url = $"{archiveBaseUrl}{year}/";
        var outputPath = BuildOutputPath(url);
        var route = new RouteInfo(url, outputPath, "pages/page.html");
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page", ["collection"] = collectionKey };
        var item = new ContentItem($"blog-archive-{year}", $"Archive: {year}", $"archive-{year}", publishAt, sb.ToString(), meta);
        return (item, route, publishAt);
    }

    private static (ContentItem Item, RouteInfo Route, DateTimeOffset LastModified) CreateMonthPage(
        string baseUrlPrefix,
        string archiveBaseUrl,
        int year,
        int month,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> monthPosts,
        string collectionKey)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var (item, route) in monthPosts.OrderByDescending(x => x.Item.PublishAt))
        {
            var href = $"{baseUrlPrefix}{route.Url}";
            sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{EscapeHtml(item.Title)}</a></li>");
        }
        sb.AppendLine("</ul>");

        var publishAt = monthPosts.OrderByDescending(x => x.Item.PublishAt).First().Item.PublishAt;
        var url = $"{archiveBaseUrl}{year}/{month:D2}/";
        var outputPath = BuildOutputPath(url);
        var routeInfo = new RouteInfo(url, outputPath, "pages/page.html");
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["type"] = "page", ["collection"] = collectionKey };
        var itemInfo = new ContentItem($"blog-archive-{year}-{month:D2}", $"Archive: {year}-{month:D2}", $"archive-{year}-{month:D2}", publishAt, sb.ToString(), meta);
        return (itemInfo, routeInfo, publishAt);
    }

    private static (string Key, CollectionConfig Config)? ResolveArchiveCollection(AppConfig config)
    {
        if (config.Site.Collections is null)
        {
            return null;
        }

        foreach (var entry in config.Site.Collections)
        {
            if (entry.Value.Output.Archive)
            {
                return (entry.Key, entry.Value);
            }
        }

        return null;
    }

    private static string NormalizeRoute(string route)
    {
        var value = (route ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/blog/";
        }

        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        if (!value.EndsWith('/'))
        {
            value += "/";
        }

        return value;
    }

    private static string BuildOutputPath(string url)
    {
        var normalized = url.Trim('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(normalized, "index.html");
    }

    private static string EscapeHtml(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string EscapeAttr(string value)
    {
        return (value ?? string.Empty)
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }
}
