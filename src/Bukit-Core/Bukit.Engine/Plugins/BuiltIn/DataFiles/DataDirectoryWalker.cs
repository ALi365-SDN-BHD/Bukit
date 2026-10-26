using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Plugins.BuiltIn.DataFiles;

/// <summary>
/// Bounded, sorted enumeration of the data directory and the traversal budget
/// (entry count, depth, cycles, byte limits) shared by one data load.
/// </summary>
internal static class DataDirectoryWalker
{
    internal static bool IsReparsePoint(string path)
        => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    internal static string GetRelativeDataPath(string dataRoot, string path)
    {
        var relative = Path.GetRelativePath(dataRoot, path).Replace('\\', '/');
        return relative == "." ? "data" : $"data/{relative}";
    }

    internal static IEnumerable<string> BoundedEnumerateFiles(
        string dir, DataTraversalState traversal)
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

    internal static IEnumerable<string> BoundedEnumerateDirectories(
        string dir, string dataRoot, DataTraversalState traversal, IReadOnlySet<string>? excludedDirectories)
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
}

internal sealed class DataTraversalState
{
    private readonly int _maxEntries;
    private readonly int _maxDepth;
    private readonly long _maxFileSizeBytes;
    private readonly long _maxTotalSizeBytes;
    private readonly long _maxDecodedChars;
    private readonly Func<string, string, Stream> _openDataFile;
    private readonly CancellationToken _cancellationToken;
    private readonly HashSet<string> _visitedDirectories = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private int _entryCount;
    private long _totalSizeBytes;

    internal CancellationToken CancellationToken => _cancellationToken;
    internal int MaxEntries => _maxEntries;
    internal int RemainingEntries => Math.Max(0, _maxEntries - _entryCount);

    internal DataTraversalState(
        int maxEntries,
        int maxDepth,
        long maxFileSizeBytes,
        long maxTotalSizeBytes,
        long maxDecodedChars,
        Func<string, string, Stream> openDataFile,
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
                $"Data directory depth exceeds the maximum of {_maxDepth} at {DataDirectoryWalker.GetRelativeDataPath(dataRoot, directory)}.",
                DiagnosticCode.ConfigInvalidValue);
        }

        var normalized = Path.GetFullPath(directory);
        if (!_visitedDirectories.Add(normalized))
        {
            throw new ConfigException(
                $"Data directory cycle detected at {DataDirectoryWalker.GetRelativeDataPath(dataRoot, directory)}.",
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
                $"Data directory contains more than {_maxEntries} entries at {DataDirectoryWalker.GetRelativeDataPath(dataRoot, path)}.",
                DiagnosticCode.ConfigInvalidValue);
        }
    }

    internal TextReader OpenDataFileReader(string dataRoot, string path)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var relativePath = DataDirectoryWalker.GetRelativeDataPath(dataRoot, path);
        var input = _openDataFile(path, dataRoot);
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
            return DataFileReaders.CreateStrictTextReader(
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

    private void ThrowFileSizeLimit(string relativePath)
    {
        throw new ConfigException(
            $"Data file exceeds the maximum file size of {_maxFileSizeBytes} bytes at {relativePath}.",
            DiagnosticCode.ConfigInvalidValue);
    }
}
