using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Plugins.BuiltIn;

namespace Bukit.Engine;

internal sealed class I18nRootAgentManifestWriter : II18nRootProjectionWriter
{
    public string Name => "agent-manifest";

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
                    PrefixRepresentationUrls(
                        result.BaseUrl,
                        DefaultContentProjectionWriter.BuildAgentManifestRepresentationEntries(
                            record,
                            mergedRoute,
                            seoEntry,
                            model)),
                    record.Lifecycle.UpdatedAt ?? record.Lifecycle.PublishedAt));
            }
        }

        new AgentManifestProjection().Project(context.OutputDir, entries);
    }

    private static IReadOnlyList<DefaultContentProjectionWriter.RepresentationEntry> PrefixRepresentationUrls(
        string baseUrl,
        IReadOnlyList<DefaultContentProjectionWriter.RepresentationEntry> representations)
    {
        return representations.Select(x => x.Kind switch
        {
            "json" or "markdown" => x with
            {
                Url = I18nRootProjectionPath.CombineBaseUrl(baseUrl, x.Url)
            },
            _ => x
        }).ToArray();
    }
}
