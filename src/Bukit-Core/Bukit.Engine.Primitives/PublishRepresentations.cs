namespace Bukit.Engine;

internal sealed record PublishProjectionResult(
    PublishRepresentation Representation,
    IReadOnlyList<PublishRepresentationOutput> Outputs);

internal sealed record PublishRepresentationOutput(
    string Kind,
    string Url,
    string Path,
    bool Exists,
    bool Indexable,
    string? DocumentRoute = null);

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
