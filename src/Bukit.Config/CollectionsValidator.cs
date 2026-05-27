using Bukit.Shared;

namespace Bukit.Config;

internal static class CollectionsValidator
{
    internal static void ValidateCollections(IReadOnlyDictionary<string, CollectionConfig> collections)
    {
        foreach (var (name, collection) in collections)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ConfigException("site.collections keys must be non-empty strings.");
            }

            if (string.IsNullOrWhiteSpace(collection.Permalink))
            {
                throw new ConfigException($"site.collections.{name}.permalink is required.");
            }

            if (!collection.Permalink.Contains("{slug}", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException($"site.collections.{name}.permalink must include {{slug}}.");
            }

            if (string.IsNullOrWhiteSpace(collection.Template))
            {
                throw new ConfigException($"site.collections.{name}.template is required.");
            }

            if (collection.Pagination.PageSize <= 0)
            {
                throw new ConfigException($"site.collections.{name}.pagination.pageSize must be a positive integer.");
            }

            if (!string.IsNullOrWhiteSpace(collection.ListRoute))
            {
                if (!collection.ListRoute.StartsWith('/'))
                {
                    throw new ConfigException($"site.collections.{name}.listRoute must start with '/'.");
                }
            }

            if (collection.FilteredLists is { Count: > 0 } filtered)
            {
                ValidateFilteredLists(name, filtered);
            }

            if (!string.IsNullOrWhiteSpace(collection.SchemaFailMode))
            {
                var mode = collection.SchemaFailMode!.Trim().ToLowerInvariant();
                if (mode is not ("off" or "warn" or "strict"))
                {
                    throw new ConfigException($"site.collections.{name}.schemaFailMode must be off|warn|strict.");
                }
            }
        }
    }

    internal static void ValidateFilteredLists(string collectionName, IReadOnlyList<FilteredListConfig> filteredLists)
    {
        var usedRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < filteredLists.Count; i++)
        {
            var filter = filteredLists[i];
            var prefix = $"site.collections.{collectionName}.filteredLists[{i}]";

            if (string.IsNullOrWhiteSpace(filter.Field))
            {
                throw new ConfigException($"{prefix}.field is required.");
            }

            if (string.IsNullOrWhiteSpace(filter.Value))
            {
                throw new ConfigException($"{prefix}.value is required.");
            }

            if (string.IsNullOrWhiteSpace(filter.ListRoute))
            {
                throw new ConfigException($"{prefix}.listRoute is required.");
            }

            if (!filter.ListRoute.StartsWith('/'))
            {
                throw new ConfigException($"{prefix}.listRoute must start with '/'.");
            }

            if (!usedRoutes.Add(filter.ListRoute.Trim().ToLowerInvariant()))
            {
                throw new ConfigException($"{prefix}.listRoute '{filter.ListRoute}' duplicates another filtered list route.");
            }
        }
    }

    internal static void ValidateSourcesToCollections(
        IReadOnlyList<ContentSourceConfig> sources,
        IReadOnlyDictionary<string, CollectionConfig> collections)
    {
        var collectionKeys = new HashSet<string>(collections.Keys, StringComparer.OrdinalIgnoreCase);
        var contentSources = new List<ContentSourceConfig>();
        foreach (var source in sources)
        {
            if ((source.Mode ?? "content").Trim().Equals("content", StringComparison.OrdinalIgnoreCase))
            {
                contentSources.Add(source);
            }
        }

        if (contentSources.Count == 0)
        {
            return;
        }

        var sourcesWithCollection = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourcesWithoutCollection = new List<int>();
        for (var i = 0; i < contentSources.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(contentSources[i].Collection))
            {
                sourcesWithCollection.Add(contentSources[i].Collection!.Trim());
            }
            else
            {
                sourcesWithoutCollection.Add(i);
            }
        }

        if (sourcesWithoutCollection.Count == 0)
        {
            return;
        }

        var unreferencedCollections = collectionKeys.Except(sourcesWithCollection).ToList();

        if (unreferencedCollections.Count > 0)
        {
            if (sourcesWithoutCollection.Count == contentSources.Count)
            {
                throw new ConfigException(
                    "content.sources: no content source has a 'collection' field, but site.collections defines: " +
                    string.Join(", ", collectionKeys) +
                    ". Without collection assignment, content items cannot match their collection rules. " +
                    "Add 'collection: <name>' to each content source (e.g. collection: post).");
            }

            throw new ConfigException(
                "content.sources: the following site.collections have no matching content source with a 'collection' field: " +
                string.Join(", ", unreferencedCollections) +
                ". Assign them via 'collection: <name>' on each content source.");
        }
    }
}
