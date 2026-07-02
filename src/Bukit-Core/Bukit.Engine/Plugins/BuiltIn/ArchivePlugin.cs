using System.Text;
using Bukit.Config;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;

namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class ArchivePlugin : IBukitPlugin, IDerivePagesPlugin, ITemplateRequirementPlugin
{
    public string Name => "archive";
    public string Version => "2.0.0";

    public IReadOnlyList<string> GetTemplateRequirementKinds(BuildContext context)
    {
        var archiveCollection = ResolveArchiveCollection(context.Config);
        if (archiveCollection is null || string.IsNullOrWhiteSpace(archiveCollection.Value.Config.ListRoute))
        {
            return Array.Empty<string>();
        }

        var posts = CollectionRouteIndex.GetOrBuild(context).GetByCollection(archiveCollection.Value.Key);
        return posts.Count > 0 ? new[] { "archive" } : Array.Empty<string>();
    }

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
    {
        var archiveCollection = ResolveArchiveCollection(context.Config);
        if (archiveCollection is null || string.IsNullOrWhiteSpace(archiveCollection.Value.Config.ListRoute))
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var collectionKey = archiveCollection.Value.Key;
        var listRoute = archiveCollection.Value.Config.ListRoute;
        var archiveBaseUrl = $"{RoutePathBuilder.NormalizeListRoute(listRoute)}archive/";
        var posts = CollectionRouteIndex.GetOrBuild(context).GetByCollection(collectionKey);

        if (posts.Count == 0)
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var prefix = context.BaseUrl == "/" ? string.Empty : context.BaseUrl;

        var byYear = posts
            .GroupBy(x => x.Document.PublishAt.Year)
            .OrderByDescending(g => g.Key)
            .ToList();

        var derived = new List<RoutedContentDocument>();
        var template = context.ResolveTemplateKind("archive");

        derived.Add(CreateArchiveIndex(prefix, archiveBaseUrl, collectionKey, context.Config.Site.OutputPathEncoding, byYear, template));

        foreach (var yearGroup in byYear)
        {
            var yearPosts = yearGroup.ToList();
            derived.Add(CreateYearPage(prefix, archiveBaseUrl, yearGroup.Key, yearPosts, collectionKey, context.Config.Site.OutputPathEncoding, template));

            var byMonth = yearPosts
                .GroupBy(x => x.Document.PublishAt.Month)
                .OrderByDescending(g => g.Key)
                .ToList();

            foreach (var monthGroup in byMonth)
            {
                derived.Add(CreateMonthPage(prefix, archiveBaseUrl, yearGroup.Key, monthGroup.Key, monthGroup.ToList(), collectionKey, context.Config.Site.OutputPathEncoding, template));
            }
        }

        return derived;
    }

    private static RoutedContentDocument CreateArchiveIndex(
        string baseUrlPrefix,
        string archiveBaseUrl,
        string collectionKey,
        string outputPathEncoding,
        IReadOnlyList<IGrouping<int, RoutedContentDocument>> byYear,
        string template)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var g in byYear)
        {
            var href = $"{baseUrlPrefix}{archiveBaseUrl}{g.Key}/";
            sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{g.Key}</a></li>");
        }
        sb.AppendLine("</ul>");

        var publishAt = byYear
            .SelectMany(g => g)
            .Select(x => x.Document.PublishAt)
            .DefaultIfEmpty(DateTimeOffset.UnixEpoch)
            .Max();
        var route = new RouteInfo(archiveBaseUrl, RoutePathBuilder.BuildOutputPathFromUrl(archiveBaseUrl, outputPathEncoding), template);
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "derived",
            ["collection"] = collectionKey,
            ["summary"] = $"Browse archived {collectionKey} entries by year."
        };
        var document = DerivedContentDocumentFactory.Create(
            $"{collectionKey}-archive-index",
            "Archive",
            "archive",
            publishAt,
            new ContentBodyRef(Html: sb.ToString()),
            ContentFieldReader.ToFieldMap(meta));
        return new RoutedContentDocument(document, route, publishAt);
    }

    private static RoutedContentDocument CreateYearPage(
        string baseUrlPrefix,
        string archiveBaseUrl,
        int year,
        IReadOnlyList<RoutedContentDocument> yearPosts,
        string collectionKey,
        string outputPathEncoding,
        string template)
    {
        var byMonth = yearPosts
            .GroupBy(x => x.Document.PublishAt.Month)
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

        var publishAt = yearPosts.OrderByDescending(x => x.Document.PublishAt).First().Document.PublishAt;
        var url = $"{archiveBaseUrl}{year}/";
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);
        var route = new RouteInfo(url, outputPath, template);
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "derived",
            ["collection"] = collectionKey,
            ["summary"] = $"Browse {collectionKey} entries published in {year}."
        };
        var document = DerivedContentDocumentFactory.Create(
            $"{collectionKey}-archive-{year}",
            $"Archive: {year}",
            $"archive-{year}",
            publishAt,
            new ContentBodyRef(Html: sb.ToString()),
            ContentFieldReader.ToFieldMap(meta));
        return new RoutedContentDocument(document, route, publishAt);
    }

    private static RoutedContentDocument CreateMonthPage(
        string baseUrlPrefix,
        string archiveBaseUrl,
        int year,
        int month,
        IReadOnlyList<RoutedContentDocument> monthPosts,
        string collectionKey,
        string outputPathEncoding,
        string template)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ul>");
        foreach (var routedDocument in monthPosts.OrderByDescending(x => x.Document.PublishAt))
        {
            var item = routedDocument.Document;
            var route = routedDocument.Route;
            var href = $"{baseUrlPrefix}{route.Url}";
            sb.AppendLine($"  <li><a href=\"{EscapeAttr(href)}\">{EscapeHtml(item.Title)}</a></li>");
        }
        sb.AppendLine("</ul>");

        var publishAt = monthPosts.OrderByDescending(x => x.Document.PublishAt).First().Document.PublishAt;
        var url = $"{archiveBaseUrl}{year}/{month:D2}/";
        var outputPath = RoutePathBuilder.BuildOutputPathFromUrl(url, outputPathEncoding);
        var routeInfo = new RouteInfo(url, outputPath, template);
        var meta = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = "derived",
            ["collection"] = collectionKey,
            ["summary"] = $"Browse {collectionKey} entries published in {year}-{month:D2}."
        };
        var document = DerivedContentDocumentFactory.Create(
            $"{collectionKey}-archive-{year}-{month:D2}",
            $"Archive: {year}-{month:D2}",
            $"archive-{year}-{month:D2}",
            publishAt,
            new ContentBodyRef(Html: sb.ToString()),
            ContentFieldReader.ToFieldMap(meta));
        return new RoutedContentDocument(document, routeInfo, publishAt);
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
