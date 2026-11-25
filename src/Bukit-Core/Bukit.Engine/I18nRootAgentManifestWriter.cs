using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine;

internal sealed class I18nRootAgentManifestWriter : II18nRootProjectionWriter
{
    public IReadOnlyList<string> RepresentationKinds => ["agent-manifest"];

    public void Write(I18nRootProjectionWriterContext context, PublishRepresentation representation)
    {
        _ = representation;
        var entries = new List<DefaultContentProjectionWriter.AgentManifestEntry>();
        foreach (var result in context.Results)
        {
            var recordsById = (result.ContentGraph ?? CanonicalContentGraph.Empty).Records
                .GroupBy(x => x.Identity.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
            foreach (var routedDocument in result.RoutedDocuments.Concat(result.DerivedDocuments))
            {
                var document = routedDocument.Document;
                var route = routedDocument.Route;
                if (route.Template == RedirectPageHtml.RouteTemplate)
                {
                    continue;
                }

                var key = BuildPathUtils.NormalizeRelPath(route.OutputPath);
                result.SeoIndex.TryGetValue(key, out var seoEntry);
                if (seoEntry?.Indexable == false)
                {
                    continue;
                }

                if (!recordsById.TryGetValue(document.Id, out var records))
                {
                    records = [document.Record];
                }

                var record = records.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Presentation.Language,
                        result.Language,
                        StringComparison.OrdinalIgnoreCase)) ?? records[0];
                result.SeoModels.TryGetValue(key, out var model);
                var mergedRoute = I18nRootProjectionPath.CombineBaseUrl(result.BaseUrl, route.Url);
                var publicId = PublicContentProjectionPolicy.ResolvePublicId(record, mergedRoute);
                entries.Add(new DefaultContentProjectionWriter.AgentManifestEntry(
                    publicId,
                    publicId,
                    mergedRoute,
                    record.Presentation.Language,
                    record.Trust.ReviewStatus,
                    PublicContentProjectionPolicy.SanitizeEntities(record).Select(x => x.Name).ToArray(),
                    DefaultContentProjectionWriter.BuildAgentManifestRepresentationEntries(
                        route with { Url = mergedRoute }, seoEntry, model, result.BaseUrl),
                    record.Lifecycle.PublishedAt,
                    record.Lifecycle.UpdatedAt));
            }
        }

        new AgentManifestProjection().Project(context.OutputDir, entries, BuildLists(context));
    }

    /// <summary>
    /// Every language's list routes, prefixed with that language's base URL like the merged documents. A merged
    /// sitemap filters every language's canonicals under the root scope with one filter, so these lists do too;
    /// otherwise each language's own sitemap, and so each language's lists, uses its own base URL as the scope.
    /// </summary>
    private static AgentManifestListEntry[] BuildLists(I18nRootProjectionWriterContext context)
    {
        var mergedSitemap = string.Equals((context.Config.Site.SitemapMode ?? "split").Trim(), "merged", StringComparison.OrdinalIgnoreCase);
        var mergedFilter = mergedSitemap ? AgentManifestListEntries.CreateLocFilter(context.Config, context.RootBaseUrl) : null;
        return [.. context.Results.SelectMany(result => AgentManifestListEntries.Build(
            context.Config,
            result.SeoIndex,
            result.ListRouteGraph,
            result.RoutedDocuments.Concat(result.DerivedDocuments),
            result.OutputDir,
            result.BaseUrl,
            result.Language,
            prefixRoutes: true,
            mergedSitemap ? mergedFilter : AgentManifestListEntries.CreateLocFilter(context.Config, result.BaseUrl),
            context.Logger))];
    }

}
