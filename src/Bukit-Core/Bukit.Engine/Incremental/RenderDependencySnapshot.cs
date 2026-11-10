using System.Security.Cryptography;
using Bukit.Rendering;

namespace Bukit.Engine.Incremental;

/// <summary>
/// The render dependencies of one build variant: a base hash for site-wide inputs and one
/// fingerprint per shared data collection. A route's dependency hash covers the base (with its
/// route metadata and breadcrumb folded in) and only the collections that route read.
/// </summary>
internal sealed class RenderDependencySnapshot
{
    private const string Absent = "absent";
    private readonly IReadOnlyDictionary<string, Lazy<string>> _collections;
    private readonly string[] _orderedKeys;

    internal RenderDependencySnapshot(string baseHash, IReadOnlyDictionary<string, Lazy<string>> collections)
    {
        BaseHash = baseHash;
        _collections = collections;
        _orderedKeys = [.. collections.Keys.Order(StringComparer.Ordinal)];
    }

    internal string BaseHash { get; }

    internal IReadOnlyCollection<string> CollectionKeys => _orderedKeys;

    /// <summary>
    /// The dependency hash for a route whose base part is <paramref name="routeBaseHash"/> and
    /// which read <paramref name="reads"/>. Null reads (no record) cover every collection.
    /// </summary>
    internal string Combine(string routeBaseHash, IReadOnlyList<string>? reads)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var writer = new RenderDependencyHashWriter(hasher);
        writer.AppendFramedValue("base", routeBaseHash);
        if (reads is null || reads.Contains(RenderReadRecorder.All, StringComparer.Ordinal))
        {
            writer.AppendFramedValue("reads", RenderReadRecorder.All);
            foreach (var key in _orderedKeys)
            {
                AppendCollection(writer, key);
            }
        }
        else
        {
            foreach (var read in reads.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            {
                writer.AppendFramedValue("read", read);
                if (read.EndsWith(".*", StringComparison.Ordinal))
                {
                    var prefix = read[..^1];
                    foreach (var key in _orderedKeys)
                    {
                        if (key.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            AppendCollection(writer, key);
                        }
                    }
                }
                else
                {
                    // A collection that does not exist yet still counts: adding it changes the hash.
                    writer.AppendFramedValue("value", _collections.TryGetValue(read, out var value) ? value.Value : Absent);
                }
            }
        }

        return HashUtil.ToHexLower(hasher.GetHashAndReset());
    }

    private void AppendCollection(RenderDependencyHashWriter writer, string key)
    {
        writer.AppendFramedValue("collection", key);
        writer.AppendFramedValue("value", _collections[key].Value);
    }
}
