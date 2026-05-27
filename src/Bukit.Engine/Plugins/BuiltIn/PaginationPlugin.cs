using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class PaginationPlugin : IBukitPlugin, IDerivePagesPlugin
{
    public string Name => "pagination";
    public string Version => "2.0.0";

    public IReadOnlyList<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)> DerivePages(BuildContext context)
    {
        var paginationCollection = ResolvePaginationCollection(context.Config);
        var pageSize = paginationCollection?.Config.Pagination.PageSize ?? 10;
        var collectionKey = paginationCollection?.Key ?? "post";
        var listRoute = paginationCollection?.Config.ListRoute ?? "/blog/";
        var index = CollectionRouteIndex.GetOrBuild(context);
        var posts = index.GetByCollection(collectionKey);

        if (posts.Count <= pageSize)
        {
            return Array.Empty<(ContentItem, RouteInfo, DateTimeOffset)>();
        }

        var prefix = context.BaseUrl == "/" ? string.Empty : context.BaseUrl;
        var totalPages = (int)Math.Ceiling(posts.Count / (double)pageSize);
        var routeTemplate = TemplateCapabilitiesResolver.SupportsPagination(TemplateCapabilitiesResolver.PaginationTemplatePath, context.LayoutsDir)
            ? TemplateCapabilitiesResolver.PaginationTemplatePath
            : "pages/page.html";

        var derived = new List<(ContentItem Item, RouteInfo Route, DateTimeOffset LastModified)>();
        for (var page = 2; page <= totalPages; page++)
        {
            var start = (page - 1) * pageSize;
            var slice = posts.Skip(start).Take(pageSize).ToList();
            if (slice.Count == 0)
            {
                continue;
            }

            var publishAt = slice[0].Item.PublishAt;
            var listUrl = RoutePathBuilder.NormalizeListRoute(listRoute);
            var html = BuildPageHtml(prefix, listUrl, slice, page, totalPages);
            var url = $"{listUrl}page/{page}/";
            var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, context.Config.Site.OutputPathEncoding);
            var route = new RouteInfo(url, outputPath, routeTemplate);
            var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "page",
                ["collection"] = collectionKey
            };
            var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["items"] = new ContentField("list", BuildItems(slice)),
                ["pagination"] = new ContentField("object", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["page"] = page,
                    ["page_size"] = pageSize,
                    ["total_pages"] = totalPages,
                    ["has_prev"] = page > 1,
                    ["has_next"] = page < totalPages
                })
            };

            var item = new ContentItem(
                Id: $"blog-page-{page}",
                Title: $"Blog - Page {page}",
                Slug: $"page-{page}",
                PublishAt: publishAt,
                ContentHtml: html,
                Meta: meta,
                Fields: fields);

            derived.Add((item, route, publishAt));
        }

        return derived;
    }

    private static string BuildPageHtml(
        string baseUrlPrefix,
        string listRoute,
        IReadOnlyList<(ContentItem Item, RouteInfo Route)> posts,
        int page,
        int totalPages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var (item, route) in posts)
        {
            var href = $"{baseUrlPrefix}{route.Url}";
            sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{EscapeHtml(item.Title)}</a></li>");
        }
        sb.AppendLine("</ul>");

        sb.AppendLine("<nav>");
        if (page > 1)
        {
            var prevHref = page == 2 ? $"{baseUrlPrefix}{listRoute}" : $"{baseUrlPrefix}{listRoute}page/{page - 1}/";
            sb.AppendLine($"  <a href=\"{EscapeAttr(prevHref)}\">Prev</a>");
        }

        if (page < totalPages)
        {
            var nextHref = $"{baseUrlPrefix}{listRoute}page/{page + 1}/";
            sb.AppendLine($"  <a href=\"{EscapeAttr(nextHref)}\">Next</a>");
        }
        sb.AppendLine("</nav>");

        return sb.ToString();
    }

    private static (string Key, CollectionConfig Config)? ResolvePaginationCollection(AppConfig config)
    {
        if (config.Site.Collections is null || config.Site.Collections.Count == 0)
        {
            return null;
        }

        foreach (var entry in config.Site.Collections)
        {
            if (entry.Value.Pagination.Enabled)
            {
                return (entry.Key, entry.Value);
            }
        }

        return null;
    }

    private static List<object> BuildItems(IReadOnlyList<(ContentItem Item, RouteInfo Route)> posts)
    {
        var items = new List<object>(posts.Count);
        foreach (var (item, route) in posts)
        {
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = item.Title,
                ["url"] = route.Url,
                ["publish_date"] = item.PublishAt.DateTime
            };
            if (item.Meta.TryGetValue("summary", out var summary) && summary is not null)
            {
                entry["summary"] = summary.ToString()!;
            }

            items.Add(entry);
        }

        return items;
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
