using System.Globalization;
using System.Text.Json.Nodes;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Config;
using Bukit.Shared;
using YamlDotNet.RepresentationModel;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class DataFilesPlugin : IBukitPlugin, IDerivePagesPlugin, IDerivePagesAsyncPlugin
{
    private const int DefaultMaxEntries = 10_000;
    private const int DefaultMaxDepth = 64;

    private readonly AppConfig _config;
    private readonly int _maxEntries;
    private readonly int _maxDepth;

    internal DataFilesPlugin(
        AppConfig config,
        int maxEntries = DefaultMaxEntries,
        int maxDepth = DefaultMaxDepth)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);
        _config = config;
        _maxEntries = maxEntries;
        _maxDepth = maxDepth;
    }

    public string Name => "data-files";
    public string Version => "1.0.0";

    public IReadOnlyList<RoutedContentDocument> DerivePages(BuildContext context)
        => DerivePagesCore(context, CancellationToken.None);

    public Task<IReadOnlyList<RoutedContentDocument>> DerivePagesAsync(
        BuildContext context,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DerivePagesCore(context, cancellationToken));

    private IReadOnlyList<RoutedContentDocument> DerivePagesCore(
        BuildContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataDir = Path.Combine(context.RootDir, "data");
        if (!Directory.Exists(dataDir) || IsReparsePoint(dataDir))
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var languages = _config.Site.Languages;
        if (languages is { Count: > 0 })
        {
            foreach (var lang in languages)
            {
                var langDir = Path.Combine(dataDir, lang);
                if (Directory.Exists(langDir) && !IsReparsePoint(langDir))
                {
                    result[lang] = LoadDataDirectory(langDir, dataDir, cancellationToken);
                }
            }
        }

        var defaultData = LoadDataDirectory(dataDir, dataDir, cancellationToken);
        foreach (var (key, value) in defaultData)
        {
            if (!result.ContainsKey(key))
            {
                result[key] = value;
            }
        }

        if (result.Count > 0)
        {
            context.Data["__data_files"] = result;
        }

        return Array.Empty<RoutedContentDocument>();
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private Dictionary<string, object> LoadDataDirectory(
        string dir,
        string dataRoot,
        CancellationToken cancellationToken)
        => LoadDataDirectory(
            dir,
            dataRoot,
            new TraversalState(_maxEntries, _maxDepth, cancellationToken),
            depth: 0);

    private static Dictionary<string, object> LoadDataDirectory(
        string dir,
        string dataRoot,
        TraversalState traversal,
        int depth)
    {
        traversal.EnterDirectory(dir, dataRoot, depth);
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        foreach (var file in Directory.EnumerateFiles(dir, "*", options)
                     .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            traversal.VisitEntry(dataRoot, file);
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".toml")
            {
                throw new ConfigException(
                    $"Unsupported data file format: {GetRelativeDataPath(dataRoot, file)}. Supported formats are .json, .yaml, and .yml.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (ext is not ".yaml" and not ".yml" and not ".json")
            {
                continue;
            }

            var key = Path.GetFileNameWithoutExtension(file);
            object? data = null;

            try
            {
                var content = File.ReadAllText(file);

                if (ext is ".json")
                {
                    data = ConvertJsonNode(JsonNode.Parse(content));
                }
                else
                {
                    var stream = new YamlStream();
                    stream.Load(new StringReader(content));
                    if (stream.Documents.Count > 0)
                    {
                        data = ConvertYamlNode(stream.Documents[0].RootNode);
                    }
                }
            }
            catch (Exception ex) when (ex is not ConfigException)
            {
                throw new ConfigException(
                    $"Failed to parse data file {GetRelativeDataPath(dataRoot, file)}.",
                    ex,
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (data is not null)
            {
                AddUnique(result, key, data, GetRelativeDataPath(dataRoot, file));
            }
        }

        foreach (var subDir in Directory.EnumerateDirectories(dir, "*", options)
                     .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            traversal.VisitEntry(dataRoot, subDir);
            var subName = Path.GetFileName(subDir);
            var subData = LoadDataDirectory(subDir, dataRoot, traversal, depth + 1);
            if (subData.Count > 0)
            {
                AddUnique(result, subName, subData, GetRelativeDataPath(dataRoot, subDir));
            }
        }

        return result;
    }

    private sealed class TraversalState
    {
        private readonly int _maxEntries;
        private readonly int _maxDepth;
        private readonly CancellationToken _cancellationToken;
        private readonly HashSet<string> _visitedDirectories = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private int _entryCount;

        internal TraversalState(
            int maxEntries,
            int maxDepth,
            CancellationToken cancellationToken)
        {
            _maxEntries = maxEntries;
            _maxDepth = maxDepth;
            _cancellationToken = cancellationToken;
        }

        internal void EnterDirectory(string directory, string dataRoot, int depth)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (depth > _maxDepth)
            {
                throw new ConfigException(
                    $"Data directory depth exceeds the maximum of {_maxDepth} at {GetRelativeDataPath(dataRoot, directory)}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            var normalized = Path.GetFullPath(directory);
            if (!_visitedDirectories.Add(normalized))
            {
                throw new ConfigException(
                    $"Data directory cycle detected at {GetRelativeDataPath(dataRoot, directory)}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }

        internal void VisitEntry(string dataRoot, string path)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            _entryCount++;
            if (_entryCount > _maxEntries)
            {
                throw new ConfigException(
                    $"Data directory contains more than {_maxEntries} entries at {GetRelativeDataPath(dataRoot, path)}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private static void AddUnique(
        Dictionary<string, object> result,
        string key,
        object value,
        string relativePath)
    {
        if (!result.TryAdd(key, value))
        {
            throw new ConfigException(
                $"Duplicate data key '{key}' at {relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    private static string GetRelativeDataPath(string dataRoot, string path)
    {
        var relative = Path.GetRelativePath(dataRoot, path).Replace('\\', '/');
        return relative == "." ? "data" : $"data/{relative}";
    }

    private static object? ConvertJsonNode(JsonNode? node)
        => node switch
        {
            null => null,
            JsonObject obj => obj.ToDictionary(
                property => property.Key,
                property => ConvertJsonNode(property.Value) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            JsonArray array => array.Select(item => ConvertJsonNode(item) ?? string.Empty).ToList(),
            JsonValue value => ConvertJsonValue(value),
            _ => node.ToJsonString()
        };

    private static object ConvertJsonValue(JsonValue value)
    {
        if (value.TryGetValue<string>(out var text)) return text;
        if (value.TryGetValue<bool>(out var boolean)) return boolean;
        if (value.TryGetValue<long>(out var integer)) return integer;
        if (value.TryGetValue<double>(out var number)) return number;
        return value.ToJsonString();
    }

    private static object? ConvertYamlNode(YamlNode node)
        => node switch
        {
            YamlMappingNode map => map.Children.ToDictionary(
                pair => GetYamlKey(pair.Key),
                pair => ConvertYamlNode(pair.Value) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            YamlSequenceNode sequence => sequence.Children.Select(item => ConvertYamlNode(item) ?? string.Empty).ToList(),
            YamlScalarNode scalar => ConvertYamlScalar(scalar),
            _ => node.ToString()
        };

    private static string GetYamlKey(YamlNode key)
        => key is YamlScalarNode scalar && scalar.Value is not null
            ? scalar.Value
            : key.ToString();

    private static object? ConvertYamlScalar(YamlScalarNode scalar)
    {
        if (scalar.Value is null)
        {
            return null;
        }

        if (bool.TryParse(scalar.Value, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(scalar.Value, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(scalar.Value, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return scalar.Value;
    }
}
