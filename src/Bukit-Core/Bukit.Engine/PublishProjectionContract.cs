using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Rendering;
using Bukit.Shared;

namespace Bukit.Engine;

internal interface IPublishProjection
{
    PublishRepresentation Representation { get; }

    PublishProjectionResult Project(PublishProjectionContext context);
}

internal sealed record PublishProjectionContext(
    AppConfig Config,
    string OutputDir,
    CanonicalContentGraph ContentGraph,
    IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex,
    IReadOnlyDictionary<string, SeoModel> SeoModels,
    IReadOnlyList<RoutedContentDocument> RoutedDocuments,
    IContentBodyStore? BodyStore = null,
    string BaseUrl = "/",
    bool SearchSnippetsEnabled = false,
    ILogger? Logger = null,
    ListRouteGraph? ListRouteGraph = null,
    IReadOnlyList<BuildVariantResult>? VariantResults = null,
    IReadOnlyList<RoutedContentDocument>? DerivedDocuments = null)
{
    public IReadOnlyList<RoutedContentDocument> DerivedDocuments { get; init; } = DerivedDocuments ?? Array.Empty<RoutedContentDocument>();
}

/// <summary>Creates the projection adapters for the representations in <see cref="PublishRepresentationRegistry"/>.</summary>
internal static class PublishProjectionAdapters
{
    internal static IReadOnlyList<IPublishProjection> Aggregate()
        =>
        [
            new RssFeedPublishProjection(),
            new AtomFeedPublishProjection(),
            new JsonFeedPublishProjection(),
            new SitemapPublishProjection(),
            new SearchIndexPublishProjection(),
            new LlmsTxtPublishProjection(),
            new LlmsFullTxtPublishProjection(),
            new RobotsTxtPublishProjection(),
            new AgentManifestAggregateInventoryProjection()
        ];

    internal static IReadOnlyList<IPublishProjection> RootAggregate()
        => PublishRepresentationRegistry.AggregateRepresentations()
            .Select(x => new I18nRootAggregatePublishProjection(x))
            .ToArray();
}

internal sealed class I18nRootAggregatePublishProjection : IPublishProjection
{
    internal I18nRootAggregatePublishProjection(PublishRepresentation representation)
    {
        Representation = representation;
    }

    public PublishRepresentation Representation { get; }

    public PublishProjectionResult Project(PublishProjectionContext context)
        => I18nOutputMerger.ProjectRootAggregate(context, Representation);
}

internal abstract class AggregatePublishProjectionBase : IPublishProjection
{
    protected AggregatePublishProjectionBase(PublishRepresentation representation)
    {
        Representation = representation;
    }

    public PublishRepresentation Representation { get; }

    public PublishProjectionResult Project(PublishProjectionContext context)
    {
        ProjectAggregate(context);
        var relativePath = Representation.Kind switch
        {
            "atom" => context.Config.Site.Feed.Path + "/atom.xml",
            "jsonfeed" => context.Config.Site.Feed.Path + "/feed.json",
            _ => Representation.Path
        };
        var path = FileWriter.GetSafeFullPath(context.OutputDir, relativePath);
        var text = File.Exists(path) ? File.ReadAllText(path) : null;
        var outputs = BuildRouteOutputs(context, text, File.Exists(path), relativePath);
        if (outputs.Count > 0)
        {
            return new PublishProjectionResult(Representation, outputs);
        }

        return new PublishProjectionResult(
            Representation,
            [new PublishRepresentationOutput(Representation.Kind, "/" + relativePath.Replace('\\', '/'), relativePath, File.Exists(path), Indexable: false)]);
    }

    protected abstract void ProjectAggregate(PublishProjectionContext context);

    private IReadOnlyList<PublishRepresentationOutput> BuildRouteOutputs(
        PublishProjectionContext context,
        string? text,
        bool fileExists,
        string relativePath)
    {
        var outputs = new List<PublishRepresentationOutput>();
        var sitemapLocKeys = Representation.Kind.Equals("sitemap", StringComparison.OrdinalIgnoreCase)
            ? SitemapCanonicalIdentity.ReadLocKeys(text)
            : null;
        var routes = context.RoutedDocuments.Concat(context.DerivedDocuments)
            .OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase)
            .Select(routedDocument =>
            {
                var route = routedDocument.Route;
                context.SeoIndex.TryGetValue(BuildPathUtils.NormalizeRelPath(route.OutputPath), out var entry);
                return (Route: route, Entry: entry);
            })
            .ToList();
        var presentPatterns = FindPresentPatterns(text, routes);
        foreach (var (route, entry) in routes)
        {
            var indexable = entry?.Indexable != false;
            var routePresent = IsRoutePresent(presentPatterns, route, entry) ||
                               (sitemapLocKeys is not null && SitemapCanonicalIdentity.Contains(sitemapLocKeys, entry?.Canonical));
            var exists = Representation.Kind.Equals("robots", StringComparison.OrdinalIgnoreCase)
                ? fileExists
                : fileExists && indexable && routePresent;
            outputs.Add(new PublishRepresentationOutput(
                Representation.Kind,
                route.Url,
                relativePath.Replace('\\', '/'),
                exists,
                indexable));
        }

