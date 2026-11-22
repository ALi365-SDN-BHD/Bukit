using System.Text.RegularExpressions;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Config;

/// <summary>The opt-in <c>miniprogram</c> section: datasets written as versioned JSON for a mini-program.</summary>
internal sealed record MiniProgramConfig
{
    internal const string DefaultOutputDir = "mp";
    internal const int DefaultMaxListItems = 2000;

    public bool Enabled { get; init; }
    public string OutputDir { get; init; } = DefaultOutputDir;
    public int MaxListItems { get; init; } = DefaultMaxListItems;
    public IReadOnlyList<MiniProgramDatasetConfig> Datasets { get; init; } = [];
}

internal sealed record MiniProgramDatasetConfig
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public string? Source { get; init; }
    public string? Module { get; init; }
    public string? Scope { get; init; }
    public string? Menu { get; init; }
    public string? Taxonomy { get; init; }
    public IReadOnlyList<string>? Fields { get; init; }
    public IReadOnlyList<string>? ListFields { get; init; }
    public IReadOnlyList<string>? DetailFields { get; init; }
    public MiniProgramSortConfig? Sort { get; init; }
    public IReadOnlyList<string>? Required { get; init; }
    public IReadOnlyDictionary<string, string>? Types { get; init; }

    /// <summary>Declared nested fields of object and object[] fields, keyed by the top-level field name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, MiniProgramFieldType>>? ObjectFields { get; init; }
}

internal sealed record MiniProgramSortConfig(string Field, bool Descending);

/// <summary>A nested field type: a scalar type name, or object/object[] with optional declared fields of its own.</summary>
internal sealed record MiniProgramFieldType(string Type, IReadOnlyDictionary<string, MiniProgramFieldType>? Fields);

internal static partial class MiniProgramKinds
{
    internal const string Collection = "collection";
    internal const string Module = "module";
    internal const string DataIndex = "dataIndex";
    internal const string Records = "records";
    internal const string Taxonomy = "taxonomy";
    internal const string Menu = "menu";

    internal static readonly string[] All = [Collection, Module, DataIndex, Records, Taxonomy, Menu];

    internal const string Object = "object";
    internal const string ObjectArray = "object[]";

    /// <summary>The deepest object nesting a field may reach; a top-level object field is depth 1.</summary>
    internal const int MaxObjectDepth = 8;

    internal static readonly string[] FieldTypes = ["string", "number", "boolean", "date", "image", "url", "string[]", Object, ObjectArray];

    internal static bool IsObjectType(string? type) => type is Object or ObjectArray;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    internal static partial Regex DatasetName();

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    internal static partial Regex OutputDirName();
}

internal static class MiniProgramConfigReader
{
    internal static MiniProgramConfig Read(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new MiniProgramConfig();
        }

        const string path = "miniprogram";
        var datasets = new List<MiniProgramDatasetConfig>();
        if (ConfigYamlHelpers.GetOptionalSequence(node, "datasets", path) is { } sequence)
        {
            var index = 0;
            foreach (var child in sequence.Children)
            {
                var itemPath = $"{path}.datasets[{index}]";
                if (child is not YamlMappingNode item)
                {
                    throw ConfigYamlHelpers.NodeKindMismatch(itemPath, "mapping", child);
                }

                datasets.Add(ReadDataset(item, itemPath));
                index++;
            }
        }

