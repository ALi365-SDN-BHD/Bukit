using System.Text.Json;
using Bukit.Shared;

namespace Bukit.Content.Media;

internal sealed class MediaIndexManager
{
    private const string IndexFileName = ".media-index.json";
    private const int IndexPersistThreshold = 20;

    private readonly object _indexLock = new();
    private Dictionary<string, string> _diskIndex;
    private volatile bool _indexLoaded;
    private bool _indexDirty;
    private int _pendingIndexChanges;
    private readonly string _downloadDir;
    private readonly string _urlBase;
    private readonly ILogger? _logger;

    internal MediaIndexManager(string downloadDir, string urlBase, ILogger? logger)
    {
        _downloadDir = downloadDir;
        _urlBase = urlBase;
        _logger = logger;
        _diskIndex = new(StringComparer.Ordinal);
    }

    internal static bool IsSafeFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
               && fileName.IndexOfAny(['/', '\\']) < 0
               && !fileName.Contains("..", StringComparison.Ordinal)
               && !Path.IsPathRooted(fileName);
    }

    internal bool TryGetUrlFromIndex(string root, string normalizedKey, out string url)
    {
        url = string.Empty;
        lock (_indexLock)
        {
            if (!_diskIndex.TryGetValue(normalizedKey, out var fileName))
            {
                return false;
            }

            if (!IsSafeFileName(fileName))
            {
                _logger?.Warn(
                    $"event=media.index_path_traversal key={normalizedKey} fileName={fileName}");
                _diskIndex.Remove(normalizedKey);
                _indexDirty = true;
                return false;
            }

            var fullPath = Path.Combine(root, fileName);
            if (!File.Exists(fullPath))
            {
                _diskIndex.Remove(normalizedKey);
                _indexDirty = true;
                return false;
            }

            url = CombineUrl(fileName);
            return true;
        }
    }

    internal void RememberIndex(string normalizedKey, string fileName)
    {
        bool shouldPersist;
        lock (_indexLock)
        {
            if (_diskIndex.TryGetValue(normalizedKey, out var existing) &&
                string.Equals(existing, fileName, StringComparison.Ordinal))
            {
                return;
            }

            _diskIndex[normalizedKey] = fileName;
            _indexDirty = true;
            _pendingIndexChanges++;
            shouldPersist = _pendingIndexChanges >= IndexPersistThreshold;
        }

        if (shouldPersist)
        {
            PersistIndex();
        }
    }

    internal string? FindExistingFileByHash(string directory, string hashPrefix)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, $"{hashPrefix}.*"))
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
                JsonElement entries;

                if (rootEl.ValueKind == JsonValueKind.Object &&
                    rootEl.TryGetProperty("entries", out var e) &&
                    e.ValueKind == JsonValueKind.Object)
                {
                    entries = e;
                }
                else if (rootEl.ValueKind == JsonValueKind.Object)
                {
                    entries = rootEl;
                }
                else
                {
                    _indexLoaded = true;
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
                using var fs = File.Create(path);
                using var writer = new Utf8JsonWriter(fs,
                    new JsonWriterOptions { Indented = false });
                writer.WriteStartObject();
                writer.WriteNumber("version", 1);
                writer.WritePropertyName("entries");
                writer.WriteStartObject();
                foreach (var kv in _diskIndex.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    writer.WriteString(kv.Key, kv.Value);
                }
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.Flush();
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
