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
}

internal sealed record MiniProgramSortConfig(string Field, bool Descending);

internal static partial class MiniProgramKinds
{
    internal const string Collection = "collection";
    internal const string Module = "module";
    internal const string DataIndex = "dataIndex";
    internal const string Records = "records";
    internal const string Taxonomy = "taxonomy";
    internal const string Menu = "menu";

    internal static readonly string[] All = [Collection, Module, DataIndex, Records, Taxonomy, Menu];

    internal static readonly string[] FieldTypes = ["string", "number", "boolean", "date", "image", "url", "string[]"];

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
            Types = ConfigYamlHelpers.ReadStringMap(item, "types")
        };
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

            ValidateKind(config, sources, dataset, path);
            ValidateTypes(dataset, path);
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
                RequireSource(sources, dataset, path, "data");
                Require(dataset.Module, $"{path}.module");
                RequireFields(dataset.Fields, $"{path}.fields");
                RequireSort(dataset, path);
                break;
            case MiniProgramKinds.Records:
                RequireSource(sources, dataset, path, "data");
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
                RejectSource(dataset, path);
                Require(dataset.Taxonomy, $"{path}.taxonomy");
                break;
            case MiniProgramKinds.Menu:
                RejectSource(dataset, path);
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

    private static void RejectSource(MiniProgramDatasetConfig dataset, string path)
    {
        if (dataset.Source is not null)
        {
            throw Invalid($"{path}.source is not allowed for kind {dataset.Kind}.");
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
        HashSet<string>? declared = dataset.Kind == MiniProgramKinds.DataIndex
            ? null
            : new HashSet<string>([.. dataset.Fields ?? [], .. dataset.ListFields ?? [], .. dataset.DetailFields ?? [], "id"], StringComparer.Ordinal);
        foreach (var (field, type) in dataset.Types ?? new Dictionary<string, string>())
        {
            if (!MiniProgramKinds.FieldTypes.Contains(type, StringComparer.Ordinal))
            {
                throw Invalid($"{path}.types.{field} must be {string.Join('|', MiniProgramKinds.FieldTypes)}.");
            }

            RequireDeclared(declared, field, $"{path}.types.{field}");
        }

        foreach (var field in dataset.Required ?? [])
        {
            RequireDeclared(declared, field, $"{path}.required");
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