        return new MiniProgramConfig
        {
            Enabled = ConfigYamlHelpers.GetOptionalBoolStrict(node, "enabled", path) ?? false,
            OutputDir = ConfigYamlHelpers.GetOptionalString(node, "outputDir", path) ?? MiniProgramConfig.DefaultOutputDir,
            MaxListItems = ConfigYamlHelpers.GetOptionalIntStrict(node, "maxListItems") ?? MiniProgramConfig.DefaultMaxListItems,
            Datasets = datasets
        };
    }

    private static MiniProgramDatasetConfig ReadDataset(YamlMappingNode item, string path)
    {
        var sortNode = ConfigYamlHelpers.GetOptionalMapping(item, "sort", path);
        MiniProgramSortConfig? sort = null;
        if (sortNode is not null)
        {
            var order = ConfigYamlHelpers.GetOptionalString(sortNode, "order", $"{path}.sort") ?? "asc";
            if (order is not ("asc" or "desc"))
            {
                throw new ConfigException($"{path}.sort.order must be asc|desc.", DiagnosticCode.ConfigInvalidValue);
            }

            sort = new MiniProgramSortConfig(ConfigYamlHelpers.GetRequiredString(sortNode, "field", $"{path}.sort"), order == "desc");
        }

        var types = ReadTypes(item, path);
        return new MiniProgramDatasetConfig
        {
            Name = ConfigYamlHelpers.GetRequiredString(item, "name", path),
            Kind = ConfigYamlHelpers.GetRequiredString(item, "kind", path),
            Source = ConfigYamlHelpers.GetOptionalString(item, "source", path),
            Module = ConfigYamlHelpers.GetOptionalString(item, "module", path),
            Scope = ConfigYamlHelpers.GetOptionalString(item, "scope", path),
            Menu = ConfigYamlHelpers.GetOptionalString(item, "menu", path),
            Taxonomy = ConfigYamlHelpers.GetOptionalString(item, "taxonomy", path),
            Fields = ConfigYamlHelpers.ReadStringList(item, "fields"),
            ListFields = ConfigYamlHelpers.ReadStringList(item, "listFields"),
            DetailFields = ConfigYamlHelpers.ReadStringList(item, "detailFields"),
            Sort = sort,
            Required = ConfigYamlHelpers.ReadStringList(item, "required"),
            Types = types?.Types,
            ObjectFields = types?.ObjectFields
        };
    }

    /// <summary>
    /// Reads <c>types</c>, where a value is a type name or, for object and object[], a mapping
    /// <c>{ type, fields }</c> whose optional <c>fields</c> declare nested types the same way.
    /// </summary>
    private static (IReadOnlyDictionary<string, string> Types, IReadOnlyDictionary<string, IReadOnlyDictionary<string, MiniProgramFieldType>>? ObjectFields)? ReadTypes(
        YamlMappingNode item, string path)
    {
        var node = ConfigYamlHelpers.GetOptionalMapping(item, "types", path);
        if (node is null)
        {
            return null;
        }

        var types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, IReadOnlyDictionary<string, MiniProgramFieldType>>? objectFields = null;
        foreach (var (field, type) in ReadFieldTypes(node, $"{path}.types", depth: 1, StringComparer.OrdinalIgnoreCase))
        {
            types[field] = type.Type;
            if (type.Fields is not null)
            {
                objectFields ??= new Dictionary<string, IReadOnlyDictionary<string, MiniProgramFieldType>>(StringComparer.OrdinalIgnoreCase);
                objectFields[field] = type.Fields;
            }
        }

        return (types, objectFields);
    }

    private static Dictionary<string, MiniProgramFieldType> ReadFieldTypes(YamlMappingNode node, string path, int depth, StringComparer comparer)
    {
        var fields = new Dictionary<string, MiniProgramFieldType>(comparer);
        foreach (var (keyNode, valueNode) in node.Children)
        {
            if (keyNode is not YamlScalarNode { Value: { } rawKey } || string.IsNullOrWhiteSpace(rawKey))
            {
                throw ConfigYamlHelpers.NodeKindMismatch($"{path} key", "non-empty scalar", keyNode);
            }

            // An empty type is skipped, as types always allowed.
            if (valueNode is YamlScalarNode { Value: var rawType } && string.IsNullOrWhiteSpace(rawType))
            {
                continue;
            }

            var key = rawKey.Trim();
            var fieldPath = $"{path}.{key}";
            if (!fields.TryAdd(key, ReadFieldType(valueNode, fieldPath, depth)))
            {
                throw new ConfigException($"{fieldPath} is declared more than once.", DiagnosticCode.ConfigInvalidValue);
            }
        }

        return fields;
    }

    private static MiniProgramFieldType ReadFieldType(YamlNode node, string path, int depth)
    {
        switch (node)
        {
            case YamlScalarNode scalar:
                return new MiniProgramFieldType(scalar.Value!.Trim(), null);
            case YamlMappingNode mapping:
                var type = ConfigYamlHelpers.GetOptionalString(mapping, "type", path)
                    ?? throw new ConfigException($"{path}.type is required.", DiagnosticCode.ConfigRequiredFieldMissing);
                foreach (var (keyNode, _) in mapping.Children)
                {
                    if (keyNode is not YamlScalarNode { Value: "type" or "fields" })
                    {
                        throw new ConfigException($"{path} allows only type and fields.", DiagnosticCode.ConfigInvalidValue);
                    }
                }

                var fieldsNode = ConfigYamlHelpers.GetOptionalMapping(mapping, "fields", path);
                if (fieldsNode is not null && depth >= MiniProgramKinds.MaxObjectDepth)
                {
                    throw new ConfigException(
                        $"{path}.fields exceeds the maximum object nesting depth of {MiniProgramKinds.MaxObjectDepth}.", DiagnosticCode.ConfigInvalidValue);
                }

                // Nested keys are written to JSON as they are, so they match exactly.
                return new MiniProgramFieldType(type.Trim(), fieldsNode is null ? null : ReadFieldTypes(fieldsNode, $"{path}.fields", depth + 1, StringComparer.Ordinal));
            default:
                throw ConfigYamlHelpers.NodeKindMismatch(path, "scalar or mapping", node);
        }
    }
}

