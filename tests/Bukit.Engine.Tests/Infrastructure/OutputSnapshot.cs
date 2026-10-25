using Xunit;

namespace Bukit.Engine.Tests.Infrastructure;

internal sealed class OutputSnapshot
{
    internal sealed record SnapshotEntry(string Kind, byte[] Bytes, string? Target = null);
    internal SortedDictionary<string, SnapshotEntry> Entries { get; } = new(StringComparer.Ordinal);

    public static OutputSnapshot Public(string root, params string[] languages)
    {
        var snapshot = new OutputSnapshot();
        snapshot.Capture(root, "", path => IsMetadata(path, languages), publicOnly: true);
        return snapshot;
    }

    public static OutputSnapshot Managed(params (string Name, string Path)[] roots)
    {
        var snapshot = new OutputSnapshot();
        foreach (var (name, path) in roots)
        {
            if (string.IsNullOrEmpty(name) || name.Contains('/') || snapshot.Entries.ContainsKey(name))
                throw new ArgumentException("Managed roots require unique simple names.");
            snapshot.Capture(path, name, _ => false, publicOnly: false);
        }
        return snapshot;
    }

    internal static bool IsMetadata(string path, IReadOnlyList<string> languages)
    {
        foreach (var root in new[] { "" }.Concat(languages.Select(language => language + "/")))
        {
            if (!path.StartsWith(root, StringComparison.Ordinal)) continue;
            var relative = path[root.Length..];
            if (relative.StartsWith(".bukit/", StringComparison.Ordinal) ||
                relative is ".bukit-output-marker" or ".bukit-build-state.json") return true;
        }
        return false;
    }

    private void Capture(string path, string key, Func<string, bool> exclude, bool publicOnly)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { Entries.Add(key, new("missing", [])); return; }
        catch (DirectoryNotFoundException) { Entries.Add(key, new("missing", [])); return; }

        var directory = attributes.HasFlag(FileAttributes.Directory);
        if (attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            if (publicOnly) throw new IOException($"Unexpected link in public output: {key}");
            var target = directory ? new DirectoryInfo(path).LinkTarget : new FileInfo(path).LinkTarget;
            Entries.Add(key, new(directory ? "directory-link" : "file-link", [], target));
        }
        else if (directory)
        {
            if (exclude(key.Length == 0 ? "" : key + "/")) return;
            if (!publicOnly) Entries.Add(key, new("directory", []));
            foreach (var child in Directory.EnumerateFileSystemEntries(path).Order(StringComparer.Ordinal))
            {
                var childKey = key.Length == 0 ? Path.GetFileName(child) : key + "/" + Path.GetFileName(child);
                Capture(child, childKey, exclude, publicOnly);
            }
        }
        else if (!exclude(key)) Entries.Add(key, new("file", File.ReadAllBytes(path)));
    }

    public void AssertEqual(OutputSnapshot actual)
    {
        Assert.Equal(Entries.Keys, actual.Entries.Keys);
        foreach (var (path, expected) in Entries)
        {
            var entry = actual.Entries[path];
            Assert.True(expected.Kind == entry.Kind && expected.Target == entry.Target &&
                expected.Bytes.AsSpan().SequenceEqual(entry.Bytes), $"Snapshot differs: {path}");
        }
    }
}
