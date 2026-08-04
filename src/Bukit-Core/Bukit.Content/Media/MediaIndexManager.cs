using System.Diagnostics;
using System.Text.Json;
using Bukit.Shared;

namespace Bukit.Content.Media;

internal sealed class MediaIndexManager
{
    private const string IndexFileName = ".media-index.json";
    private const int CurrentIndexVersion = 3;
    private const int IndexPersistThreshold = 20;

    // In-process coordination keyed by the physical index path so that two
    // instances writing the same directory serialize their merge-and-persist.
    // Gates are reference-counted leases: the last holder removes the entry
    // conditionally on key/value identity so a later lease is never dropped.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PathGate> s_indexPathLocks =
        new(StringComparer.Ordinal);

    internal sealed class PathGate
    {
        internal int References;
    }

    internal static PathGate AcquirePathGate(string pathKey)
    {
        while (true)
        {
            var gate = s_indexPathLocks.GetOrAdd(pathKey, static _ => new PathGate());
            lock (gate)
            {
                if (ReferenceEquals(s_indexPathLocks.GetValueOrDefault(pathKey), gate))
                {
                    gate.References++;
                    return gate;
                }
            }
        }
    }

    internal static void ReleasePathGate(string pathKey, PathGate gate)
    {
        lock (gate)
        {
            gate.References--;
            if (gate.References <= 0)
            {
                // Conditional key/value removal: never delete a different gate instance
                // that a later acquirer installed for the same key.
                s_indexPathLocks.TryRemove(new KeyValuePair<string, PathGate>(pathKey, gate));
            }
        }
    }

    internal static bool HasPathGate(string pathKey) => s_indexPathLocks.ContainsKey(pathKey);

    private readonly object _indexLock = new();
    private Dictionary<string, string> _diskIndex;
    private readonly Dictionary<string, string> _upsertedIndexEntries = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deletedIndexKeys = new(StringComparer.Ordinal);
    private volatile bool _indexLoaded;
    private bool _indexDirty;
    private int _pendingIndexChanges;
    private readonly string _downloadDir;
    private readonly string _urlBase;
    private readonly ILogger? _logger;
    private readonly Action? _beforeIndexReplace;

    internal MediaIndexManager(string downloadDir, string urlBase, ILogger? logger)
        : this(downloadDir, urlBase, logger, beforeIndexReplace: null)
    {
    }

    internal MediaIndexManager(
        string downloadDir,
        string urlBase,
        ILogger? logger,
        Action? beforeIndexReplace)
    {
        _downloadDir = downloadDir;
        _urlBase = urlBase;
        _logger = logger;
        _beforeIndexReplace = beforeIndexReplace;
        _diskIndex = new(StringComparer.Ordinal);
    }

    internal static bool IsSafeFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
               && fileName.IndexOfAny(['/', '\\']) < 0
               && !fileName.Contains("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(fileName);
    }

    internal bool TryGetFileNameFromIndex(string root, string normalizedKey, out string fileName)
    {
        fileName = string.Empty;
        lock (_indexLock)
        {
            if (!_diskIndex.TryGetValue(normalizedKey, out var indexedFileName))
            {
                return false;
            }

            if (!IsSafeFileName(indexedFileName))
            {
                _logger?.Warn(
                    $"event=media.index_path_traversal key={normalizedKey} fileName={indexedFileName}");
                MarkIndexDeleted(normalizedKey);
                return false;
            }

            var fullPath = Path.Combine(root, indexedFileName);
            if (!File.Exists(fullPath))
            {
                MarkIndexDeleted(normalizedKey);
                return false;
            }

            fileName = indexedFileName;
            return true;
        }
    }

    internal void ForgetIndex(string normalizedKey)
    {
        lock (_indexLock)
        {
            if (_diskIndex.ContainsKey(normalizedKey))
            {
                MarkIndexDeleted(normalizedKey);
            }
        }
    }

    internal void RememberIndex(string normalizedKey, string fileName)
    {
        bool shouldPersist;
        lock (_indexLock)
        {
            _diskIndex[normalizedKey] = fileName;
            _upsertedIndexEntries[normalizedKey] = fileName;
            _deletedIndexKeys.Remove(normalizedKey);
            _indexDirty = true;
            _pendingIndexChanges++;
            shouldPersist = _pendingIndexChanges >= IndexPersistThreshold;
        }

        if (shouldPersist)
        {
            PersistIndex();
        }
    }