internal static class MiniProgramConfigValidator
{
    internal static void Validate(AppConfig config)
    {
        var miniProgram = config.MiniProgram;
        if (!miniProgram.Enabled)
        {
            return;
        }

        if (!MiniProgramKinds.OutputDirName().IsMatch(miniProgram.OutputDir))
        {
            throw Invalid("miniprogram.outputDir must be a single directory name of letters, digits, '-' or '_'.");
        }

        if (miniProgram.MaxListItems < 1)
        {
            throw Invalid("miniprogram.maxListItems must be at least 1.");
        }

        ValidateSiteUrl(config, required: false);
        var sources = config.Content.Sources ?? [];
        var names = new HashSet<string>(StringComparer.Ordinal) { "site" };
        for (var i = 0; i < miniProgram.Datasets.Count; i++)
        {
            var dataset = miniProgram.Datasets[i];
            var path = $"miniprogram.datasets[{i}]";
            if (!MiniProgramKinds.DatasetName().IsMatch(dataset.Name))
            {
                throw Invalid($"{path}.name must match ^[a-z0-9][a-z0-9-]*$.");
            }

            if (!names.Add(dataset.Name))
            {
                throw Invalid($"{path}.name '{dataset.Name}' is reserved or already used by another dataset.");
            }

            if (!MiniProgramKinds.All.Contains(dataset.Kind, StringComparer.Ordinal))
            {
                throw Invalid($"{path}.kind must be {string.Join('|', MiniProgramKinds.All)}.");
            }

            RejectUnusedKeys(dataset, path);
            ValidateKind(config, sources, dataset, path);
            ValidateTypes(dataset, path);
        }
    }

    /// <summary>
    /// The effective site URL for mini-program output, without a trailing slash. Configuration
    /// validation passes <paramref name="required"/>=false because CLI overrides such as
    /// <c>--site-url</c> may still supply it; the build passes true once overrides are applied.
    /// </summary>
    internal static string? ValidateSiteUrl(AppConfig config, bool required)
    {
        if (!config.MiniProgram.Enabled)
        {
            return null;
        }

        var url = config.Site.Url?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(url))
        {
            return required
                ? throw new ConfigException("miniprogram requires site.url (or --site-url) to be an https:// URL.", DiagnosticCode.ConfigRequiredFieldMissing)
                : null;
        }

