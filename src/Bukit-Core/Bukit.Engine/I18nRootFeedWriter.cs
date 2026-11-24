using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Primitives;

namespace Bukit.Engine;

internal sealed class I18nRootFeedWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["feed", "atom", "jsonfeed"];

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        var siteUrl = context.Config.Site.Url;
        if (string.IsNullOrWhiteSpace(siteUrl) ||
            SiteModeResolver.ResolveFeedMode(context.Config.Site) != "merged")
        {
            return;
        }

        var format = representation.Kind switch
        {
            "feed" => "rss",
            "atom" => "atom",
            "jsonfeed" => "json",
            _ => string.Empty
        };
        if (ParseFeedFormats(context.Config.Site.Feed.Formats).Contains(format))
        {
            GenerateMergedFeeds(context, siteUrl, format);
        }
    }

    private static void GenerateMergedFeeds(I18nRootProjectionWriterContext context, string siteUrl, string format)
    {
        var postCandidates = new List<(string RouteUrl, RssGenerator.Post Post)>();
        var rssCollections = ResolveRssCollections(context.Config.Site.Collections);
        var tieBreak = ContentOrdering.Resolve(context.Config);
        foreach (var result in context.Results)
        {
            var documentsByPath = SearchIndexDocuments.BuildDocumentMap(result.RoutedDocuments);
            foreach (var (key, seo) in result.SeoIndex
                         .Where(x => x.Value.Indexable)
                         .OrderBy(x => x.Value.Route.Url, StringComparer.OrdinalIgnoreCase))
            {
                if (!documentsByPath.TryGetValue(key, out var document) ||
                    !rssCollections.Contains(ContentFieldReader.GetCollection(document)))
                {
                    continue;
                }

                var post = RssGenerator.ToPost(document, seo.Canonical, result.BodyStore, tieBreak, seo.Route.Url);
                if (format == "rss")
                {
                    // An undeclared page belongs to the variant that built it, so the merged RSS channel
                    // sees every language it mixes and omits <language> instead of naming one of them.
                    post = post with { Language = ContentLanguageFallback.Resolve(post.Language, result.Language) };
                }

                postCandidates.Add((I18nRootProjectionPath.CombineBaseUrl(result.BaseUrl, seo.Route.Url), post));
            }
        }

        var posts = postCandidates
            .OrderBy(candidate => candidate.RouteUrl, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Post)
            .ToArray();
        var limit = context.Config.Site.Feed.Limit > 0 ? context.Config.Site.Feed.Limit : 20;
        switch (format)
        {
            case "rss":
                // Item HTML gets absolute root-relative URLs, as in the per-language feeds; only the
                // posts the feed keeps are rewritten.
                RssGenerator.GenerateMerged(
                    context.OutputDir,
                    siteUrl,
                    context.RootBaseUrl,
                    context.Config.Site.Title,
                    FeedHtmlUrls.AbsolutizeContent(RssGenerator.SelectWindow(posts, limit), siteUrl),
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
