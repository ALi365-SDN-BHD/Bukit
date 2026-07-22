using Bukit.Config;
using Bukit.Engine.Abstractions.Content;

namespace Bukit.Engine;

internal sealed class I18nRootFeedWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["feed", "atom", "jsonfeed"];

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        _ = representation;
        var siteUrl = context.Config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl) ||
            SiteModeResolver.ResolveFeedMode(context.Config.Site) != "merged")
        {
            return;
        }

        GenerateMergedFeeds(context, siteUrl);
    }

    private static void GenerateMergedFeeds(I18nRootProjectionWriterContext context, string siteUrl)
    {
        var postCandidates = new List<(string RouteUrl, RssGenerator.Post Post)>();
        var rssCollections = ResolveRssCollections(context.Config.Site.Collections);
        foreach (var result in context.Results)
        {
            var documentsByPath = SearchIndexBuilder.BuildDocumentMap(result.RoutedDocuments);
            foreach (var (key, seo) in result.SeoIndex
                         .Where(x => x.Value.Indexable)
                         .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                if (!documentsByPath.TryGetValue(key, out var document) ||
                    !rssCollections.Contains(ContentFieldReader.GetCollection(document)))
                {
                    continue;
                }

                postCandidates.Add((
                    I18nRootProjectionPath.CombineBaseUrl(result.BaseUrl, seo.Route.Url),
                    RssGenerator.ToPost(document, seo.Canonical, result.BodyStore)));
            }
        }

        var posts = postCandidates
            .OrderBy(candidate => candidate.RouteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Post)
            .ToArray();
        var formats = ParseFeedFormats(context.Config.Site.Feed.Formats);
        var limit = context.Config.Site.Feed.Limit > 0 ? context.Config.Site.Feed.Limit : 20;
        foreach (var format in formats)
        {
            switch (format)
            {
                case "rss":
                    RssGenerator.GenerateMerged(
                        context.OutputDir,
                        siteUrl,
                        context.RootBaseUrl,
                        context.Config.Site.Title,
                        posts,
                        limit,
                        context.Config.Site.Description);
                    break;
                case "atom":
                    AtomFeedGenerator.Generate(
                        context.OutputDir,
                        siteUrl,
                        context.RootBaseUrl,
                        context.Config.Site.Title,
                        posts,
                        $"{context.Config.Site.Feed.Path}/atom.xml",
                        limit,
                        context.Config.Site.Description);
                    break;
                case "json":
                    JsonFeedGenerator.Generate(
                        context.OutputDir,
                        siteUrl,
                        context.RootBaseUrl,
                        context.Config.Site.Title,
                        posts,
                        $"{context.Config.Site.Feed.Path}/feed.json",
                        limit,
                        context.Config.Site.Description);
                    break;
            }
        }
    }

    private static IReadOnlySet<string> ParseFeedFormats(IReadOnlyList<string> formats)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var format in formats)
        {
            var normalized = format.Trim().ToLowerInvariant();
            if (normalized is "rss" or "atom" or "json")
            {
                set.Add(normalized);
            }
        }

        if (set.Count == 0)
        {
            set.Add("rss");
        }

        return set;
    }

    private static HashSet<string> ResolveRssCollections(
        IReadOnlyDictionary<string, CollectionConfig>? collections)
    {
        if (collections is null || collections.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, config) in collections)
        {
            if (config.Output.Rss)
            {
                set.Add(key);
            }
        }

        return set;
    }
}
