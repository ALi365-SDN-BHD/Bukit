using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

/// <summary>Reads <c>site.queries</c>; every error names the full config path.</summary>
internal static class SiteQueryConfigReader
{
    internal static IReadOnlyList<SiteQueryConfig> Read(YamlMappingNode siteNode)
    {
        if (ConfigYamlHelpers.GetOptionalMapping(siteNode, "queries", "site") is not { } node)
        {
            return [];
        }

        var queries = new List<SiteQueryConfig>();
        foreach (var (key, value) in node.Children)
        {
            var name = (key as YamlScalarNode)?.Value?.Trim() ?? string.Empty;
            var path = $"site.queries.{name}";
            if (!ContentLinkPath.IsName(name))
            {
                throw Invalid($"{path} must be named with letters, digits, '_' or '-'.");
            }

            if (value is not YamlMappingNode query)
            {
                throw Invalid($"{path} must be a mapping.");
            }

            if (queries.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                throw Invalid($"{path} repeats a query name; query names ignore case.");
            }

            var distinct = OptionalName(query, "distinct", path);
            var group = ReadGroup(query, path);
            if (distinct is not null && group is not null)
            {
                throw Invalid($"{path} cannot use both distinct and group.");
            }

            var limit = ConfigYamlHelpers.GetOptionalIntStrict(query, "limit");
            if (limit is < 1)
            {
                throw Invalid($"{path}.limit must be at least 1, got {limit}.");
            }

            queries.Add(new SiteQueryConfig
            {
                Name = name,
                From = ConfigYamlHelpers.GetOptionalString(query, "from")?.Trim() is { Length: > 0 } from
                    ? from
                    : throw new ConfigException($"{path}.from is required.", DiagnosticCode.ConfigRequiredFieldMissing),
                Where = ReadItems(query, "where", path, ReadFilter),
                Sort = ReadItems(query, "sort", path, ReadSort),
                Select = Select(query, path),
                Distinct = distinct,
                Group = group,
                Limit = limit
            });
        }

        return queries;
    }

    private static SiteQueryFilterConfig ReadFilter(YamlMappingNode item, string path)
    {
        var field = OptionalName(item, "field", path);
        var hasBackref = OptionalName(item, "hasBackref", path);
        var hasEq = item.Children.ContainsKey(new YamlScalarNode("eq"));
        var hasIn = item.Children.ContainsKey(new YamlScalarNode("in"));
        if (hasBackref is not null)
        {
            return field is null && !hasEq && !hasIn
                ? new SiteQueryFilterConfig { HasBackref = hasBackref }
                : throw Invalid($"{path} uses hasBackref, so it cannot also set field, eq or in.");
        }

        if (field is null || hasEq == hasIn)
        {
            throw Invalid($"{path} needs field with exactly one of eq or in, or hasBackref.");
        }

        IReadOnlyList<string> values = hasEq
            ? [ConfigYamlHelpers.GetOptionalString(item, "eq", path)?.Trim() ?? string.Empty]
            : ConfigYamlHelpers.ReadStringList(item, "in") ?? [];
        return values.Count == 0 || values.Any(string.IsNullOrEmpty)
            ? throw Invalid($"{path}.{(hasEq ? "eq" : "in")} must have a value.")
            : new SiteQueryFilterConfig { Field = field, Values = values };
    }

    private static SiteQuerySortConfig ReadSort(YamlMappingNode item, string path)
    {
        var direction = ConfigYamlHelpers.GetOptionalString(item, "direction", path)?.Trim().ToLowerInvariant() ?? "asc";
        if (direction is not ("asc" or "desc"))
        {
            throw Invalid($"{path}.direction must be asc or desc, got '{direction}'.");
        }

        return new SiteQuerySortConfig
        {
            Field = OptionalName(item, "field", path)
                ?? throw new ConfigException($"{path}.field is required.", DiagnosticCode.ConfigRequiredFieldMissing),
            Direction = direction,
            Values = ConfigYamlHelpers.ReadStringList(item, "values") ?? []
        };
    }

    private static SiteQueryGroupConfig? ReadGroup(YamlMappingNode query, string path)
    {
        if (ConfigYamlHelpers.GetOptionalMapping(query, "group", path) is not { } group)
        {
            return null;
        }

        return new SiteQueryGroupConfig
        {
            Field = OptionalName(group, "field", $"{path}.group")
                ?? throw new ConfigException($"{path}.group.field is required.", DiagnosticCode.ConfigRequiredFieldMissing),
            Values = ConfigYamlHelpers.ReadStringList(group, "values") is { Count: > 0 } values
                ? values
                : throw new ConfigException($"{path}.group.values must list at least one value.", DiagnosticCode.ConfigRequiredFieldMissing)
        };
    }

    private static List<T> ReadItems<T>(YamlMappingNode query, string key, string path, Func<YamlMappingNode, string, T> read)
    {
        if (ConfigYamlHelpers.GetOptionalSequence(query, key, path) is not { } sequence)
        {
            return [];
        }

        var items = new List<T>();
        for (var i = 0; i < sequence.Children.Count; i++)
        {
            var itemPath = $"{path}.{key}[{i}]";
            items.Add(sequence.Children[i] is YamlMappingNode item
                ? read(item, itemPath)
                : throw Invalid($"{itemPath} must be a mapping."));
        }

        return items;
    }

    /// <summary>Query entries carry key, title, slug, url and collection like link entries; select must not overwrite them.</summary>
    private static IReadOnlyList<string> Select(YamlMappingNode query, string path)
    {
        var names = ConfigYamlHelpers.ReadStringList(query, "select") ?? [];
        ConfigValidator.ValidateContentLinkSelect(names, $"{path}.select");
        return names;
    }

    private static string? OptionalName(YamlMappingNode node, string key, string path)
    {
        var value = ConfigYamlHelpers.GetOptionalString(node, key, path)?.Trim();
        return value is null || ContentLinkPath.IsName(value)
            ? value
            : throw Invalid($"{path}.{key} '{value}' is not a valid field name.");
    }

    private static ConfigException Invalid(string message) => new(message, DiagnosticCode.ConfigInvalidValue);
}
