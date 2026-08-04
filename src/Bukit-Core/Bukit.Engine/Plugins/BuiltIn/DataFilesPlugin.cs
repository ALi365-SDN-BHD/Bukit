using System.Globalization;
using System.Text;
using System.Text.Json;
using Bukit.Content;
using Bukit.Engine.Abstractions.Content;
using Bukit.Routing;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Config;
using Bukit.Shared;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

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
    private readonly int _maxEntries;
    private readonly int _maxDepth;
    private readonly long _maxFileSizeBytes;
    private readonly long _maxTotalSizeBytes;
    private readonly int _maxDocumentNodes;
    private readonly int _maxDocumentDepth;
    private readonly long _maxScalarChars;
    private readonly long _maxProjectedChars;
    private readonly int _maxProjectedEntries;
    private readonly long _maxDecodedChars;
    private readonly Func<string, Stream> _openDataFile;

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
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDocumentNodes);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDocumentDepth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxProjectedChars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxProjectedEntries);
        var resolvedMaxDecodedChars = maxDecodedChars
            ?? Math.Min(maxFileSizeBytes, int.MaxValue);
        var resolvedMaxScalarChars = maxScalarChars ?? resolvedMaxDecodedChars;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolvedMaxDecodedChars);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resolvedMaxScalarChars);
        if (resolvedMaxScalarChars > resolvedMaxDecodedChars)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxScalarChars),
                "The scalar character limit cannot exceed the decoded character limit.");
        }

        _config = config;
        _maxEntries = maxEntries;
        _maxDepth = maxDepth;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxTotalSizeBytes = maxTotalSizeBytes;
        _maxDocumentNodes = maxDocumentNodes;
        _maxDocumentDepth = maxDocumentDepth;
        _maxDecodedChars = resolvedMaxDecodedChars;
        _maxScalarChars = resolvedMaxScalarChars;
        _maxProjectedChars = maxProjectedChars;
        _maxProjectedEntries = maxProjectedEntries;
        _openDataFile = openDataFile ?? OpenFileForSequentialRead;
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
            _maxDecodedChars,
            _openDataFile,
            cancellationToken);
        var projectionBudget = new ProjectionBudgetState(
            _maxProjectedChars,
            _maxProjectedEntries,
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
                        projectionBudget.VisitMapEntry(lang, GetRelativeDataPath(dataDir, langDir));
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
            context.Data["__data_files"] = result;
        }

        return Array.Empty<RoutedContentDocument>();
    }

    private static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static Stream OpenFileForSequentialRead(string path)
        => new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            FileOptions.SequentialScan);

    private Dictionary<string, object> LoadDataDirectory(
        string dir,
        string dataRoot,
        TraversalState traversal,
        ProjectionBudgetState projectionBudget,
        int depth,
        IReadOnlySet<string>? excludedDirectories = null)
    {
        traversal.EnterDirectory(dir, dataRoot, depth);
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in BoundedEnumerateFiles(dir, traversal))
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
                using var content = traversal.OpenDataFileReader(dataRoot, file);
                var documentTraversal = CreateDocumentTraversal(
                    relativePath,
                    traversal.CancellationToken,
                    projectionBudget);

                if (ext is ".json")
                {
                    data = ParseJsonDocument(content, documentTraversal);
                }
                else
                {
                    data = ParseYamlDocument(content, documentTraversal);
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

        foreach (var subDir in BoundedEnumerateDirectories(dir, dataRoot, traversal, excludedDirectories))
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
                var relativePath = GetRelativeDataPath(dataRoot, subDir);
                projectionBudget.VisitMapEntry(subName, relativePath);
                AddUnique(result, subName, subData, relativePath);
            }
        }

        return result;
    }

    private static IEnumerable<string> BoundedEnumerateFiles(
        string dir, TraversalState traversal)
    {
        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        return TakeBoundedSortedWithinEntryBudget(
            Directory.EnumerateFiles(dir, "*", options),
            traversal.RemainingEntries,
            traversal.MaxEntries);
    }

    private static IEnumerable<string> BoundedEnumerateDirectories(
        string dir, string dataRoot, TraversalState traversal, IReadOnlySet<string>? excludedDirectories)
    {
        var options = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false
        };

        var directories = Directory.EnumerateDirectories(dir, "*", options)
            .Where(subDir => excludedDirectories?.Contains(Path.GetFullPath(subDir)) != true);
        return TakeBoundedSortedWithinEntryBudget(
            directories,
            traversal.RemainingEntries,
            traversal.MaxEntries);
    }

    internal static IReadOnlyList<string> TakeBoundedSortedWithinEntryBudget(
        IEnumerable<string> entries,
        int remainingEntries,
        int maxEntries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(remainingEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEntries);
        var probeLimit = remainingEntries == int.MaxValue
            ? int.MaxValue
            : remainingEntries + 1;
        var bounded = TakeBoundedSorted(entries, probeLimit);
        if (bounded.Count > remainingEntries)
        {
            throw new ConfigException(
                $"Data directory contains more than {maxEntries} entries.",
                DiagnosticCode.ConfigInvalidValue);
        }

        return bounded;
    }

    internal static IReadOnlyList<string> TakeBoundedSorted(
        IEnumerable<string> entries,
        int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        var bounded = new List<string>(Math.Min(limit, 64));
        foreach (var entry in entries)
        {
            bounded.Add(entry);
            if (bounded.Count == limit)
            {
                break;
            }
        }

        bounded.Sort(static (left, right) => string.Compare(
            Path.GetFileName(left),
            Path.GetFileName(right),
            StringComparison.Ordinal));
        return bounded;
    }

    private sealed class TraversalState
    {
        private readonly int _maxEntries;
        private readonly int _maxDepth;
        private readonly long _maxFileSizeBytes;
        private readonly long _maxTotalSizeBytes;
        private readonly long _maxDecodedChars;
        private readonly Func<string, Stream> _openDataFile;
        private readonly CancellationToken _cancellationToken;
        private readonly HashSet<string> _visitedDirectories = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private int _entryCount;
        private long _totalSizeBytes;

        internal CancellationToken CancellationToken => _cancellationToken;
        internal int MaxEntries => _maxEntries;
        internal int RemainingEntries => Math.Max(0, _maxEntries - _entryCount);

        internal TraversalState(
            int maxEntries,
            int maxDepth,
            long maxFileSizeBytes,
            long maxTotalSizeBytes,
            long maxDecodedChars,
            Func<string, Stream> openDataFile,
            CancellationToken cancellationToken)
        {
            _maxEntries = maxEntries;
            _maxDepth = maxDepth;
            _maxFileSizeBytes = maxFileSizeBytes;
            _maxTotalSizeBytes = maxTotalSizeBytes;
            _maxDecodedChars = maxDecodedChars;
            _openDataFile = openDataFile;
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

        internal TextReader OpenDataFileReader(string dataRoot, string path)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            var relativePath = GetRelativeDataPath(dataRoot, path);
            var input = _openDataFile(path);
            try
            {
                if (input.CanSeek && input.Length > _maxFileSizeBytes)
                {
                    ThrowFileSizeLimit(relativePath);
                }

                long fileSizeBytes = 0;
                var boundedInput = new BudgetedReadStream(
                    input,
                    bytesRead =>
                    {
                        _cancellationToken.ThrowIfCancellationRequested();
                        if (fileSizeBytes > _maxFileSizeBytes - bytesRead)
                        {
                            ThrowFileSizeLimit(relativePath);
                        }

                        if (_totalSizeBytes > _maxTotalSizeBytes - bytesRead)
                        {
                            throw new ConfigException(
                                $"Data files exceed the total size limit of {_maxTotalSizeBytes} bytes at {relativePath}.",
                                DiagnosticCode.ConfigInvalidValue);
                        }

                        fileSizeBytes += bytesRead;
                        _totalSizeBytes += bytesRead;
                    });
                return CreateStrictTextReader(
                    boundedInput,
                    _maxDecodedChars,
                    relativePath,
                    _cancellationToken);
            }
            catch
            {
                input.Dispose();
                throw;
            }
        }

        private static TextReader CreateStrictTextReader(
            Stream input,
            long maxDecodedChars,
            string relativePath,
            CancellationToken cancellationToken)
        {
            var prefix = new byte[5];
            var prefixLength = 0;
            while (prefixLength < prefix.Length)
            {
                var read = input.Read(prefix, prefixLength, prefix.Length - prefixLength);
                if (read == 0)
                {
                    break;
                }

                prefixLength += read;
            }

            Encoding encoding;
            var bomLength = 0;
            if (StartsWith(prefix, prefixLength, [0xFF, 0xFE, 0x00, 0x00])
                || StartsWith(prefix, prefixLength, [0x00, 0x00, 0xFE, 0xFF])
                || IsUtf7Bom(prefix, prefixLength))
            {
                throw new ConfigException(
                    $"Unsupported data file encoding at {relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
            else if (StartsWith(prefix, prefixLength, [0xEF, 0xBB, 0xBF]))
            {
                encoding = new UTF8Encoding(false, true);
                bomLength = 3;
            }
            else if (StartsWith(prefix, prefixLength, [0xFF, 0xFE]))
            {
                encoding = new UnicodeEncoding(false, false, true);
                bomLength = 2;
            }
            else if (StartsWith(prefix, prefixLength, [0xFE, 0xFF]))
            {
                encoding = new UnicodeEncoding(true, false, true);
                bomLength = 2;
            }
            else
            {
                encoding = new UTF8Encoding(false, true);
            }

            var prefixedInput = new PrefixReadStream(
                input,
                prefix,
                bomLength,
                prefixLength - bomLength);
            var streamReader = new StreamReader(
                prefixedInput,
                encoding,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 4096,
                leaveOpen: false);
            return new StrictBoundedTextReader(
                streamReader,
                maxDecodedChars,
                relativePath,
                cancellationToken);
        }

        private static bool StartsWith(byte[] value, int valueLength, byte[] prefix)
            => valueLength >= prefix.Length
                && value.AsSpan(0, prefix.Length).SequenceEqual(prefix);

        private static bool IsUtf7Bom(byte[] value, int valueLength)
            => valueLength >= 5
                && value[0] == 0x2B
                && value[1] == 0x2F
                && value[2] == 0x76
                && value[3] is 0x38 or 0x39 or 0x2B or 0x2F
                && value[4] == 0x2D;

        private void ThrowFileSizeLimit(string relativePath)
        {
            throw new ConfigException(
                $"Data file exceeds the maximum file size of {_maxFileSizeBytes} bytes at {relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    private sealed class BudgetedReadStream(Stream inner, Action<int> recordRead) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            recordRead(read);
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = inner.Read(buffer);
            recordRead(read);
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class PrefixReadStream(
        Stream inner,
        byte[] prefix,
        int prefixOffset,
        int prefixCount) : Stream
    {
        private int _prefixOffset = prefixOffset;
        private readonly int _prefixEnd = prefixOffset + prefixCount;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var copied = Math.Min(buffer.Length, _prefixEnd - _prefixOffset);
            if (copied > 0)
            {
                prefix.AsSpan(_prefixOffset, copied).CopyTo(buffer);
                _prefixOffset += copied;
                if (copied == buffer.Length)
                {
                    return copied;
                }
            }

            return copied + inner.Read(buffer[copied..]);
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class StrictBoundedTextReader(
        TextReader inner,
        long maxChars,
        string relativePath,
        CancellationToken cancellationToken) : TextReader
    {
        private long _chars;

        public override int Peek()
        {
            cancellationToken.ThrowIfCancellationRequested();
            return inner.Peek();
        }

        public override int Read()
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = inner.Read();
            if (value >= 0) Record(1);
            return value;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = inner.Read(buffer, index, count);
            Record(read);
            return read;
        }

        public override int Read(Span<char> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = inner.Read(buffer);
            Record(read);
            return read;
        }

        private void Record(int read)
        {
            if (_chars > maxChars - read)
            {
                throw new ConfigException(
                    $"Data file decodes to more than {maxChars} characters at {relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            _chars += read;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) inner.Dispose();
            base.Dispose(disposing);
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
        private readonly long _maxScalarChars;
        private readonly ProjectionBudgetState _projectionBudget;
        private readonly string _relativePath;
        private readonly CancellationToken _cancellationToken;
        private int _nodeCount;

        internal DocumentTraversalState(
            int maxNodes,
            int maxDepth,
            long maxScalarChars,
            ProjectionBudgetState projectionBudget,
            string relativePath,
            CancellationToken cancellationToken)
        {
            _maxNodes = maxNodes;
            _maxDepth = maxDepth;
            _maxScalarChars = maxScalarChars;
            _projectionBudget = projectionBudget;
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

        internal void VisitScalar(long length)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (length > _maxScalarChars)
            {
                throw new ConfigException(
                    $"Data document contains a scalar longer than {_maxScalarChars} characters at {_relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }
        }

        internal void VisitResultString(int length)
            => _projectionBudget.VisitString(length, _relativePath);

        internal void VisitResultEntry()
            => _projectionBudget.VisitEntry(_relativePath);
    }

    private sealed class ProjectionBudgetState
    {
        private readonly long _maxChars;
        private readonly int _maxEntries;
        private readonly CancellationToken _cancellationToken;
        private long _chars;
        private int _entries;

        internal ProjectionBudgetState(
            long maxChars,
            int maxEntries,
            CancellationToken cancellationToken)
        {
            _maxChars = maxChars;
            _maxEntries = maxEntries;
            _cancellationToken = cancellationToken;
        }

        internal void VisitString(int length, string relativePath)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_chars > _maxChars - length)
            {
                throw new ConfigException(
                    $"Data document projects to more than {_maxChars} characters at {relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            _chars += length;
        }

        internal void VisitEntry(string relativePath)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_entries >= _maxEntries)
            {
                throw new ConfigException(
                    $"Data document projects to more than {_maxEntries} collection entries at {relativePath}.",
                    DiagnosticCode.ConfigInvalidValue);
            }

            _entries++;
        }

        internal void VisitMapEntry(string key, string relativePath)
        {
            VisitEntry(relativePath);
            VisitString(key.Length, relativePath);
        }
    }

    private DocumentTraversalState CreateDocumentTraversal(
        string relativePath,
        CancellationToken cancellationToken,
        ProjectionBudgetState projectionBudget)
        => new(
            _maxDocumentNodes,
            _maxDocumentDepth,
            _maxScalarChars,
            projectionBudget,
            relativePath,
            cancellationToken);

    private static int GetParserMaxDepth(int maxDocumentDepth)
        => maxDocumentDepth < int.MaxValue ? maxDocumentDepth + 1 : int.MaxValue;

    private object? ParseJsonDocument(
        TextReader content,
        DocumentTraversalState traversal)
    {
        var builder = new JsonProjectionBuilder(traversal);
        var state = new JsonReaderState(new JsonReaderOptions
        {
            MaxDepth = GetParserMaxDepth(_maxDocumentDepth)
        });
        var chars = new char[4096];
        var bytes = new byte[16384];
        var bufferedBytes = 0;
        var isFinalBlock = false;
        var utf8 = new UTF8Encoding(false, true);
        var encoder = utf8.GetEncoder();

        while (true)
        {
            if (!isFinalBlock)
            {
                var charsRead = content.Read(chars, 0, chars.Length);
                isFinalBlock = charsRead == 0;
                EnsureCapacity(
                    ref bytes,
                    bufferedBytes + utf8.GetMaxByteCount(charsRead));
                encoder.Convert(
                    chars.AsSpan(0, charsRead),
                    bytes.AsSpan(bufferedBytes),
                    isFinalBlock,
                    out var charsUsed,
                    out var bytesUsed,
                    out _);
                if (charsUsed != charsRead)
                {
                    throw new InvalidDataException("Unable to transcode the JSON data file.");
                }

                bufferedBytes += bytesUsed;
            }

            var reader = new Utf8JsonReader(
                bytes.AsSpan(0, bufferedBytes),
                isFinalBlock,
                state);
            while (reader.Read())
            {
                builder.Accept(ref reader);
            }

            var consumed = checked((int)reader.BytesConsumed);
            state = reader.CurrentState;
            bufferedBytes -= consumed;
            if (bufferedBytes > 0 && consumed > 0)
            {
                bytes.AsSpan(consumed, bufferedBytes).CopyTo(bytes);
            }

            if (isFinalBlock)
            {
                break;
            }

            if (consumed == 0 && bufferedBytes == bytes.Length)
            {
                EnsureCapacity(ref bytes, checked(bytes.Length * 2));
            }
        }

        return builder.Complete();
    }

    private static void EnsureCapacity(ref byte[] buffer, int required)
    {
        if (required <= buffer.Length) return;
        Array.Resize(ref buffer, Math.Max(required, checked(buffer.Length * 2)));
    }

    private sealed class JsonProjectionBuilder(DocumentTraversalState traversal)
    {
        private readonly Stack<JsonContainerFrame> _containers = new();
        private bool _hasRoot;
        private object? _root;

        internal void Accept(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    traversal.Visit(reader.CurrentDepth);
                    var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    AttachValue(map);
                    _containers.Push(new JsonContainerFrame(map));
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (_containers.Count == 0) throw new JsonException();
                    _containers.Pop();
                    break;
                case JsonTokenType.StartArray:
                    traversal.Visit(reader.CurrentDepth);
                    var list = new List<object>();
                    AttachValue(list);
                    _containers.Push(new JsonContainerFrame(list));
                    break;
                case JsonTokenType.PropertyName:
                    var property = reader.GetString() ?? string.Empty;
                    if (_containers.Count == 0 || !_containers.Peek().IsMap)
                    {
                        throw new JsonException();
                    }

                    traversal.VisitScalar(property.Length);
                    traversal.VisitResultEntry();
                    traversal.VisitResultString(property.Length);
                    _containers.Peek().PendingProperty = property;
                    break;
                case JsonTokenType.String:
                    traversal.Visit(reader.CurrentDepth);
                    var text = reader.GetString() ?? string.Empty;
                    traversal.VisitScalar(text.Length);
                    AttachValue(TrackResultString(text, traversal));
                    break;
                case JsonTokenType.Number:
                    traversal.Visit(reader.CurrentDepth);
                    traversal.VisitScalar(
                        reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length);
                    AttachValue(ConvertJsonNumber(ref reader, traversal));
                    break;
                case JsonTokenType.True:
                    traversal.Visit(reader.CurrentDepth);
                    AttachValue(true);
                    break;
                case JsonTokenType.False:
                    traversal.Visit(reader.CurrentDepth);
                    AttachValue(false);
                    break;
                case JsonTokenType.Null:
                    traversal.Visit(reader.CurrentDepth);
                    AttachValue(null);
                    break;
            }
        }

        internal object? Complete()
        {
            if (!_hasRoot || _containers.Count != 0)
            {
                throw new JsonException();
            }

            return _root;
        }

        private void AttachValue(object? value)
        {
            if (_containers.Count == 0)
            {
                if (_hasRoot) throw new JsonException();
                _hasRoot = true;
                _root = value;
                return;
            }

            var parent = _containers.Peek();
            if (parent.Map is not null)
            {
                var property = parent.PendingProperty ?? throw new JsonException();
                parent.Map.Add(property, value ?? string.Empty);
                parent.PendingProperty = null;
            }
            else
            {
                traversal.VisitResultEntry();
                parent.List!.Add(value ?? string.Empty);
            }
        }
    }

    private sealed class JsonContainerFrame
    {
        internal JsonContainerFrame(Dictionary<string, object> map) => Map = map;
        internal JsonContainerFrame(List<object> list) => List = list;

        internal Dictionary<string, object>? Map { get; }
        internal List<object>? List { get; }
        internal bool IsMap => Map is not null;
        internal string? PendingProperty { get; set; }
    }

    private static object ConvertJsonNumber(
        ref Utf8JsonReader reader,
        DocumentTraversalState traversal)
    {
        if (reader.TryGetInt64(out var integer)) return integer;
        if (reader.TryGetDouble(out var number)) return number;
        return TrackResultString(Encoding.UTF8.GetString(reader.ValueSpan), traversal);
    }

    private static string TrackResultString(string text, DocumentTraversalState traversal)
    {
        traversal.VisitResultString(text.Length);
        return text;
    }

    private static object? ParseYamlDocument(
        TextReader content,
        DocumentTraversalState traversal)
    {
        var parser = new Parser(content);
        if (!parser.MoveNext() || parser.Current is not StreamStart)
        {
            throw new InvalidDataException("YAML stream start was not found.");
        }

        if (!parser.MoveNext()) throw new InvalidDataException("Unexpected end of YAML stream.");
        object? firstDocument = null;
        var hasDocument = false;
        while (parser.Current is not StreamEnd)
        {
            if (parser.Current is not DocumentStart)
            {
                throw new InvalidDataException("YAML document start was not found.");
            }

            MoveNextYaml(parser);
            var anchors = new Dictionary<string, object?>(StringComparer.Ordinal);
            var document = ParseYamlNode(
                parser,
                traversal,
                depth: 0,
                project: !hasDocument,
                anchors);
            if (parser.Current is not DocumentEnd)
            {
                throw new InvalidDataException("YAML document end was not found.");
            }

            if (!hasDocument)
            {
                firstDocument = document;
                hasDocument = true;
            }

            MoveNextYaml(parser);
        }

        return firstDocument;
    }

    private static object? ParseYamlNode(
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        bool project,
        Dictionary<string, object?> anchors)
    {
        switch (parser.Current)
        {
            case MappingStart mappingStart:
                {
                    traversal.Visit(depth);
                    var result = project
                        ? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                        : null;
                    MoveNextYaml(parser);
                    while (parser.Current is not MappingEnd)
                    {
                        var key = ParseYamlKey(parser, traversal, depth + 1, anchors);
                        if (project)
                        {
                            traversal.VisitResultEntry();
                            traversal.VisitResultString(key.Length);
                        }

                        var value = ParseYamlNode(parser, traversal, depth + 1, project, anchors);
                        result?.Add(key, value ?? string.Empty);
                    }

                    MoveNextYaml(parser);
                    RegisterYamlAnchor(mappingStart, result, anchors);
                    return result;
                }
            case SequenceStart sequenceStart:
                {
                    traversal.Visit(depth);
                    var result = project ? new List<object>() : null;
                    MoveNextYaml(parser);
                    while (parser.Current is not SequenceEnd)
                    {
                        if (project) traversal.VisitResultEntry();
                        var value = ParseYamlNode(parser, traversal, depth + 1, project, anchors);
                        result?.Add(value ?? string.Empty);
                    }

                    MoveNextYaml(parser);
                    RegisterYamlAnchor(sequenceStart, result, anchors);
                    return result;
                }
            case Scalar scalar:
                {
                    traversal.Visit(depth);
                    traversal.VisitScalar(scalar.Value?.Length ?? 0);
                    var value = project ? ConvertYamlScalar(scalar.Value, traversal) : null;
                    RegisterYamlAnchor(scalar, value, anchors);
                    MoveNextYaml(parser);
                    return value;
                }
            case AnchorAlias alias:
                {
                    traversal.Visit(depth);
                    var anchor = alias.Value.Value;
                    if (!anchors.TryGetValue(anchor, out var value))
                    {
                        throw new AnchorNotFoundException($"Anchor '{anchor}' was not found.");
                    }

                    MoveNextYaml(parser);
                    return project ? CloneYamlProjection(value, traversal) : null;
                }
            default:
                throw new InvalidDataException($"Unexpected YAML event {parser.Current?.GetType().Name}.");
        }
    }

    private static string ParseYamlKey(
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        Dictionary<string, object?> anchors)
    {
        if (parser.Current is Scalar scalar)
        {
            traversal.Visit(depth);
            var key = scalar.Value ?? string.Empty;
            traversal.VisitScalar(key.Length);
            RegisterYamlAnchor(scalar, key, anchors);
            MoveNextYaml(parser);
            return key;
        }

        if (parser.Current is AnchorAlias alias)
        {
            traversal.Visit(depth);
            var anchor = alias.Value.Value;
            if (!anchors.TryGetValue(anchor, out var value))
            {
                throw new AnchorNotFoundException($"Anchor '{anchor}' was not found.");
            }

            MoveNextYaml(parser);
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return ParseYamlComplexKey(parser, traversal, depth, anchors);
    }

    private static string ParseYamlComplexKey(
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        Dictionary<string, object?> anchors)
    {
        var result = new StringBuilder();
        AppendYamlDisplayNode(result, parser, traversal, depth, anchors);
        return result.ToString();
    }

    private static void AppendYamlDisplayNode(
        StringBuilder result,
        Parser parser,
        DocumentTraversalState traversal,
        int depth,
        Dictionary<string, object?> anchors)
    {
        switch (parser.Current)
        {
            case Scalar scalar:
                traversal.Visit(depth);
                var scalarText = scalar.Value ?? string.Empty;
                traversal.VisitScalar(scalarText.Length);
                result.Append(scalarText);
                RegisterYamlAnchor(scalar, scalarText, anchors);
                MoveNextYaml(parser);
                return;
            case SequenceStart sequenceStart:
                traversal.Visit(depth);
                result.Append("[ ");
                MoveNextYaml(parser);
                var firstItem = true;
                while (parser.Current is not SequenceEnd)
                {
                    if (!firstItem) result.Append(", ");
                    AppendYamlDisplayNode(result, parser, traversal, depth + 1, anchors);
                    firstItem = false;
                }

                result.Append(" ]");
                MoveNextYaml(parser);
                if (!sequenceStart.Anchor.IsEmpty)
                {
                    anchors.Add(sequenceStart.Anchor.Value, result.ToString());
                }

                return;
            case MappingStart mappingStart:
                traversal.Visit(depth);
                result.Append("{ ");
                MoveNextYaml(parser);
                var firstPair = true;
                while (parser.Current is not MappingEnd)
                {
                    if (!firstPair) result.Append(", ");
                    result.Append("{ ");
                    AppendYamlDisplayNode(result, parser, traversal, depth + 1, anchors);
                    result.Append(", ");
                    AppendYamlDisplayNode(result, parser, traversal, depth + 1, anchors);
                    result.Append(" }");
                    firstPair = false;
                }

                result.Append(" }");
                MoveNextYaml(parser);
                if (!mappingStart.Anchor.IsEmpty)
                {
                    anchors.Add(mappingStart.Anchor.Value, result.ToString());
                }

                return;
            case AnchorAlias alias:
                traversal.Visit(depth);
                if (!anchors.TryGetValue(alias.Value.Value, out var value))
                {
                    throw new AnchorNotFoundException($"Anchor '{alias.Value.Value}' was not found.");
                }

                AppendYamlDisplayValue(result, value);
                MoveNextYaml(parser);
                return;
            default:
                throw new InvalidDataException($"Unexpected YAML event {parser.Current?.GetType().Name}.");
        }
    }

    private static void AppendYamlDisplayValue(StringBuilder result, object? value)
    {
        switch (value)
        {
            case Dictionary<string, object> map:
                result.Append("{ ");
                var firstPair = true;
                foreach (var (key, item) in map)
                {
                    if (!firstPair) result.Append(", ");
                    result.Append("{ ").Append(key).Append(", ");
                    AppendYamlDisplayValue(result, item);
                    result.Append(" }");
                    firstPair = false;
                }

                result.Append(" }");
                break;
            case List<object> list:
                result.Append("[ ");
                for (var index = 0; index < list.Count; index++)
                {
                    if (index > 0) result.Append(", ");
                    AppendYamlDisplayValue(result, list[index]);
                }

                result.Append(" ]");
                break;
            default:
                result.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static void RegisterYamlAnchor(
        NodeEvent node,
        object? value,
        Dictionary<string, object?> anchors)
    {
        if (!node.Anchor.IsEmpty) anchors.Add(node.Anchor.Value, value);
    }

    private static object? CloneYamlProjection(
        object? value,
        DocumentTraversalState traversal)
    {
        switch (value)
        {
            case Dictionary<string, object> map:
                var mapClone = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var (key, item) in map)
                {
                    traversal.VisitResultEntry();
                    traversal.VisitResultString(key.Length);
                    mapClone.Add(key, CloneYamlProjection(item, traversal) ?? string.Empty);
                }

                return mapClone;
            case List<object> list:
                var listClone = new List<object>(list.Count);
                foreach (var item in list)
                {
                    traversal.VisitResultEntry();
                    listClone.Add(CloneYamlProjection(item, traversal) ?? string.Empty);
                }

                return listClone;
            case string text:
                return TrackResultString(text, traversal);
            default:
                return value;
        }
    }

    private static void MoveNextYaml(Parser parser)
    {
        if (!parser.MoveNext())
        {
            throw new InvalidDataException("Unexpected end of YAML stream.");
        }
    }

    private static object? ConvertYamlScalar(
        string? value,
        DocumentTraversalState traversal)
    {
        if (value is null)
        {
            return null;
        }

        if (bool.TryParse(value, out var boolean))
        {
            return boolean;
        }

        if (long.TryParse(value, CultureInfo.InvariantCulture, out var integer))
        {
            return integer;
        }

        if (double.TryParse(value, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        return TrackResultString(value, traversal);
    }
}
