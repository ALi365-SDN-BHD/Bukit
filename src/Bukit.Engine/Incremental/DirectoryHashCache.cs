using System.Collections.Concurrent;

namespace Bukit.Engine.Incremental;

internal sealed class DirectoryHashCache
{
    private readonly ConcurrentDictionary<string, string> _hashes = new(StringComparer.Ordinal);
    private readonly Func<string, string> _hashFactory;

    public DirectoryHashCache(Func<string, string>? hashFactory = null)
    {
        _hashFactory = hashFactory ?? HashUtil.Sha256HexForDirectory;
    }

    public string GetOrAdd(string directoryPath)
    {
        return _hashes.GetOrAdd(directoryPath, _hashFactory);
    }
}
