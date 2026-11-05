using System.Security.Cryptography;
using System.Text.Json;

namespace Bukit.Importing;

/// <summary>
/// Remembers which site files the last import wrote and what they contained, so a
/// re-import can update or remove files nobody edited and keep the ones that were.
/// Files the manifest does not know (older imports, hand-written files) are never
/// replaced without --overwrite.
/// </summary>
internal sealed class ImportManifest
{
    internal const string FileName = ".bukit-import.json";

    private readonly string _siteDir;
    private readonly bool _overwrite;
    private readonly Dictionary<string, string> _previous;
    private readonly Dictionary<string, string> _current = new(StringComparer.Ordinal);
    private readonly List<string> _producedAreas = [];

    private ImportManifest(string siteDir, bool overwrite, Dictionary<string, string> previous)
    {
        _siteDir = siteDir;
        _overwrite = overwrite;
        _previous = previous;
    }

    internal List<string> Warnings { get; } = [];

    internal static ImportManifest Load(string siteDir, bool overwrite)
    {
        var previous = new Dictionary<string, string>(StringComparer.Ordinal);
        var path = Path.Combine(siteDir, FileName);
        if (File.Exists(path))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (doc.RootElement.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object)
                {
                    foreach (var file in files.EnumerateObject())
                    {
                        if (file.Value.ValueKind == JsonValueKind.String && file.Value.GetString() is { } hash)
                            previous[file.Name] = hash;
                    }
                }
            }
            catch (JsonException)
            {
                // An unreadable manifest only means every existing file counts as hand-edited.
            }
        }

        return new ImportManifest(siteDir, overwrite, previous);
    }

    /// <summary>
    /// Whether the importer may write <paramref name="path"/>: it is new, --overwrite was
    /// given, or the last import wrote it and it has not changed since.
    /// </summary>
    internal bool CanWrite(string path)
    {
        if (!File.Exists(path) || _overwrite)
            return true;

        var relative = Relative(path);
        if (relative is not null && _previous.TryGetValue(relative, out var hash))
        {
            if (hash == Hash(path))
                return true;

            // Still ours; keep the old hash so later imports also see the edit.
            _current[relative] = hash;
            Warnings.Add($"Kept '{relative}': it was edited after the last import. Use --overwrite to replace it with the newly imported version.");
            return false;
        }

        Warnings.Add($"Kept existing '{relative ?? path}': the importer did not write it. Use --overwrite to replace it.");
        return false;
    }

    /// <summary>
    /// Marks a file or directory this import regenerated in full. Only there may files
    /// from the last import that were not written again be removed; content left alone
    /// (--no-extract-content, --build-source notion) keeps its files.
    /// </summary>
    internal void Produced(string path)
    {
        if (Relative(path) is { } relative)
            _producedAreas.Add(relative);
    }

    /// <summary>Records a file the importer just wrote.</summary>
    internal void Record(string path)
    {
        if (Relative(path) is { } relative && File.Exists(path))
            _current[relative] = Hash(path);
    }

    /// <summary>
    /// Removes files the last import wrote that this import no longer produces (a page
    /// deleted from the demo), unless they were edited, then saves the manifest. With
    /// <paramref name="cleanUp"/> false (a failed import) nothing is removed, but the
    /// files written so far are still recorded.
    /// </summary>
    internal void Save(bool cleanUp = true)
    {
        foreach (var (relative, hash) in _previous)
        {
            if (_current.ContainsKey(relative))
                continue;

            if (!cleanUp || !InProducedArea(relative))
            {
                _current[relative] = hash;
                continue;
            }

            var full = Path.GetFullPath(Path.Combine(_siteDir, relative));
            if (Relative(full) != relative || !File.Exists(full))
                continue;

            if (Hash(full) == hash)
            {
                File.Delete(full);
                Warnings.Add($"Removed '{relative}': the demo no longer produces it.");
            }
            else
            {
                _current[relative] = hash;
                Warnings.Add($"Kept '{relative}': the demo no longer produces it, but it was edited after the last import. Delete it if it is no longer needed.");
            }
        }

        var path = Path.Combine(_siteDir, FileName);
        Directory.CreateDirectory(_siteDir);
        var ordered = _current.OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        File.WriteAllText(path, JsonSerializer.Serialize(new { version = 1, files = ordered },
            new JsonSerializerOptions { WriteIndented = true }));
    }

    private bool InProducedArea(string relative)
        => _producedAreas.Any(area => relative == area || relative.StartsWith(area + "/", StringComparison.Ordinal));

    // Site-relative path with forward slashes, or null for paths outside the site.
    private string? Relative(string path)
    {
        var full = Path.GetFullPath(path);
        if (!ImportWriteGuard.IsInside(full, _siteDir))
            return null;

        var relative = Path.GetRelativePath(_siteDir, full).Replace('\\', '/');
        return relative is "." || relative.StartsWith("../", StringComparison.Ordinal) ? null : relative;
    }

    private static string Hash(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
