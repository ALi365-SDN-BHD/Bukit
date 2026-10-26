using System.Text;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Config;
using Bukit.Shared;
using Bukit.Engine.Plugins.BuiltIn.DataFiles;

namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class DataFilesPlugin : IBukitPlugin, IDerivePagesPlugin, IDerivePagesAsyncPlugin
{
    private const int DefaultMaxEntries = 10_000;
    private const int DefaultMaxDepth = 64;
    private const long DefaultMaxFileSizeBytes = 16 * 1024 * 1024;
    private const long DefaultMaxTotalSizeBytes = 64 * 1024 * 1024;
    private const int DefaultMaxDocumentNodes = 250_000;
    private const int DefaultMaxDocumentDepth = 64;
    private const long DefaultMaxProjectedChars = 64L * 1024 * 1024;
    private const int DefaultMaxProjectedEntries = 250_000;

    private readonly AppConfig _config;
    private readonly DataFileLimits _limits;
    private readonly Func<string, string, Stream> _openDataFile;

    internal DataFilesPlugin(
        AppConfig config,
        int maxEntries = DefaultMaxEntries,
        int maxDepth = DefaultMaxDepth,
        long maxFileSizeBytes = DefaultMaxFileSizeBytes,
        long maxTotalSizeBytes = DefaultMaxTotalSizeBytes,
        int maxDocumentNodes = DefaultMaxDocumentNodes,
        int maxDocumentDepth = DefaultMaxDocumentDepth,
        long maxProjectedChars = DefaultMaxProjectedChars,
        int maxProjectedEntries = DefaultMaxProjectedEntries,
        long? maxDecodedChars = null,
        long? maxScalarChars = null,
        Func<string, Stream>? openDataFile = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        _limits = DataFileLimits.Create(
            maxEntries,
            maxDepth,
            maxFileSizeBytes,
            maxTotalSizeBytes,
            maxDocumentNodes,
            maxDocumentDepth,
            maxProjectedChars,
            maxProjectedEntries,
            maxDecodedChars,
            maxScalarChars);
        _config = config;
        _openDataFile = openDataFile is null
            ? DataFileReaders.OpenVerifiedDataFile
            : (path, _) => openDataFile(path);
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
        if (!Directory.Exists(dataDir) || DataDirectoryWalker.IsReparsePoint(dataDir))
        {
            return Array.Empty<RoutedContentDocument>();
        }

        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var traversal = new DataTraversalState(
            _limits.MaxEntries,
            _limits.MaxDepth,
            _limits.MaxFileSizeBytes,
            _limits.MaxTotalSizeBytes,
            _limits.MaxDecodedChars,
            _openDataFile,
            cancellationToken);
        var projectionBudget = new ProjectionBudgetState(
            _limits.MaxProjectedChars,
            _limits.MaxProjectedEntries,
            cancellationToken);
        var languageDirectories = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var languages = _config.Site.Languages;
        if (languages is { Count: > 0 })
        {
            foreach (var lang in languages)
            {
                var langDir = Path.Combine(dataDir, lang);
                if (Directory.Exists(langDir) && !DataDirectoryWalker.IsReparsePoint(langDir))
                {
                    var normalizedLanguageDirectory = Path.GetFullPath(langDir);
                    if (languageDirectories.Add(normalizedLanguageDirectory))
                    {
                        traversal.VisitEntry(dataDir, langDir);
                        projectionBudget.VisitMapEntry(lang, DataDirectoryWalker.GetRelativeDataPath(dataDir, langDir));
                        result[lang] = LoadDataDirectory(
                            langDir,
                            dataDir,
                            traversal,
                            projectionBudget,
                            depth: 0);
                    }
                }
            }
        }

        var defaultData = LoadDataDirectory(
            dataDir,
            dataDir,
            traversal,
            projectionBudget,
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
            context.Set(BuildDataKeys.DataFiles, result);
        }

        return Array.Empty<RoutedContentDocument>();
    }

    private Dictionary<string, object> LoadDataDirectory(
        string dir,
        string dataRoot,
        DataTraversalState traversal,
        ProjectionBudgetState projectionBudget,
        int depth,
        IReadOnlySet<string>? excludedDirectories = null)
    {
        traversal.EnterDirectory(dir, dataRoot, depth);
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in DataDirectoryWalker.BoundedEnumerateFiles(dir, traversal))
        {
            traversal.VisitEntry(dataRoot, file);
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext is ".toml")
            {
                throw new ConfigException(
                    $"Unsupported data file format: {DataDirectoryWalker.GetRelativeDataPath(dataRoot, file)}. Supported formats are .json, .yaml, and .yml.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            if (ext is not ".yaml" and not ".yml" and not ".json")
            {
                continue;
            }

            var key = Path.GetFileNameWithoutExtension(file);
            object? data = null;
            var relativePath = DataDirectoryWalker.GetRelativeDataPath(dataRoot, file);

            try
            {
                using var content = traversal.OpenDataFileReader(dataRoot, file);
                var documentTraversal = CreateDocumentTraversal(
                    relativePath,
                    traversal.CancellationToken,
                    projectionBudget);

                if (ext is ".json")
                {
                    data = DataJsonProjector.Parse(content, documentTraversal, _limits.MaxDocumentDepth);
                }
                else
                {
                    data = DataYamlProjector.Parse(content, documentTraversal);
                }
            }
            catch (DecoderFallbackException ex)
            {
                throw new ConfigException(
                    $"Malformed data file encoding at {relativePath}.",
                    ex,
                    DiagnosticCode.ConfigInvalidValue);
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
                projectionBudget.VisitMapEntry(key, relativePath);
                AddUnique(result, key, data, relativePath);
            }
        }

        foreach (var subDir in DataDirectoryWalker.BoundedEnumerateDirectories(dir, dataRoot, traversal, excludedDirectories))
        {
            traversal.VisitEntry(dataRoot, subDir);
            var subName = Path.GetFileName(subDir);
            var subData = LoadDataDirectory(
                subDir,
                dataRoot,
                traversal,
                projectionBudget,
                depth + 1);
            if (subData.Count > 0)
            {
                var relativePath = DataDirectoryWalker.GetRelativeDataPath(dataRoot, subDir);
                projectionBudget.VisitMapEntry(subName, relativePath);
                AddUnique(result, subName, subData, relativePath);
            }
        }

        return result;
    }

    private DocumentTraversalState CreateDocumentTraversal(
        string relativePath,
        CancellationToken cancellationToken,
        ProjectionBudgetState projectionBudget)
        => new(
            _limits.MaxDocumentNodes,
            _limits.MaxDocumentDepth,
            _limits.MaxScalarChars,
            projectionBudget,
            relativePath,
            cancellationToken);

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
}