        // File URLs are appended to the site URL and published to every client, so a query or fragment
        // would break each of them and user info would publish credentials.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrEmpty(uri.Host) || url.Any(char.IsWhiteSpace) || url.Contains('?') || url.Contains('#') ||
            uri.UserInfo.Length > 0)
        {
            // Never echo the credentials themselves.
            var shown = uri is { UserInfo.Length: > 0 } ? url.Replace(uri.UserInfo + "@", "***@", StringComparison.Ordinal) : url;
            throw Invalid($"miniprogram requires site.url to be an https:// URL without user info, query or fragment; got '{shown}'.");
        }

        return url;
    }

    private static void RejectUnusedKeys(MiniProgramDatasetConfig dataset, string path)
    {
        string[] used = dataset.Kind switch
        {
            MiniProgramKinds.Collection => ["source", "listFields", "detailFields", "sort", "required", "types"],
            MiniProgramKinds.Module => ["source", "module", "fields", "sort", "required", "types"],
            MiniProgramKinds.Records => ["source", "fields", "sort", "required", "types"],
            MiniProgramKinds.DataIndex => ["source", "scope", "required", "types"],
            MiniProgramKinds.Taxonomy => ["taxonomy"],
            MiniProgramKinds.Menu => ["menu"],
            _ => []
        };
        (string Key, bool Present)[] keys =
        [
            ("source", dataset.Source is not null),
            ("module", dataset.Module is not null),
            ("scope", dataset.Scope is not null),
            ("menu", dataset.Menu is not null),
            ("taxonomy", dataset.Taxonomy is not null),
            ("fields", dataset.Fields is not null),
            ("listFields", dataset.ListFields is not null),
            ("detailFields", dataset.DetailFields is not null),
            ("sort", dataset.Sort is not null),
            ("required", dataset.Required is not null),
            ("types", dataset.Types is not null)
        ];
        foreach (var (key, present) in keys)
        {
            if (present && !used.Contains(key, StringComparer.Ordinal))
            {
                throw Invalid($"{path}.{key} is not allowed for kind {dataset.Kind}.");
            }
        }
    }

    private static void ValidateKind(AppConfig config, IReadOnlyList<ContentSourceConfig> sources, MiniProgramDatasetConfig dataset, string path)
    {
        switch (dataset.Kind)
        {
            case MiniProgramKinds.Collection:
                RequireSource(sources, dataset, path, "content");
                RequireFields(dataset.ListFields, $"{path}.listFields");
                RequireFields(dataset.DetailFields, $"{path}.detailFields");
                break;
            case MiniProgramKinds.Module:
                RequireRenderedBody(RequireSource(sources, dataset, path, "data"), dataset, path);
                Require(dataset.Module, $"{path}.module");
                RequireFields(dataset.Fields, $"{path}.fields");
                RequireSort(dataset, path);
                break;
            case MiniProgramKinds.Records:
                RequireRenderedBody(RequireSource(sources, dataset, path, "data"), dataset, path);
                RequireFields(dataset.Fields, $"{path}.fields");
                RequireSort(dataset, path);
                break;
            case MiniProgramKinds.DataIndex:
                var source = RequireSource(sources, dataset, path, "data");
                if (source.DataIndex is null)
                {
                    throw Invalid($"{path}.source '{dataset.Source}' has no dataIndex.");
                }

                Require(dataset.Scope, $"{path}.scope");
                break;
            case MiniProgramKinds.Taxonomy:
                Require(dataset.Taxonomy, $"{path}.taxonomy");
                if (!config.MiniProgram.Datasets.Any(candidate => candidate.Kind == MiniProgramKinds.Collection))
                {
                    throw Invalid($"{path} counts terms of collection datasets; declare at least one kind: collection dataset.");
                }

                break;
            case MiniProgramKinds.Menu:
                Require(dataset.Menu, $"{path}.menu");
                if (config.Site.Menus is null || !config.Site.Menus.ContainsKey(dataset.Menu!))
                {
                    throw Invalid($"{path}.menu '{dataset.Menu}' is not defined in site.menus.");
                }

                break;
        }
    }

    private static ContentSourceConfig RequireSource(IReadOnlyList<ContentSourceConfig> sources, MiniProgramDatasetConfig dataset, string path, string mode)
    {
        Require(dataset.Source, $"{path}.source");
        var source = sources.FirstOrDefault(candidate => string.Equals(candidate.Name?.Trim(), dataset.Source, StringComparison.OrdinalIgnoreCase))
            ?? throw Invalid($"{path}.source '{dataset.Source}' does not name a content.sources[].name.");
        var sourceMode = (source.Mode ?? "content").Trim().ToLowerInvariant();
        if (sourceMode != mode)
        {
            throw Invalid($"{path}.source '{dataset.Source}' must use mode: {mode} for kind {dataset.Kind}.");
        }

        return source;
    }

    // Notion data sources skip page rendering unless asked, so a declared body would always be empty.
    private static void RequireRenderedBody(ContentSourceConfig source, MiniProgramDatasetConfig dataset, string path)
    {
        if (dataset.Fields?.Contains("body", StringComparer.Ordinal) == true &&
            string.Equals(source.Type, "notion", StringComparison.OrdinalIgnoreCase) &&
            source.Notion?.RenderContent != true)
        {
            throw Invalid($"{path}.fields declares body, but source '{dataset.Source}' is a Notion data source without notion.renderContent: true.");
        }
    }

    private static void RequireSort(MiniProgramDatasetConfig dataset, string path)
    {
        if (dataset.Sort is null)
        {
            throw new ConfigException($"{path}.sort is required for kind {dataset.Kind}.", DiagnosticCode.ConfigRequiredFieldMissing);
        }
    }

    private static void RequireFields(IReadOnlyList<string>? fields, string path)
    {
        if (fields is not { Count: > 0 })
        {
            throw new ConfigException($"{path} must list at least one field.", DiagnosticCode.ConfigRequiredFieldMissing);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            if (!seen.Add(field))
            {
                throw Invalid($"{path} lists '{field}' more than once.");
            }
        }
    }

    private static void ValidateTypes(MiniProgramDatasetConfig dataset, string path)
    {
        // dataIndex keys come from the data, so only the other kinds can check against declared fields.
        // RejectUnusedKeys already removed field lists that the kind does not write.
        HashSet<string>? declared = dataset.Kind == MiniProgramKinds.DataIndex
            ? null
            : new HashSet<string>([.. dataset.Fields ?? [], .. dataset.ListFields ?? [], .. dataset.DetailFields ?? [], "id"], StringComparer.Ordinal);
        foreach (var (field, type) in dataset.Types ?? new Dictionary<string, string>())
        {
            IReadOnlyDictionary<string, MiniProgramFieldType>? fields = null;
            dataset.ObjectFields?.TryGetValue(field, out fields);
            ValidateFieldType(new MiniProgramFieldType(type, fields), $"{path}.types.{field}");
            if (field == "body")
            {
                throw Invalid($"{path}.types.body is not allowed; body is always sanitized HTML.");
            }

            RequireDeclared(declared, field, $"{path}.types.{field}");
        }

        foreach (var field in dataset.ObjectFields?.Keys ?? [])
        {
            if (dataset.Types?.ContainsKey(field) != true)
            {
                throw Invalid($"{path}.types.{field}.fields has no type.");
            }
        }

        foreach (var field in dataset.Required ?? [])
        {
            RequireDeclared(declared, field, $"{path}.required");
        }
    }

    private static void ValidateFieldType(MiniProgramFieldType type, string path)
    {
        if (!MiniProgramKinds.FieldTypes.Contains(type.Type, StringComparer.Ordinal))
        {
            throw Invalid($"{path} must be {string.Join('|', MiniProgramKinds.FieldTypes)}.");
        }

        if (type.Fields is null)
        {
            return;
        }

        if (!MiniProgramKinds.IsObjectType(type.Type))
        {
            throw Invalid($"{path}.fields is only allowed for type object or object[].");
        }

        foreach (var (field, nested) in type.Fields)
        {
            ValidateFieldType(nested, $"{path}.fields.{field}");
        }
    }

    private static void RequireDeclared(HashSet<string>? declared, string field, string path)
    {
        if (declared is not null && !declared.Contains(field))
        {
            throw Invalid($"{path} names '{field}', which is not a declared field of this dataset.");
        }
    }

    private static void Require(string? value, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ConfigException($"{path} is required.", DiagnosticCode.ConfigRequiredFieldMissing);
        }
    }

    private static ConfigException Invalid(string message) => new(message, DiagnosticCode.ConfigInvalidValue);
}
