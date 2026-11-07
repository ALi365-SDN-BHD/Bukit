using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed class BuildTransactionJournal
{
    internal sealed record Entry(string Path, string Stage, string Backup, bool Directory)
    {
        internal int State { get; set; }
        internal bool HadOriginal { get; set; }
        internal string? Fingerprint { get; set; }
    }

    private readonly string _path;
    private readonly List<Entry> _entries;
    private readonly int _processId;
    private bool _committed;

    private BuildTransactionJournal(string path, List<Entry> entries, int processId)
        => (_path, _entries, _processId) = (path, entries, processId);

    internal bool IsUnstarted => !_committed && _entries.All(entry => entry.State == 0);

    internal void Add(Entry entry)
    {
        _entries.Add(entry);
        Save();
    }

    internal void EnsureEntries(IReadOnlyList<Entry> entries)
    {
        if (_entries.Count > entries.Count || _entries.Where((entry, index) => entry != entries[index]).Any())
            throw new IOException("Build transaction targets changed after staging.");
        for (var index = _entries.Count; index < entries.Count; index++) _entries.Add(entries[index]);
        Save();
    }

    internal static string PathFor(string root, string output)
    {
        var identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(BuildResourceLease.Canonical(output))));
        var repository = FindGitAdminDirectory(root);
        var directory = repository is null
            ? BuildResourceLease.Canonical(root)
            : System.IO.Path.Combine(repository, "bukit-build-transactions-v1");
        return System.IO.Path.Combine(directory, ".bukit-transaction-" + identity + ".json");
    }

    internal static bool HasPending(string root, string output)
    {
        var path = PathFor(root, output);
        return ExistsOrLink(path) || ExistsOrLink(LegacyPath(root, output));
    }

    internal static BuildTransactionJournal Create(string root, string output, IEnumerable<Entry> entries, int? processId = null)
    {
        if (HasPending(root, output))
            throw new IOException("A build transaction journal already exists; recover it before starting another build.");
        var journal = new BuildTransactionJournal(PathFor(root, output), entries.ToList(), processId ?? Environment.ProcessId);
        var directory = System.IO.Path.GetDirectoryName(journal._path)!;
        System.IO.Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Build transaction directory is a symbolic link: {directory}");
        journal.Save(replace: false);
        return journal;
    }

    internal void BeforeMove(int index, bool hadOriginal)
    {
        _entries[index].HadOriginal = hadOriginal;
        _entries[index].Fingerprint = Fingerprint(_entries[index].Stage, _entries[index].Directory);
        _entries[index].State = 1;
        Save();
    }

    internal void Complete()
    {
        _committed = true;
        Save();
    }

    internal void Remove() => File.Delete(_path);

    private void Save(bool replace = true)
    {
        var temp = _path + ".pending-" + Guid.NewGuid().ToString("N") + ".tmp";
        var owned = false;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                owned = true;
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("version", 1);
                    writer.WriteNumber("pid", _processId);
                    writer.WriteBoolean("committed", _committed);
                    writer.WriteStartArray("entries");
                    foreach (var entry in _entries)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("path", entry.Path);
                        writer.WriteString("stage", entry.Stage);
                        writer.WriteString("backup", entry.Backup);
                        writer.WriteBoolean("directory", entry.Directory);
                        writer.WriteNumber("state", entry.State);
                        writer.WriteBoolean("hadOriginal", entry.HadOriginal);
                        writer.WriteString("fingerprint", entry.Fingerprint);
                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, _path, overwrite: replace);
            owned = false;
        }
        finally { if (owned) File.Delete(temp); }
    }

    internal static void Recover(string root, string output, IReadOnlyList<(string Path, bool Directory)> allowed, string? worktreeRunsRoot = null, ILogger? logger = null)
    {
        var journalPath = PathFor(root, output);
        var legacyPath = LegacyPath(root, output);
        if (!BuildResourceLease.Same(journalPath, legacyPath) && ExistsOrLink(legacyPath))
        {
            if (ExistsOrLink(journalPath))
                throw new IOException("Multiple build transaction journals require manual inspection.");
            journalPath = legacyPath;
        }
        if (new FileInfo(journalPath).LinkTarget is not null)
            throw new IOException($"Build transaction journal requires manual inspection: {journalPath}");
        if (!File.Exists(journalPath)) return;
        worktreeRunsRoot ??= FindWorktreeRunsRoot(root);
        var (committed, entries) = ReadEntries(journalPath, root, allowed, worktreeRunsRoot);
        ValidateRecoveryState(entries, committed);
        foreach (var entry in entries.AsEnumerable().Reverse())
            RestoreEntry(entry, committed, logger);
        try { File.Delete(journalPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new IOException($"Build transaction journal could not be removed: {journalPath}", ex); }
    }

    private static (bool Committed, List<Entry> Entries) ReadEntries(
        string journalPath,
        string root,
        IReadOnlyList<(string Path, bool Directory)> allowed,
        string? worktreeRunsRoot)
    {
        try
        {
            using var json = JsonDocument.Parse(File.ReadAllBytes(journalPath));
            var document = json.RootElement;
            if (document.GetProperty("version").GetInt32() != 1)
                throw new IOException($"Unsupported build transaction journal: {journalPath}");
            var committed = document.GetProperty("committed").GetBoolean();
            var entries = new List<Entry>();
            foreach (var item in document.GetProperty("entries").EnumerateArray())
                entries.Add(ReadEntry(item, journalPath, root, allowed, worktreeRunsRoot));
            return (committed, entries);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new IOException($"Build transaction journal is invalid; preserve it for inspection: {journalPath}", ex);
        }
    }

    private static Entry ReadEntry(
        JsonElement item,
        string journalPath,
        string root,
        IReadOnlyList<(string Path, bool Directory)> allowed,
        string? worktreeRunsRoot)
    {
        var path = item.GetProperty("path").GetString()!;
        var stage = item.GetProperty("stage").GetString()!;
        var backup = item.GetProperty("backup").GetString()!;
        var directory = item.GetProperty("directory").GetBoolean();
        var state = item.GetProperty("state").GetInt32();
        var hadOriginal = item.GetProperty("hadOriginal").GetBoolean();
        var enginePaths = SafeSibling(path, stage, "-stage") && SafeSibling(path, backup, "-backup");
        var worktreePaths = directory
            ? SafeWorktreeSibling(path, stage, "staging") && SafeWorktreeSibling(path, backup, "backup")
            : worktreeRunsRoot is not null && SafeMetricsStage(stage, worktreeRunsRoot) && SafeMetricsBackup(path, backup);
        var allowedTarget = allowed.Any(x => x.Directory == directory && BuildResourceLease.Same(x.Path, path));
        var safeNewAncestor = directory && !hadOriginal &&
            BuildResourceLease.Same(path, BuildResourceLease.Canonical(path)) &&
            BuildResourceLease.Contains(BuildResourceLease.Canonical(root), path) &&
            allowed.Any(x => BuildResourceLease.Contains(path, x.Path));
        if (!(allowedTarget || safeNewAncestor) ||
            !(enginePaths || worktreePaths) || state is < 0 or > 1)
            throw new IOException($"Unsafe build transaction journal: {journalPath}");
        return new Entry(path, stage, backup, directory)
        {
            State = state,
            HadOriginal = hadOriginal,
            Fingerprint = item.TryGetProperty("fingerprint", out var fingerprint) ? fingerprint.GetString() : null
        };
    }

    private static void ValidateRecoveryState(IReadOnlyList<Entry> entries, bool committed)
    {
        if (committed) return;
        foreach (var entry in entries.Where(entry => entry.State == 1 &&
                     Exists(entry.Path, entry.Directory) && !Exists(entry.Stage, entry.Directory) &&
                     (Exists(entry.Backup, entry.Directory) || !entry.HadOriginal)))
        {
            if (entry.Fingerprint is null || Fingerprint(entry.Path, entry.Directory) != entry.Fingerprint)
                throw new IOException($"Interrupted build target changed; preserve existing files and backup: {entry.Path}");
        }
        if (entries.FirstOrDefault(entry => entry.State == 1 && entry.HadOriginal &&
                !Exists(entry.Backup, entry.Directory) && !Exists(entry.Stage, entry.Directory)) is { } missingBackup)
            throw new IOException($"Interrupted build backup is missing: {missingBackup.Backup}");
    }

    private static void RestoreEntry(Entry entry, bool committed, ILogger? logger)
    {
        if (!committed && entry.State == 1)
        {
            if (Exists(entry.Backup, entry.Directory))
            {
                if (Exists(entry.Path, entry.Directory))
                {
                    if (Exists(entry.Stage, entry.Directory))
                        throw new IOException($"Ambiguous interrupted build; preserve backup: {entry.Backup}");
                    Delete(entry.Path, entry.Directory);
                }
                Move(entry.Backup, entry.Path, entry.Directory);
            }
            else if (entry.HadOriginal && !Exists(entry.Path, entry.Directory))
                throw new IOException($"Interrupted build backup is missing: {entry.Backup}");
            else if (!entry.HadOriginal && !Exists(entry.Stage, entry.Directory) && Exists(entry.Path, entry.Directory))
                Delete(entry.Path, entry.Directory);
        }
        TryDelete(entry.Stage, entry.Directory, logger, "staging");
        if (committed) TryDelete(entry.Backup, entry.Directory, logger, "backup");
    }

    internal bool RemoveBestEffort(ILogger? logger)
    {
        try
        {
            Remove();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.Warn($"Build transaction journal cleanup failed; retained: {_path}: {ex.Message}");
            return false;
        }
    }

    internal static IReadOnlyList<string> FindOtherJournals(string root, string output)
    {
        var current = Path.GetFileName(PathFor(root, output));
        var directory = Path.GetDirectoryName(PathFor(root, output))!;
        var result = new List<string>();
        AddFrom(directory);
        if (!BuildResourceLease.Same(directory, BuildResourceLease.Canonical(root)))
            AddFrom(BuildResourceLease.Canonical(root));
        return result;

        void AddFrom(string folder)
        {
            if (!Directory.Exists(folder)) return;
            result.AddRange(Directory.EnumerateFiles(folder, ".bukit-transaction-*.json")
                .Where(path => Path.GetFileName(path) != current && !IsLiveOwner(path)));
        }
    }

    internal static void RejectOtherJournals(string root, string output)
    {
        var others = FindOtherJournals(root, output);
        if (others.Count == 0) return;
        throw new IOException(
            "Interrupted build transactions exist for a different output. Build with the original output to recover, or inspect and remove these journals after checking their backups: " +
            string.Join("; ", others));
    }

    private static bool IsLiveOwner(string journalPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
            if (!document.RootElement.TryGetProperty("pid", out var pidElement) ||
                !pidElement.TryGetInt32(out var pid) || pid <= 0)
                return false;
            if (pid == Environment.ProcessId) return true;
            try
            {
                using var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static void TryDelete(string path, bool directory, ILogger? logger, string kind)
    {
        if (!Exists(path, directory)) return;
        try { Delete(path, directory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { logger?.Warn($"Build transaction {kind} cleanup failed; retained: {path}: {ex.Message}"); }
    }

    private static bool SafeSibling(string path, string sibling, string suffix)
    {
        var name = System.IO.Path.GetFileName(sibling);
        return string.Equals(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetDirectoryName(sibling), StringComparison.Ordinal) &&
            name.StartsWith(".bukit-txn-", StringComparison.Ordinal) && name.EndsWith(suffix, StringComparison.Ordinal) &&
            Guid.TryParseExact(name[11..^suffix.Length], "N", out _);
    }

    private static bool SafeWorktreeSibling(string path, string sibling, string kind)
    {
        var prefix = "." + System.IO.Path.GetFileName(path) + ".bukit-" + kind + "-";
        var name = System.IO.Path.GetFileName(sibling);
        return string.Equals(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetDirectoryName(sibling), StringComparison.Ordinal) &&
            name.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(name[prefix.Length..], "N", out _);
    }

    private static bool SafeMetricsStage(string stage, string runsRoot)
    {
        var relative = System.IO.Path.GetRelativePath(BuildResourceLease.Canonical(runsRoot), BuildResourceLease.Canonical(stage)).Split(System.IO.Path.DirectorySeparatorChar);
        return relative.Length == 3 && Guid.TryParseExact(relative[0], "N", out _) &&
            relative[1] == "metrics" && relative[2] is not ("" or "." or "..");
    }

    private static string? FindWorktreeRunsRoot(string root)
    {
        for (var directory = BuildResourceLease.Canonical(root); directory is not null; directory = System.IO.Path.GetDirectoryName(directory))
        {
            var git = System.IO.Path.Combine(directory, ".git");
            if (!System.IO.Directory.Exists(git) && !File.Exists(git)) continue;
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(System.IO.Path.GetFullPath(directory))))[..16];
            return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bukit-i18n-worktrees", hash);
        }
        return null;
    }

    private static string LegacyPath(string root, string output)
    {
        var identity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(BuildResourceLease.Canonical(output))));
        return System.IO.Path.Combine(BuildResourceLease.Canonical(root), ".bukit-transaction-" + identity + ".json");
    }

    private static string? FindGitAdminDirectory(string root)
    {
        for (var directory = BuildResourceLease.Canonical(root); directory is not null; directory = System.IO.Path.GetDirectoryName(directory))
        {
            var git = System.IO.Path.Combine(directory, ".git");
            if (System.IO.Directory.Exists(git)) return BuildResourceLease.Canonical(git);
            var file = new FileInfo(git);
            if (!file.Exists || file.Length > 4096) continue;
            var line = File.ReadAllText(git).Trim();
            const string prefix = "gitdir:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var value = line[prefix.Length..].Trim();
            if (value.Length == 0) continue;
            var admin = System.IO.Path.GetFullPath(value, directory);
            if (System.IO.Directory.Exists(admin) && File.Exists(System.IO.Path.Combine(admin, "HEAD")))
                return BuildResourceLease.Canonical(admin);
        }
        return null;
    }

    private static bool SafeMetricsBackup(string path, string backup)
        => string.Equals(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetDirectoryName(backup), StringComparison.Ordinal) &&
           System.IO.Path.GetFileName(backup) == "." + System.IO.Path.GetFileName(path) + ".bukit-backup";

    private static string Fingerprint(string path, bool directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Visit(directory ? new DirectoryInfo(path) : new FileInfo(path), "");
        return Convert.ToHexString(hash.GetHashAndReset());

        void Visit(FileSystemInfo entry, string relative)
        {
            var link = entry.LinkTarget;
            var kind = link is not null ? "link" : entry is DirectoryInfo ? "directory" : "file";
            hash.AppendData(Encoding.UTF8.GetBytes(kind + "\0" + relative + "\0"));
            if (link is not null)
                hash.AppendData(Encoding.UTF8.GetBytes(link));
            else if (entry is DirectoryInfo folder)
                foreach (var child in folder.EnumerateFileSystemInfos().OrderBy(item => item.Name, StringComparer.Ordinal))
                    Visit(child, System.IO.Path.Combine(relative, child.Name));
            else
            {
                using var stream = File.OpenRead(entry.FullName);
                hash.AppendData(SHA256.HashData(stream));
            }
        }
    }

    private static bool Exists(string path, bool directory) => directory ? System.IO.Directory.Exists(path) : File.Exists(path);
    private static bool ExistsOrLink(string path) => File.Exists(path) || System.IO.Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;
    private static void Delete(string path, bool directory) { if (directory) System.IO.Directory.Delete(path, true); else File.Delete(path); }
    private static void Move(string from, string to, bool directory) { if (directory) System.IO.Directory.Move(from, to); else File.Move(from, to); }
}
