using System.Text.Json;

namespace Bukit.Engine;

// Registration is short-lived; independent builds never hold the registry gate while building.
internal sealed class BuildResourceLease : IDisposable
{
    internal sealed record Resource(string Path, bool Directory, bool Write, bool NewAncestor = false);
    private static readonly object RegistrationGate = new();
    private static readonly string Registry = Path.Combine(Path.GetTempPath(), "bukit-build-leases-v1");
    private readonly FileStream _handle;

    private BuildResourceLease(FileStream handle) => _handle = handle;

    internal static BuildResourceLease Acquire(IReadOnlyList<Resource> resources)
    {
        lock (RegistrationGate)
        {
            Directory.CreateDirectory(Registry);
            using var gate = OpenGate();
            string? reusable = null;
            foreach (var path in Directory.EnumerateFiles(Registry, "*.lease"))
            {
                FileStream? idle = null;
                try { idle = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); reusable ??= path; }
                catch (IOException)
                {
                    // The declaration is separate so it remains readable while its lease is held.
                    using var doc = JsonDocument.Parse(File.ReadAllText(path + ".json"));
                    foreach (var entry in doc.RootElement.EnumerateArray())
                    {
                        var held = new Resource(entry.GetProperty("path").GetString()!, entry.GetProperty("directory").GetBoolean(), entry.GetProperty("write").GetBoolean());
                        if (resources.Any(request => Conflicts(request, held)))
                            throw new IOException($"Build resource is busy: {held.Path}");
                    }
                }
                finally { idle?.Dispose(); }
            }
            var leasePath = reusable ?? Path.Combine(Registry, Guid.NewGuid().ToString("N") + ".lease");
            using (var stream = File.Create(leasePath + ".json"))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                foreach (var resource in resources)
                {
                    writer.WriteStartObject();
                    writer.WriteString("path", resource.Path);
                    writer.WriteBoolean("directory", resource.Directory);
                    writer.WriteBoolean("write", resource.Write);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }
            return new BuildResourceLease(new FileStream(leasePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
        }
    }

    private static FileStream OpenGate()
    {
        // A busy registration is retried briefly; a busy build resource is never waited on.
        for (var attempt = 0; ; attempt++)
        {
            try { return new FileStream(Path.Combine(Registry, "registry.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 100) { Thread.Sleep(10); }
        }
    }

    internal static bool Conflicts(Resource a, Resource b)
        => (a.Write || b.Write) && (Same(a.Path, b.Path) || a.Directory && Contains(a.Path, b.Path) || b.Directory && Contains(b.Path, a.Path));

    internal static bool Same(string a, string b) => string.Equals(a, b, Comparison(a, b));
    internal static bool Contains(string parent, string child) => child.StartsWith(parent.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, Comparison(parent, child));

    private static StringComparison Comparison(string a, string b)
        => IsCaseInsensitive(a) || IsCaseInsensitive(b) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsCaseInsensitive(string path)
    {
        if (OperatingSystem.IsWindows()) return true;
        // Read-only capability detection: a unique existing entry with a case alias proves folding.
        for (var current = path; !string.IsNullOrEmpty(Path.GetDirectoryName(current)); current = Path.GetDirectoryName(current)!)
        {
            if (!Directory.Exists(current) && !File.Exists(current)) continue;
            var name = Path.GetFileName(current);
            var alternate = string.Concat(name.Select(c => char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c)));
            if (name == alternate) continue;
            var parent = Path.GetDirectoryName(current)!;
            if (!Directory.Exists(Path.Combine(parent, alternate)) && !File.Exists(Path.Combine(parent, alternate))) return false;
            return Directory.EnumerateFileSystemEntries(parent).Count(p => string.Equals(Path.GetFileName(p), name, StringComparison.OrdinalIgnoreCase)) == 1;
        }
        return false;
    }

    internal static string Canonical(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = new FileInfo(current);
            try
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.Directory) != 0) info = new DirectoryInfo(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    var target = info.ResolveLinkTarget(true) ?? throw new IOException($"Cannot resolve build resource: {current}");
                    current = Canonical(target.FullName);
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return Path.TrimEndingDirectorySeparator(current);
    }

    public void Dispose() => _handle.Dispose(); // Never unlink a lock inode.
}
