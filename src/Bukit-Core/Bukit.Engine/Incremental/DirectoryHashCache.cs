using System.Collections.Concurrent;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

internal sealed class DirectoryHashCache
{
    private readonly ConcurrentDictionary<string, string> _hashes = new(StringComparer.Ordinal);
    private readonly Func<string, string> _hashFactory;
    private readonly int _maxFiles;
    private readonly long _maxTotalSize;

    public DirectoryHashCache(
        ILogger logger,
        Func<string, string>? hashFactory = null,
        int maxFiles = 10000,
        long maxTotalSize = 100 * 1024 * 1024)
    {
        _maxFiles = maxFiles;
        _maxTotalSize = maxTotalSize;
        _hashFactory = hashFactory ?? (path => HashUtil.Sha256HexForDirectory(path, logger, _maxFiles, _maxTotalSize));
    }

    public string GetOrAdd(string directoryPath)
    {
        return _hashes.GetOrAdd(directoryPath, _hashFactory);
    }
}
