using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Plugins.BuiltIn;
using Bukit.Engine.Primitives;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// The list, taxonomy and home routes of one language for the agent manifest's <c>lists</c> array. A route is
/// listed only when the sitemap would list it, so agents see the same list pages search engines do.
/// </summary>
internal static class AgentManifestListEntries
{
    /// <summary>
    /// The sitemap's canonical filter for <paramref name="scopeBaseUrl"/>, or <see langword="null"/> without
    /// <c>site.url</c>: no sitemap is written then, so no list route is either.
    /// </summary>
    internal static SitemapLocFilter? CreateLocFilter(AppConfig config, string scopeBaseUrl)
        => string.IsNullOrWhiteSpace(config.Site.Url) ? null : new SitemapLocFilter(config.Site.Url, scopeBaseUrl);

    /// <summary>
    /// Replays the sitemap's checks over <paramref name="seoIndex"/> in sitemap order and keeps the list routes it
    /// accepts. Document canonicals go through <paramref name="locFilter"/> too, so a list page whose canonical a
    /// document already took is left out, as in the sitemap.
    /// </summary>
    /// <param name="sharedListCanonicalKeys">
    /// Every language's <see cref="ListCanonicalKeys"/> when <paramref name="locFilter"/> is shared across
    /// languages (a merged sitemap): a document can then take another language's list canonical. Null uses
    /// this language's list canonicals.
    /// </param>
    /// <param name="prefixRoutes">
    /// <see langword="true"/> for the root merged manifest, whose routes carry the language base URL; otherwise
    /// routes stay relative to <paramref name="baseUrl"/>, the same as the manifest's documents.
    /// </param>
    internal static IReadOnlyList<AgentManifestListEntry> Build(
        AppConfig config,
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        ListRouteGraph? listRouteGraph,
        IEnumerable<RoutedContentDocument> documents,
        string outputDir,
        string baseUrl,
        string? language,
        bool prefixRoutes,
        SitemapLocFilter? locFilter,
        ILogger? logger,
        IReadOnlySet<string>? sharedListCanonicalKeys = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(seoIndex);
        ArgumentNullException.ThrowIfNull(documents);
        if (locFilter is null)
        {
            return [];
        }

        IReadOnlyCollection<RoutedContentDocument> documentList = documents as IReadOnlyCollection<RoutedContentDocument> ?? [.. documents];
        var documentPaths = DocumentPaths(documentList);
        var documentExclusions = SitemapPlugin.BuildDocumentSitemapExclusions(config, documentList);
        var excludedListPaths = ListRouteSitemapPolicy.BuildExcludedOutputPaths(config, listRouteGraph);
        SeoIndexEntry[] ordered = [.. seoIndex.Values.OrderBy(x => x.Route.Url, StringComparer.OrdinalIgnoreCase)];
        // Only a document that shares a list canonical can change which list routes are kept, so only those
        // documents pay for reading their rendered page.
        var listCanonicalKeys = sharedListCanonicalKeys ?? ListCanonicalKeys(ordered, documentPaths).ToHashSet(StringComparer.Ordinal);
        var sitemapLogger = logger ?? new ConsoleLogger(LogLevel.Error);
        var resolvedLanguage = ContentLanguageFallback.Resolve(language, ContentLanguageFallback.Undetermined)!;
        var entries = new List<AgentManifestListEntry>();
        foreach (var seo in ordered)
        {
            var outputPath = BuildPathUtils.NormalizeRelPath(seo.Route.OutputPath);
            if (!seo.Indexable || excludedListPaths.Contains(outputPath) || documentExclusions.Contains(outputPath))
            {
                continue;
            }

            var kind = ResolveKind(seo, documentPaths);
            var readsPage = kind is not null ||
                            (!string.IsNullOrWhiteSpace(seo.Canonical) && listCanonicalKeys.Contains(SitemapLocFilter.CanonicalKey(seo.Canonical)));
            // Without an output directory there is no rendered page to read meta robots from. The sitemap pass
            // already logged canonical decisions, so this replay does not log them again.
            if ((readsPage && !string.IsNullOrWhiteSpace(outputDir) &&
                 SitemapPolicy.ShouldExcludeFromSitemapFile(Path.Combine(outputDir, seo.Route.OutputPath), sitemapLogger)) ||
                !locFilter.TryAccept(seo.Route.Url, seo.Canonical, logger: null) ||
                kind is null)
            {
                continue;
            }

            var publicUrl = I18nRootProjectionPath.CombineBaseUrl(baseUrl, seo.Route.Url);
            entries.Add(new AgentManifestListEntry(
                prefixRoutes ? publicUrl : seo.Route.Url,
                resolvedLanguage,
                kind,
                seo.LastModified,
                [new DefaultContentProjectionWriter.RepresentationEntry("html", publicUrl)]));
        }

        return entries;
    }

    /// <summary>The canonical keys of the list routes in <paramref name="seoIndex"/>.</summary>
    internal static IEnumerable<string> ListCanonicalKeys(
        IReadOnlyDictionary<string, SeoIndexEntry> seoIndex,
        IEnumerable<RoutedContentDocument> documents)
        => ListCanonicalKeys(seoIndex.Values, DocumentPaths(documents));

    private static IEnumerable<string> ListCanonicalKeys(IEnumerable<SeoIndexEntry> entries, HashSet<string> documentPaths)
        => entries
            .Where(seo => ResolveKind(seo, documentPaths) is not null && !string.IsNullOrWhiteSpace(seo.Canonical))
            .Select(seo => SitemapLocFilter.CanonicalKey(seo.Canonical));

    // A list route that shares its output path with a content record is already a document row.
    private static HashSet<string> DocumentPaths(IEnumerable<RoutedContentDocument> documents)
        => documents
            .Select(document => BuildPathUtils.NormalizeRelPath(document.Route.OutputPath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string? ResolveKind(SeoIndexEntry entry, HashSet<string> documentPaths)
    {
        if (documentPaths.Contains(BuildPathUtils.NormalizeRelPath(entry.Route.OutputPath)))
        {
            return null;
        }

        return entry.ContentType switch
        {
            "taxonomy" => "taxonomy",
            "list" => entry.Route.Url == "/" ? "home" : "list",
            _ => null
        };
    }
}

internal sealed record AgentManifestListEntry(
    string Route,
    string Language,
    string Kind,
    DateTimeOffset LastModified,
    IReadOnlyList<DefaultContentProjectionWriter.RepresentationEntry> Representations);
