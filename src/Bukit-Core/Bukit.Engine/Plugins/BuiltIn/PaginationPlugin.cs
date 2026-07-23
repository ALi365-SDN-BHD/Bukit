using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class PaginationPlugin : IBukitPlugin, IDerivePagesPlugin, ITemplateRequirementPlugin
{
    private readonly AppConfig _config;

    internal PaginationPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "pagination";
    public string Version => "2.0.0";

    public IReadOnlyList<string> GetTemplateRequirementKinds(BuildContext context)
    {
        var paginationCollection = ResolvePaginationCollection(_config);
        if (paginationCollection is null || string.IsNullOrWhiteSpace(paginationCollection.Value.Config.ListRoute))
        {
            return Array.Empty<string>();
        }

        if (IsHandledByListRouteGraph(context, paginationCollection.Value.Key))
        {
            return Array.Empty<string>();
        }

        var pageSize = paginationCollection.Value.Config.Pagination.PageSize;
        var posts = CollectionRouteIndex.GetOrBuild(context).GetByCollection(paginationCollection.Value.Key);
        return posts.Count > pageSize ? new[] { "pagination" } : Array.Empty<string>();
    }

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
    {
        var paginationCollection = ResolvePaginationCollection(_config);
        if (paginationCollection is null || string.IsNullOrWhiteSpace(paginationCollection.Value.Config.ListRoute))
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var pageSize = paginationCollection.Value.Config.Pagination.PageSize;
        var collectionKey = paginationCollection.Value.Key;
        var listRoute = paginationCollection.Value.Config.ListRoute;
        if (IsHandledByListRouteGraph(context, collectionKey))
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var index = CollectionRouteIndex.GetOrBuild(context);
        var posts = index.GetByCollection(collectionKey);

        if (posts.Count <= pageSize)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var prefix = context.BaseUrl == "/" ? string.Empty : context.BaseUrl;
        var totalPages = (int)Math.Ceiling(posts.Count / (double)pageSize);
        var routeTemplate = context.ResolveTemplateKind("pagination");

        var derived = new List<RoutedContentDocument>();
        for (var page = 2; page <= totalPages; page++)
        {
            var start = (page - 1) * pageSize;
            var slice = posts.Skip(start).Take(pageSize).ToList();
            if (slice.Count == 0)
            {
                continue;
            }

            var publishAt = slice[0].Document.PublishAt;
            var listUrl = RoutePathBuilder.NormalizeListRoute(listRoute);
            var html = BuildPageHtml(prefix, listUrl, slice, page, totalPages);
            var url = $"{listUrl}page/{page}/";
            var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, _config.Site.OutputPathEncoding);
            var route = new RouteInfo(url, outputPath, routeTemplate);
            var fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = new ContentField("text", "derived"),
                ["collection"] = new ContentField("text", collectionKey),
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

            var document = DerivedContentDocumentFactory.Create(
                id: $"{collectionKey}-page-{page}",
                title: $"{collectionKey} - Page {page}",
                slug: $"page-{page}",
                publishAt: publishAt,
                body: new ContentBodyRef(Html: html),
                customFields: fields);

            derived.Add(new RoutedContentDocument(document, route, publishAt));
        }

        return derived;
    }

    private static string BuildPageHtml(
        string baseUrlPrefix,
        string listRoute,
        IReadOnlyList<RoutedContentDocument> posts,
        int page,
        int totalPages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var routedDocument in posts)
        {
            var item = routedDocument.Document;
            var route = routedDocument.Route;
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

    private static bool IsHandledByListRouteGraph(BuildContext context, string collectionKey)
    {
        return context.Data.TryGetValue(ListRouteGraphBuilder.BuildContextDataKey, out var value) &&
               value is ListRouteGraph graph &&
               graph.Routes.Any(route =>
                   route.Kind == ListRouteKind.CollectionPage &&
                   string.Equals(route.Collection, collectionKey, StringComparison.OrdinalIgnoreCase));
    }

    private static List<object> BuildItems(IReadOnlyList<RoutedContentDocument> posts)
    {
        var items = new List<object>(posts.Count);
        foreach (var routedDocument in posts)
        {
            var item = routedDocument.Document;
            var route = routedDocument.Route;
            var entry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["title"] = item.Title,
                ["url"] = route.Url,
                ["publish_date"] = item.PublishAt.DateTime
            };
            var summary = ContentFieldReader.GetSummary(item);
            if (!string.IsNullOrWhiteSpace(summary))
            {
                entry["summary"] = summary;
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