    internal string? FindExistingFileByIdentity(string directory, string fileIdentity)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, $"{fileIdentity}.*"))
            {
                var name = Path.GetFileName(file);
                if (!name.StartsWith('.') &&
                    ImageAssetLocalizer.AllowedExtensions.Contains(Path.GetExtension(name)))
                {
                    return name;
                }
            }
        }
        catch (DirectoryNotFoundException)
        {
        }

        return null;
    }

    internal void EnsureIndexLoaded(string root)
    {
        if (_indexLoaded)
        {
            return;
        }

        lock (_indexLock)
        {
            if (_indexLoaded)
            {
                return;
            }

            var path = Path.Combine(root, IndexFileName);
            if (!File.Exists(path))
            {
                _indexLoaded = true;
                return;
            }

            try
            {
                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                var rootEl = doc.RootElement;
                if (rootEl.ValueKind != JsonValueKind.Object ||
                    !rootEl.TryGetProperty("version", out var versionElement) ||
                    !versionElement.TryGetInt32(out var version) ||
                    version != CurrentIndexVersion ||
                    !rootEl.TryGetProperty("entries", out var entries) ||
                    entries.ValueKind != JsonValueKind.Object)
                {
                    _diskIndex.Clear();
                    _indexDirty = true;
                    return;
                }

                foreach (var prop in entries.EnumerateObject())
                {
                    if (prop.Value.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var v = prop.Value.GetString();
                    if (string.IsNullOrWhiteSpace(v))
                    {
                        continue;
                    }

                    var trimmed = v.Trim();
                    if (!IsSafeFileName(trimmed))
                    {
                        _logger?.Warn(
                            $"event=media.index_unsafe_entry key={prop.Name} value={trimmed}");
                        _indexDirty = true;
                        continue;
                    }

                    _diskIndex[prop.Name] = trimmed;
                }
            }
            catch (Exception ex)
            {
                _logger?.Warn(
                    $"event=media.index_corrupt path={path} error={ex.GetType().Name}");
                _diskIndex = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            finally
            {
                _indexLoaded = true;
            }
        }
    }

    internal void PersistIndex()
    {
        var root = (_downloadDir ?? string.Empty).Trim();
        if (root.Length == 0)
        {
            return;
        }

        lock (_indexLock)
        {
            if (!_indexLoaded || !_indexDirty)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(root);
                var path = Path.Combine(root, IndexFileName);
                var pathKey = Path.GetFullPath(path);
                var pathGate = AcquirePathGate(pathKey);

                try
                {
                    lock (pathGate)
                    {
                        // Cross-process coordination: hold an exclusive lock file while
                        // re-reading, merging, and atomically replacing the index.
                        var lockPath = path + ".lock";
                        using (OpenLockFileWithRetry(lockPath))
                        {
                            // Merge entries committed by other instances/processes
                            var merged = ReadDiskIndex(path);
                            foreach (var deletedKey in _deletedIndexKeys)
                            {
                                merged.Remove(deletedKey);
                            }

                            foreach (var kv in _upsertedIndexEntries)
                            {
                                merged[kv.Key] = kv.Value;
                            }

                            var tempPath = Path.Combine(root, $".{IndexFileName}.{Guid.NewGuid():N}.tmp");
                            try
                            {
                                using (var fs = new FileStream(
                                    tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                    bufferSize: 4096, FileOptions.SequentialScan))
                                {
                                    using var writer = new Utf8JsonWriter(fs,
                                        new JsonWriterOptions { Indented = false });
                                    writer.WriteStartObject();
                                    writer.WriteNumber("version", CurrentIndexVersion);
                                    writer.WritePropertyName("entries");
                                    writer.WriteStartObject();
                                    foreach (var kv in merged.OrderBy(x => x.Key, StringComparer.Ordinal))
                                    {
                                        writer.WriteString(kv.Key, kv.Value);
                                    }
                                    writer.WriteEndObject();
                                    writer.WriteEndObject();
                                    writer.Flush();
                                    fs.Flush(flushToDisk: true);
                                }

                                _beforeIndexReplace?.Invoke();
                                if (File.Exists(path))
                                {
                                    File.Replace(tempPath, path, destinationBackupFileName: null);
                                }
                                else
                                {
                                    File.Move(tempPath, path);
                                }
                            }
                            catch
                            {
                                try { File.Delete(tempPath); } catch { /* best effort */ }
                                throw;
                            }

                            // Reconcile this instance's in-memory map with what was committed
                            _diskIndex = merged;
                            _upsertedIndexEntries.Clear();
                            _deletedIndexKeys.Clear();
                        }
                    }
                }
                finally
                {
                    ReleasePathGate(pathKey, pathGate);
                }

                _indexDirty = false;
                _pendingIndexChanges = 0;
            }
            catch (Exception ex)
            {
                _logger?.Warn(
                    $"event=media.index_write_failed error={ex.GetType().Name}");
            }
        }
    }

    private void MarkIndexDeleted(string normalizedKey)
    {
        _diskIndex.Remove(normalizedKey);
        _upsertedIndexEntries.Remove(normalizedKey);
        _deletedIndexKeys.Add(normalizedKey);
        _indexDirty = true;
        _pendingIndexChanges++;
    }

    private static Dictionary<string, string> ReadDiskIndex(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            var rootEl = doc.RootElement;
            if (rootEl.ValueKind != JsonValueKind.Object ||
                !rootEl.TryGetProperty("version", out var versionElement) ||
                !versionElement.TryGetInt32(out var version) ||
                version != CurrentIndexVersion ||
                !rootEl.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var prop in entries.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var v = prop.Value.GetString();
                if (string.IsNullOrWhiteSpace(v))
                {
                    continue;
                }

                var trimmed = v.Trim();
                if (IsSafeFileName(trimmed))
                {
                    result[prop.Name] = trimmed;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or unreadable index: start from this instance's map only
        }

        return result;
    }

    private static FileStream OpenLockFileWithRetry(string lockPath)
    {
        var deadline = TimeSpan.FromSeconds(5);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException) when (stopwatch.Elapsed < deadline)
            {
                Thread.Sleep(25);
            }
        }
    }

    internal string CombineUrl(string fileName)
    {
        var trimmedBase = (_urlBase ?? string.Empty).Trim();
        if (trimmedBase.Length == 0)
        {
            return "/" + fileName;
        }

        if (!trimmedBase.StartsWith('/'))
        {
            trimmedBase = "/" + trimmedBase;
        }

        return $"{trimmedBase.TrimEnd('/')}/{fileName}";
    }
}