        return outputs;
    }

    // Scans the aggregate file once for every route URL and canonical, instead of once per route.
    private static HashSet<string> FindPresentPatterns(string? text, List<(RouteInfo Route, SeoIndexEntry? Entry)> routes)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var patterns = new List<string>(routes.Count * 2);
        foreach (var (route, entry) in routes)
        {
            if (!string.IsNullOrWhiteSpace(route.Url))
            {
                patterns.Add(route.Url);
            }

            if (!string.IsNullOrWhiteSpace(entry?.Canonical))
            {
                patterns.Add(entry.Canonical);
            }
        }

        return OrdinalIgnoreCaseMultiMatcher.FindPresent(text, patterns);
    }

    private static bool IsRoutePresent(HashSet<string> presentPatterns, RouteInfo route, SeoIndexEntry? entry)
        => (!string.IsNullOrWhiteSpace(route.Url) && presentPatterns.Contains(route.Url)) ||
           (!string.IsNullOrWhiteSpace(entry?.Canonical) && presentPatterns.Contains(entry.Canonical));
}

internal sealed class RssFeedPublishProjection : AggregatePublishProjectionBase
{
    internal RssFeedPublishProjection()
        : base(new PublishRepresentation("feed", "rss.xml", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
        RssProjectionWriter.WriteRss(context);
    }
}

internal sealed class AtomFeedPublishProjection : AggregatePublishProjectionBase
{
    internal AtomFeedPublishProjection()
        : base(new PublishRepresentation("atom", "feed/atom.xml", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
        RssProjectionWriter.WriteAtom(context);
    }
}

internal sealed class JsonFeedPublishProjection : AggregatePublishProjectionBase
{
    internal JsonFeedPublishProjection()
        : base(new PublishRepresentation("jsonfeed", "feed/feed.json", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
        RssProjectionWriter.WriteJsonFeed(context);
    }
}

internal sealed class SitemapPublishProjection : AggregatePublishProjectionBase
{
    internal SitemapPublishProjection()
        : base(new PublishRepresentation("sitemap", "sitemap.xml", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Config.Site.Url))
        {
            return;
        }

        var logger = context.Logger ?? new ConsoleLogger(LogLevel.Error);
        var filtered = new List<(string AbsoluteUrl, DateTimeOffset? LastModified)>();
        var locFilter = new SitemapLocFilter(context.Config.Site.Url, context.BaseUrl);
        var documentExclusions = SitemapPlugin.BuildDocumentSitemapExclusions(
            context.Config,
            context.RoutedDocuments.Concat(context.DerivedDocuments));
        foreach (var seo in context.SeoIndex.Values.OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!seo.Indexable)
            {
                continue;
            }

            if (ListRouteSitemapPolicy.IsExcluded(context.Config, context.ListRouteGraph, seo))
            {
                continue;
            }

            if (documentExclusions.Contains(BuildPathUtils.NormalizeRelPath(seo.Route.OutputPath)))
            {
                continue;
            }

            if (SitemapPolicy.ShouldExcludeFromSitemapFile(Path.Combine(context.OutputDir, seo.Route.OutputPath), logger))
            {
                continue;
            }

            if (!locFilter.TryAccept(seo.Route.Url, seo.Canonical, logger))
            {
                continue;
            }

            filtered.Add((seo.Canonical, seo.LastModified));
        }

        SitemapGenerator.GenerateAbsolute(context.OutputDir, filtered);
    }
}

internal sealed class SearchIndexPublishProjection : AggregatePublishProjectionBase
{
    internal SearchIndexPublishProjection()
        : base(new PublishRepresentation("search", "search.json", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
        SearchProjectionWriter.WriteSearchIndex(context);
    }
}

internal sealed class LlmsTxtPublishProjection : AggregatePublishProjectionBase
{
    internal LlmsTxtPublishProjection()
        : base(new PublishRepresentation("llms", "llms.txt", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
        LlmsAggregateProjectionWriter.WriteLlmsTxt(context);
    }
}

internal sealed class LlmsFullTxtPublishProjection : AggregatePublishProjectionBase
{
    internal LlmsFullTxtPublishProjection()
        : base(new PublishRepresentation("llms-full", "llms-full.txt", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
        LlmsAggregateProjectionWriter.WriteLlmsFullTxt(context);
    }
}

internal sealed class RobotsTxtPublishProjection : AggregatePublishProjectionBase
{
    internal RobotsTxtPublishProjection()
        : base(new PublishRepresentation("robots", "robots.txt", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
        => RobotsAggregateProjectionWriter.WriteRobotsTxt(context);
}

internal sealed class AgentManifestAggregateInventoryProjection : AggregatePublishProjectionBase
{
    internal AgentManifestAggregateInventoryProjection()
        : base(new PublishRepresentation("agent-manifest", "agent-manifest.json", IsAggregate: true))
    {
    }

    protected override void ProjectAggregate(PublishProjectionContext context)
    {
    }
}
