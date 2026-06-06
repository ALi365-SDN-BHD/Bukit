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
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> Routed,
    IReadOnlyList<(ContentItem Item, RouteInfo Route)> DerivedRouted,
    IReadOnlyDictionary<string, SeoIndexEntry> SeoIndex,
    IReadOnlyDictionary<string, SeoModel> SeoModels,
    IContentBodyStore? BodyStore = null,
    string BaseUrl = "/",
    bool SearchSnippetsEnabled = false,
    ILogger? Logger = null,
    BuildContext? PluginContext = null,
    IReadOnlyList<BuildVariantResult>? VariantResults = null);

internal sealed record PublishProjectionResult(
    PublishRepresentation Representation,
    IReadOnlyList<PublishRepresentationOutput> Outputs);

internal sealed record PublishRepresentationOutput(
    string Kind,
    string Url,
    string Path,
    bool Exists,
    bool Indexable);

internal sealed record PublishRepresentation(
    string Kind,
    string Path,
    bool IsAggregate);

internal static class PublishRepresentationRegistry
{
    internal static readonly PublishRepresentation Html = new("html", "", IsAggregate: false);
    internal static readonly PublishRepresentation SemanticHtml = new("semantic-html", "", IsAggregate: false);
    internal static readonly PublishRepresentation Json = new("json", "content/*.json", IsAggregate: false);
    internal static readonly PublishRepresentation Markdown = new("markdown", "content/*.md", IsAggregate: false);
    internal static readonly PublishRepresentation JsonLd = new("jsonld", "", IsAggregate: false);

    private static readonly PublishRepresentation[] DocumentRepresentations =
    [
        Html,
        SemanticHtml,
        Json,
        Markdown
    ];

    private static readonly PublishRepresentation[] AggregateOutputRepresentations =
    [
        new("feed", "rss.xml", IsAggregate: true),
        new("atom", "feed/atom.xml", IsAggregate: true),
        new("jsonfeed", "feed/feed.json", IsAggregate: true),
        new("sitemap", "sitemap.xml", IsAggregate: true),
        new("search", "search.json", IsAggregate: true),
        new("llms", "llms.txt", IsAggregate: true),
        new("llms-full", "llms-full.txt", IsAggregate: true),
        new("robots", "robots.txt", IsAggregate: true),
        new("agent-manifest", "agent-manifest.json", IsAggregate: true)
    ];

    internal static IReadOnlyList<string> DocumentKinds(bool includeJsonLd = false)
    {
        var kinds = DocumentRepresentations.Select(x => x.Kind);
        if (includeJsonLd)
        {
            kinds = kinds.Append(JsonLd.Kind);
        }

        return kinds.ToArray();
    }

    internal static IReadOnlyList<PublishRepresentation> DocumentRepresentationsFor(bool includeJsonLd = false)
    {
        var values = DocumentRepresentations.AsEnumerable();
        if (includeJsonLd)
        {
            values = values.Append(JsonLd);
        }

        return values.ToArray();
    }

    internal static IReadOnlyList<PublishRepresentation> AggregateRepresentations()
        => AggregateOutputRepresentations;

    internal static IReadOnlyList<IPublishProjection> AggregateProjectionAdapters()
        => AggregateOutputRepresentations
            .Select(x => new ExistingAggregatePublishProjection(x))
            .ToArray();

    internal static IReadOnlyList<IPublishProjection> RootAggregateProjectionAdapters()
        => AggregateOutputRepresentations
            .Select(x => new I18nRootAggregatePublishProjection(x))
            .ToArray();

    internal static IReadOnlyList<string> ExpectedAggregateKinds(PublishRepresentationExpectation expectation)
        => AggregateOutputRepresentations
            .Where(x => x.Kind switch
            {
                "feed" => expectation.Feed,
                "atom" => expectation.Atom,
                "jsonfeed" => expectation.JsonFeed,
                "sitemap" => expectation.Sitemap,
                "search" => expectation.Search,
                "llms" => expectation.Llms,
                "llms-full" => expectation.LlmsFull,
                "robots" => expectation.Robots,
                "agent-manifest" => expectation.AgentManifest,
                _ => false
            })
            .Select(x => x.Kind)
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

internal sealed record PublishRepresentationExpectation(
    bool Feed = false,
    bool Atom = false,
    bool JsonFeed = false,
    bool Sitemap = false,
    bool Search = false,
    bool Llms = false,
    bool LlmsFull = false,
    bool Robots = false,
    bool AgentManifest = false);

internal sealed class ExistingAggregatePublishProjection : IPublishProjection
{
    internal ExistingAggregatePublishProjection(PublishRepresentation representation)
    {
        Representation = representation;
    }

    public PublishRepresentation Representation { get; }

    public PublishProjectionResult Project(PublishProjectionContext context)
    {
        Generate(context);
        var path = Path.Combine(context.OutputDir, Representation.Path);
        var text = File.Exists(path) ? File.ReadAllText(path) : null;
        var outputs = BuildRouteOutputs(context, text, File.Exists(path));
        if (outputs.Count > 0)
        {
            return new PublishProjectionResult(Representation, outputs);
        }

        return new PublishProjectionResult(
            Representation,
            [new PublishRepresentationOutput(Representation.Kind, "/" + Representation.Path.Replace('\\', '/'), Representation.Path, File.Exists(path), Indexable: false)]);
    }

    private void Generate(PublishProjectionContext context)
    {
        switch (Representation.Kind)
        {
            case "feed":
                GenerateRss(context);
                break;
            case "atom":
                GenerateAtom(context);
                break;
            case "jsonfeed":
                GenerateJsonFeed(context);
                break;
            case "sitemap":
                GenerateSitemap(context);
                break;
            case "search":
                GenerateSearch(context);
                break;
            case "llms":
                GenerateLlms(context);
                break;
            case "llms-full":
                GenerateLlmsFull(context);
                break;
            case "robots":
                GenerateRobots(context);
                break;
        }
    }

