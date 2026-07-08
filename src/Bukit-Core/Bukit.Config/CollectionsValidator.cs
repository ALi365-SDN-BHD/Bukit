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
                throw new ConfigException("site.collections keys must be non-empty strings.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            if (string.IsNullOrWhiteSpace(collection.Permalink))
            {
                throw new ConfigException($"site.collections.{name}.permalink is required.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            if (!collection.Permalink.Contains("{slug}", StringComparison.OrdinalIgnoreCase))
            {
                throw new ConfigException($"site.collections.{name}.permalink must include {{slug}}.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            if (collection.Template is not null && string.IsNullOrWhiteSpace(collection.Template))
            {
                throw new ConfigException($"site.collections.{name}.template must be a non-empty string when set.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            if (collection.Pagination.PageSize <= 0)
            {
                throw new ConfigException($"site.collections.{name}.pagination.pageSize must be a positive integer.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            ValidatePaginationUrlPattern($"site.collections.{name}.pagination.urlPattern", collection.Pagination.UrlPattern);

            if (!string.IsNullOrWhiteSpace(collection.ListRoute))
            {
                if (!collection.ListRoute.StartsWith('/'))
                {
                    throw new ConfigException($"site.collections.{name}.listRoute must start with '/'.", DiagnosticCode.ConfigInvalidValue);
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
                    throw new ConfigException($"site.collections.{name}.schemaFailMode must be off|warn|strict.", DiagnosticCode.ConfigRequiredFieldMissing);
                }
            }
        }
    }

    private static void ValidatePaginationUrlPattern(string fieldName, string? urlPattern)
    {
        if (string.IsNullOrWhiteSpace(urlPattern))
        {
            throw new ConfigException($"{fieldName} must be a non-empty relative URL pattern.", DiagnosticCode.ConfigInvalidValue);
        }

        var pattern = urlPattern.Trim();
        if (pattern.StartsWith('/') ||
            pattern.StartsWith("//", StringComparison.Ordinal) ||
            pattern.Contains("://", StringComparison.Ordinal))
        {
            throw new ConfigException($"{fieldName} must be relative.", DiagnosticCode.ConfigInvalidValue);
        }

        if (pattern.Any(char.IsControl))
        {
            throw new ConfigException($"{fieldName} must not contain control characters.", DiagnosticCode.ConfigInvalidValue);
        }

        if (pattern.Contains('\\'))
        {
            throw new ConfigException($"{fieldName} must not contain backslashes.", DiagnosticCode.ConfigInvalidValue);
        }

        if (pattern.Contains('?') || pattern.Contains('#'))
        {
            throw new ConfigException($"{fieldName} must not contain query strings or fragments.", DiagnosticCode.ConfigInvalidValue);
        }

        foreach (var segment in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.", DiagnosticCode.ConfigPathTraversal);
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(segment);
            }
            catch
            {
                throw new ConfigException($"{fieldName} must contain valid percent-encoding.", DiagnosticCode.ConfigInvalidValue);
            }

            if (decoded is "." or "..")
            {
                throw new ConfigException($"{fieldName} must not contain '..' path traversal segments.", DiagnosticCode.ConfigPathTraversal);
            }

            if (decoded.Contains('/') || decoded.Contains('\\'))
            {
                throw new ConfigException($"{fieldName} must not contain encoded slashes.", DiagnosticCode.ConfigInvalidValue);
            }
        }

        ValidatePaginationPlaceholders(fieldName, pattern);

        if (!ContainsPagePlaceholder(pattern))
        {
            throw new ConfigException($"{fieldName} must include :num, {{num}}, or {{page}}.", DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static void ValidatePaginationPlaceholders(string fieldName, string pattern)
    {
        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] == '}')
            {
                throw new ConfigException($"{fieldName} contains an unopened placeholder.", DiagnosticCode.ConfigInvalidValue);
            }

            if (pattern[i] != '{')
            {
                continue;
            }

            var end = pattern.IndexOf('}', i + 1);
            if (end < 0)
            {
                throw new ConfigException($"{fieldName} contains an unclosed placeholder.", DiagnosticCode.ConfigInvalidValue);
            }

            var placeholder = pattern[(i + 1)..end];
            if (!IsSupportedPaginationPlaceholder(placeholder))
            {
                throw new ConfigException($"{fieldName} contains unsupported placeholder {{{placeholder}}}. Supported placeholders: :num, {{num}}, {{page}}, {{collection}}, {{slug}}.", DiagnosticCode.ConfigInvalidValue);
            }

            i = end;
        }
    }

    private static bool IsSupportedPaginationPlaceholder(string placeholder)
        => placeholder.Equals("num", StringComparison.OrdinalIgnoreCase) ||
           placeholder.Equals("page", StringComparison.OrdinalIgnoreCase) ||
           placeholder.Equals("collection", StringComparison.OrdinalIgnoreCase) ||
           placeholder.Equals("slug", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPagePlaceholder(string pattern)
    {
        return pattern.Contains(":num", StringComparison.OrdinalIgnoreCase) ||
               pattern.Contains("{num}", StringComparison.OrdinalIgnoreCase) ||
               pattern.Contains("{page}", StringComparison.OrdinalIgnoreCase);
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
                throw new ConfigException($"{prefix}.field is required.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            var filterOperator = (filter.Operator ?? "equals").Trim().ToLowerInvariant();
            if (filterOperator is not ("equals" or "contains" or "in"))
            {
                throw new ConfigException($"{prefix}.operator must be equals|contains|in.", DiagnosticCode.ConfigInvalidValue);
            }

            if (filterOperator is "equals" or "contains" && string.IsNullOrWhiteSpace(filter.Value))
            {
                throw new ConfigException($"{prefix}.value is required for operator {filterOperator}.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            if (filterOperator is "equals" or "contains" && HasAnyFilterValue(filter.Values))
            {
                throw new ConfigException($"{prefix}.values is only supported for operator in.", DiagnosticCode.ConfigInvalidValue);
            }

            if (filterOperator == "in" && !HasAnyFilterValue(filter.Values))
            {
                throw new ConfigException($"{prefix}.values must include at least one value for operator in.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            if (filterOperator == "in" && !string.IsNullOrWhiteSpace(filter.Value))
            {
                throw new ConfigException($"{prefix}.value must not be set when operator is in; use values instead.", DiagnosticCode.ConfigInvalidValue);
            }

            if (string.IsNullOrWhiteSpace(filter.ListRoute))
            {
                throw new ConfigException($"{prefix}.listRoute is required.", DiagnosticCode.ConfigRequiredFieldMissing);
            }

            if (!filter.ListRoute.StartsWith('/'))
            {
                throw new ConfigException($"{prefix}.listRoute must start with '/'.", DiagnosticCode.ConfigInvalidValue);
            }

            if (!usedRoutes.Add(filter.ListRoute.Trim().ToLowerInvariant()))
            {
                throw new ConfigException($"{prefix}.listRoute '{filter.ListRoute}' duplicates another filtered list route.", DiagnosticCode.ConfigInvalidValue);
            }

            if (filter.ListTemplate is not null && string.IsNullOrWhiteSpace(filter.ListTemplate))
            {
                throw new ConfigException($"{prefix}.listTemplate must be a non-empty string when set.", DiagnosticCode.ConfigInvalidValue);
            }

            if (filter.PageSize is not null && filter.PageSize <= 0)
            {
                throw new ConfigException($"{prefix}.pageSize must be a positive integer.", DiagnosticCode.ConfigInvalidValue);
            }

            if (filter.UrlPattern is not null)
            {
                ValidatePaginationUrlPattern($"{prefix}.urlPattern", filter.UrlPattern);
            }

            var emptyBehavior = (filter.EmptyBehavior ?? "render").Trim().ToLowerInvariant();
            if (emptyBehavior is not ("render" or "skip"))
            {
                throw new ConfigException($"{prefix}.emptyBehavior must be render|skip.", DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static bool HasAnyFilterValue(IReadOnlyList<string>? values)
        => values is { Count: > 0 } && values.Any(value => !string.IsNullOrWhiteSpace(value));

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
