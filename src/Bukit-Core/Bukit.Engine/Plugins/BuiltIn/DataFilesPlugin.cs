using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Config;
using Bukit.Shared;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class DataFilesPlugin : IBukitPlugin, IDerivePagesPlugin, IDerivePagesAsyncPlugin
{
    private const int DefaultMaxEntries = 10_000;
    private const int DefaultMaxDepth = 64;
    private const long DefaultMaxFileSizeBytes = 16 * 1024 * 1024;
    private const long DefaultMaxTotalSizeBytes = 64 * 1024 * 1024;
    private const int DefaultMaxDocumentNodes = 250_000;
    private const int DefaultMaxDocumentDepth = 64;

    private readonly AppConfig _config;
    private readonly int _maxEntries;
    private readonly int _maxDepth;
    private readonly long _maxFileSizeBytes;
    private readonly long _maxTotalSizeBytes;
    private readonly int _maxDocumentNodes;
    private readonly int _maxDocumentDepth;

    internal DataFilesPlugin(
        AppConfig config,
        int maxEntries = DefaultMaxEntries,
        int maxDepth = DefaultMaxDepth,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        long maxTotalSizeBytes = DefaultMaxTotalSizeBytes,
        int maxDocumentNodes = DefaultMaxDocumentNodes,
        int maxDocumentDepth = DefaultMaxDocumentDepth)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDocumentNodes);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDocumentDepth);
        _config = config;
        _maxEntries = maxEntries;
        _maxDepth = maxDepth;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxTotalSizeBytes = maxTotalSizeBytes;
        _maxDocumentNodes = maxDocumentNodes;
        _maxDocumentDepth = maxDocumentDepth;
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
        var traversal = new TraversalState(
            _maxEntries,
            _maxDepth,
            _maxFileSizeBytes,
            _maxTotalSizeBytes,
            cancellationToken);
        var languageDirectories = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var languages = _config.Site.Languages;
        if (languages is { Count: > 0 })
        {
            foreach (var lang in languages)
            {
                var langDir = Path.Combine(dataDir, lang);
                if (Directory.Exists(langDir) && !IsReparsePoint(langDir))
                {
                    var normalizedLanguageDirectory = Path.GetFullPath(langDir);
                    if (languageDirectories.Add(normalizedLanguageDirectory))
                    {
                        traversal.VisitEntry(dataDir, langDir);
                        result[lang] = LoadDataDirectory(
                            langDir,
                            dataDir,
                            traversal,
                            depth: 0);
                    }
                }
            }
        }

        var defaultData = LoadDataDirectory(
            dataDir,
            dataDir,
            traversal,
            depth: 0,
            excludedDirectories: languageDirectories);
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
        TraversalState traversal,
        int depth,
        IReadOnlySet<string>? excludedDirectories = null)
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
            var relativePath = GetRelativeDataPath(dataRoot, file);

            try
            {
                var content = traversal.ReadDataFile(dataRoot, file);

                if (ext is ".json")
                {
                    PreflightJsonDocument(content, relativePath, traversal.CancellationToken);
                    data = ConvertJsonNode(
                        JsonNode.Parse(
                            content,
                            documentOptions: new JsonDocumentOptions
                            {
                                MaxDepth = GetParserMaxDepth(_maxDocumentDepth)
                            }),
                        CreateDocumentTraversal(relativePath, traversal.CancellationToken),
                        depth: 0);
                }
                else
                {
                    PreflightYamlDocument(content, relativePath, traversal.CancellationToken);
                    var stream = new YamlStream();
                    stream.Load(new StringReader(content));
                    if (stream.Documents.Count > 0)
                    {
                        data = ConvertYamlNode(
                            stream.Documents[0].RootNode,
                            CreateDocumentTraversal(relativePath, traversal.CancellationToken),
                            depth: 0);
                    }
                }
            }
            catch (Exception ex) when (ex is not ConfigException and not OperationCanceledException)
            {
                throw new ConfigException(
                    $"Failed to parse data file {relativePath}.",
                    ex,
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (data is not null)
            {
                AddUnique(result, key, data, relativePath);
            }
        }

        foreach (var subDir in Directory.EnumerateDirectories(dir, "*", options)
                     .OrderBy(static path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            if (excludedDirectories?.Contains(Path.GetFullPath(subDir)) == true)
            {
                continue;
            }

            traversal.VisitEntry(dataRoot, subDir);
            var subName = Path.GetFileName(subDir);
            var subData = LoadDataDirectory(
                subDir,
                dataRoot,
                traversal,
                depth + 1);
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
        private readonly long _maxFileSizeBytes;
        private readonly long _maxTotalSizeBytes;
        private readonly CancellationToken _cancellationToken;
        private readonly HashSet<string> _visitedDirectories = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private int _entryCount;
        private long _totalSizeBytes;

        internal CancellationToken CancellationToken => _cancellationToken;

        internal TraversalState(
            int maxEntries,
            int maxDepth,
            long maxFileSizeBytes,
            long maxTotalSizeBytes,
            CancellationToken cancellationToken)
        {
            _maxEntries = maxEntries;
            _maxDepth = maxDepth;
            _maxFileSizeBytes = maxFileSizeBytes;
            _maxTotalSizeBytes = maxTotalSizeBytes;
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

        internal string ReadDataFile(string dataRoot, string path)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var relativePath = GetRelativeDataPath(dataRoot, path);
            using var input = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 8192,
                FileOptions.SequentialScan);
            if (input.Length > _maxFileSizeBytes)
            {
                ThrowFileSizeLimit(relativePath);
            }

            using var content = new MemoryStream(
                capacity: (int)Math.Min(input.Length, 8192));
            var buffer = new byte[8192];
            long fileSizeBytes = 0;
            while (true)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var read = input.Read(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    break;
                }

                fileSizeBytes += read;
                if (fileSizeBytes > _maxFileSizeBytes)
                {
                    ThrowFileSizeLimit(relativePath);
                }

                content.Write(buffer, 0, read);
            }

            if (_totalSizeBytes > _maxTotalSizeBytes - fileSizeBytes)
            {
                throw new ConfigException(
                    $"Data files exceed the total size limit of {_maxTotalSizeBytes} bytes at {relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            _totalSizeBytes += fileSizeBytes;
            content.Position = 0;
            using var reader = new StreamReader(
                content,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: false);
            return reader.ReadToEnd();
        }

        private void ThrowFileSizeLimit(string relativePath)
        {
            throw new ConfigException(
                $"Data file exceeds the maximum file size of {_maxFileSizeBytes} bytes at {relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
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

    private sealed class DocumentTraversalState
    {
        private readonly int _maxNodes;
        private readonly int _maxDepth;
        private readonly string _relativePath;
        private readonly CancellationToken _cancellationToken;
        private int _nodeCount;

        internal DocumentTraversalState(
            int maxNodes,
            int maxDepth,
            string relativePath,
            CancellationToken cancellationToken)
        {
            _maxNodes = maxNodes;
            _maxDepth = maxDepth;
            _relativePath = relativePath;
            _cancellationToken = cancellationToken;
        }

        internal void Visit(int depth)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (depth > _maxDepth)
            {
                throw new ConfigException(
                    $"Data document depth exceeds the maximum of {_maxDepth} at {_relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            _nodeCount++;
            if (_nodeCount > _maxNodes)
            {
                throw new ConfigException(
                    $"Data document contains more than {_maxNodes} nodes at {_relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }
    }

    private DocumentTraversalState CreateDocumentTraversal(
        string relativePath,
        CancellationToken cancellationToken)
        => new(
            _maxDocumentNodes,
            _maxDocumentDepth,
            relativePath,
            cancellationToken);

    private void PreflightJsonDocument(
        string content,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var traversal = CreateDocumentTraversal(relativePath, cancellationToken);
        var reader = new Utf8JsonReader(
            Encoding.UTF8.GetBytes(content),
            new JsonReaderOptions
            {
                MaxDepth = GetParserMaxDepth(_maxDocumentDepth)
            });
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                case JsonTokenType.String:
                case JsonTokenType.Number:
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    traversal.Visit(reader.CurrentDepth);
                    break;
            }
        }
    }

    private void PreflightYamlDocument(
        string content,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var traversal = CreateDocumentTraversal(relativePath, cancellationToken);
        var parser = new Parser(new StringReader(content));
        var depth = 0;
        while (parser.MoveNext())
        {
            switch (parser.Current)
            {
                case MappingStart:
                case SequenceStart:
                    traversal.Visit(depth);
                    depth++;
                    break;
                case MappingEnd:
                case SequenceEnd:
                    depth--;
                    break;
                case Scalar:
                case AnchorAlias:
                    traversal.Visit(depth);
                    break;
            }
        }
    }

    private static int GetParserMaxDepth(int maxDocumentDepth)
        => maxDocumentDepth < int.MaxValue ? maxDocumentDepth + 1 : int.MaxValue;

    private static object? ConvertJsonNode(
        JsonNode? node,
        DocumentTraversalState traversal,
        int depth)
    {
        traversal.Visit(depth);
        if (node is null)
        {
            return null;
        }

        return node switch
        {
            JsonObject obj => obj.ToDictionary(
                property => property.Key,
                property => ConvertJsonNode(property.Value, traversal, depth + 1) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            JsonArray array => array
                .Select(item => ConvertJsonNode(item, traversal, depth + 1) ?? string.Empty)
                .ToList(),
            JsonValue value => ConvertJsonValue(value),
            _ => node.ToJsonString()
        };
    }

    private static object ConvertJsonValue(JsonValue value)
    {
        if (value.TryGetValue<string>(out var text)) return text;
        if (value.TryGetValue<bool>(out var boolean)) return boolean;
        if (value.TryGetValue<long>(out var integer)) return integer;
        if (value.TryGetValue<double>(out var number)) return number;
        return value.ToJsonString();
    }

    private static object? ConvertYamlNode(
        YamlNode node,
        DocumentTraversalState traversal,
        int depth)
    {
        traversal.Visit(depth);
        return node switch
        {
            YamlMappingNode map => map.Children.ToDictionary(
                pair => GetYamlKey(pair.Key),
                pair => ConvertYamlNode(pair.Value, traversal, depth + 1) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase),
            YamlSequenceNode sequence => sequence.Children
                .Select(item => ConvertYamlNode(item, traversal, depth + 1) ?? string.Empty)
                .ToList(),
            YamlScalarNode scalar => ConvertYamlScalar(scalar),
            _ => node.ToString()
        };
    }

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