    private static void GenerateRss(PublishProjectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Config.Site.Url))
        {
            return;
        }

        if (!context.Config.Site.Feed.Formats.Any(x => string.Equals(x, "rss", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var posts = RssGenerator.CollectAllPosts(
            context.Config.Site.Collections,
            context.Routed,
            context.BodyStore ?? NullContentBodyStore.Instance,
            context.ContentGraph,
            context.SeoIndex,
            context.Config.Site.Url,
            context.BaseUrl);
        var limit = context.Config.Site.Feed.Limit > 0 ? context.Config.Site.Feed.Limit : 20;
        RssGenerator.GenerateMerged(context.OutputDir, context.Config.Site.Url, context.BaseUrl, context.Config.Site.Title, posts, limit, context.Config.Site.Description);
    }

    private static void GenerateAtom(PublishProjectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Config.Site.Url) ||
            !context.Config.Site.Feed.Formats.Any(x => string.Equals(x, "atom", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var posts = RssGenerator.CollectAllPosts(context.Config.Site.Collections, context.Routed, context.BodyStore ?? NullContentBodyStore.Instance, context.ContentGraph, context.SeoIndex, context.Config.Site.Url, context.BaseUrl);
        var limit = context.Config.Site.Feed.Limit > 0 ? context.Config.Site.Feed.Limit : 20;
        AtomFeedGenerator.Generate(context.OutputDir, context.Config.Site.Url, context.BaseUrl, context.Config.Site.Title, posts, $"{context.Config.Site.Feed.Path}/atom.xml", limit, context.Config.Site.Description);
    }

    private static void GenerateJsonFeed(PublishProjectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Config.Site.Url) ||
            !context.Config.Site.Feed.Formats.Any(x => string.Equals(x, "json", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var posts = RssGenerator.CollectAllPosts(context.Config.Site.Collections, context.Routed, context.BodyStore ?? NullContentBodyStore.Instance, context.ContentGraph, context.SeoIndex, context.Config.Site.Url, context.BaseUrl);
        var limit = context.Config.Site.Feed.Limit > 0 ? context.Config.Site.Feed.Limit : 20;
        JsonFeedGenerator.Generate(context.OutputDir, context.Config.Site.Url, context.BaseUrl, context.Config.Site.Title, posts, $"{context.Config.Site.Feed.Path}/feed.json", limit, context.Config.Site.Description);
    }

    private static void GenerateSitemap(PublishProjectionContext context)
    {
        if (string.IsNullOrWhiteSpace(context.Config.Site.Url))
        {
            return;
        }

        var logger = context.Logger ?? new ConsoleLogger(LogLevel.Error);
        var filtered = new List<(string AbsoluteUrl, DateTimeOffset LastModified)>();
        foreach (var seo in context.SeoIndex.Values.OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
            if (!seo.Indexable)
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

    private static void GenerateSearch(PublishProjectionContext context)
    {
        SearchIndexBuilder.GenerateSingleSearchIndex(
            context.OutputDir,
            context.BaseUrl,
            context.Config.Site.SearchIncludeDerived,
            context.SearchSnippetsEnabled,
            context.Routed,
            context.DerivedRouted,
            context.SeoIndex,
            context.BodyStore ?? NullContentBodyStore.Instance);
        SearchIndexPlugin.WriteSearchUi(BuildPluginContext(context));
    }

    private static void GenerateLlms(PublishProjectionContext context)
    {
        if (!context.Config.Site.Seo.Geo.Enabled || !context.Config.Site.Seo.Geo.LlmsTxt)
        {
            return;
        }

        LlmsTxtPlugin.WriteLlmsTxt(BuildPluginContext(context), context.Config.Site.Seo.Geo);
    }

    private static void GenerateLlmsFull(PublishProjectionContext context)
    {
        if (!context.Config.Site.Seo.Geo.Enabled || !context.Config.Site.Seo.Geo.LlmsFullTxt)
        {
            return;
        }

        LlmsTxtPlugin.WriteLlmsFullTxt(BuildPluginContext(context));
    }

    private static void GenerateRobots(PublishProjectionContext context)
    {
        RobotsTxtWriter.WriteIfRequested(context.Config, context.OutputDir, context.BaseUrl, context.SeoIndex);
    }

    private static BuildContext BuildPluginContext(PublishProjectionContext context)
    {
        if (context.PluginContext is not null)
        {
            return context.PluginContext;
        }

        var buildContext = new BuildContext
        {
            Config = context.Config,
            RootDir = Directory.GetCurrentDirectory(),
            OutputDir = context.OutputDir,
            BaseUrl = context.BaseUrl,
            LayoutsDir = string.Empty,
            Routed = context.Routed,
            ContentGraph = context.ContentGraph,
            BodyStore = context.BodyStore ?? NullContentBodyStore.Instance,
            SeoIndex = context.SeoIndex,
            Logger = context.Logger ?? new ConsoleLogger(LogLevel.Error)
        };
        buildContext.DerivedRouted.AddRange(context.DerivedRouted);
        buildContext.Data["__seo_models"] = context.SeoModels;
        return buildContext;
    }

    private IReadOnlyList<PublishRepresentationOutput> BuildRouteOutputs(
        PublishProjectionContext context,
        string? text,
        bool fileExists)
    {
        var outputs = new List<PublishRepresentationOutput>();
        foreach (var (item, route) in context.Routed.Concat(context.DerivedRouted)
                     .OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase))
        {
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
                Representation.Path.Replace('\\', '/'),
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
