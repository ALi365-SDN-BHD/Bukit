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
        foreach (var routedDocument in context.RoutedDocuments.Concat(context.DerivedDocuments)
                     .OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            var route = routedDocument.Route;
            var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
            context.SeoIndex.TryGetValue(key, out var entry);
            var indexable = entry?.Indexable != false;
            var routePresent = IsRoutePresent(text, route, entry);
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

    private bool IsRoutePresent(string? text, RouteInfo route, SeoIndexEntry? entry)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (ContainsInvariant(text, route.Url))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(entry?.Canonical) && ContainsInvariant(text, entry.Canonical);
    }

    private static bool ContainsInvariant(string? haystack, string needle)
        => !string.IsNullOrWhiteSpace(haystack) &&
           !string.IsNullOrWhiteSpace(needle) &&
           haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
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
