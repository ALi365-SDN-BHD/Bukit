using Bukit.Config;
using Bukit.Engine.Abstractions.Content;
using Bukit.Shared;

namespace Bukit.Engine.Stages;

internal sealed class CollectionWarningStage : IContentStage
{
    public string Name => "CollectionWarning";

    public Task<ContentStageOutput> ExecuteAsync(ContentStageInput input, CancellationToken cancellationToken)
    {
        var warned = WarnFilteredLists(input.Config, input.Logger);

        foreach (var document in input.Documents)
        {
            var collection = ContentFieldReader.GetCollection(document);
            var hasCollection = !string.IsNullOrWhiteSpace(collection);
            var explicitType = ContentFieldReader.GetText(document.CustomFields, "type");

            if (hasCollection)
            {
                if (!string.IsNullOrWhiteSpace(explicitType))
                {
                    input.Logger.Warn(
                    $"[WARN] Content \"{document.Id}\" defines both type={explicitType} and collection={collection}. " +
                    "Collection routing uses collection; type remains content metadata.");
                    warned++;
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(explicitType))
            {
                input.Logger.Warn(
                    $"[WARN] Content \"{document.Id}\" uses type={explicitType} without collection. " +
                    "Routing must be provided by site.collections, site.permalinks, or route front matter.");
                warned++;
            }
        }

        return Task.FromResult(new ContentStageOutput(input.Documents, input.BodyStore, Name, warned, null));
    }

    private static int WarnFilteredLists(AppConfig config, ILogger logger)
    {
        if (config.Site.Collections is not { Count: > 0 } collections)
        {
            return 0;
        }

        var warned = 0;
        foreach (var (collectionName, collection) in collections)
        {
            if (collection.FilteredLists is not { Count: > 0 } filteredLists)
            {
                continue;
            }

            for (var i = 0; i < filteredLists.Count; i++)
            {
                var filter = filteredLists[i];
                var prefix = $"site.collections.{collectionName}.filteredLists[{i}]";
                var values = filter.Values is { Count: > 0 }
                    ? string.Join(", ", filter.Values)
                    : filter.Value ?? string.Empty;
                var routeDescription = $"manual static filtered list route for field={filter.Field} operator={filter.Operator} value={values} at {filter.ListRoute}";
                if (string.IsNullOrWhiteSpace(collection.ListRoute))
                {
                    logger.Warn(
                        $"[WARN] {prefix} configures a {routeDescription}, but site.collections.{collectionName}.listRoute is missing; " +
                        "the filtered list route will not be generated. Add listRoute to enable filteredLists, or use taxonomy.kinds " +
                        "for automatically generated tag/category/term routes.");
                    warned++;
                    continue;
                }

                logger.Warn(
                    $"[WARN] {prefix} creates a {routeDescription}. " +
                    "Use taxonomy.kinds for automatically generated tag/category/term routes.");
                warned++;
            }
        }

        return warned;
    }
}
