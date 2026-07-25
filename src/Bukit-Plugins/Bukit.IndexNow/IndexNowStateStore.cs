using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace Bukit.IndexNow;

public sealed class IndexNowStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string ResolveStateFile(string rootDir, string stateDir)
    {
        var root = Path.GetFullPath(rootDir);
        var expectedRoot = Path.GetFullPath(Path.Combine(root, ".cache", "indexnow"));
        var resolvedDir = Path.IsPathRooted(stateDir)
            ? Path.GetFullPath(stateDir)
            : Path.GetFullPath(Path.Combine(root, stateDir));
        if (!PathsEqual(expectedRoot, resolvedDir))
        {
            throw new InvalidOperationException("IndexNow state directory must be exactly .cache/indexnow.");
        }

        EnsureNoSymbolicLinks(root, resolvedDir);
        return Path.Combine(resolvedDir, "state.json");
    }

    public async Task<IndexNowState> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ValidatePath(path);
        if (!File.Exists(path))
        {
            return IndexNowState.Empty;
        }

        await using var stream = File.OpenRead(path);
        var state = await JsonSerializer.DeserializeAsync<IndexNowState>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException("IndexNow state document is empty.");
        if (state.Version != 1 || state.Deployed is null || state.Notified is null || state.Pending is null)
        {
            throw new InvalidDataException("IndexNow state document is invalid or unsupported.");
        }

        return Normalize(state);
    }

    public async Task<IAsyncDisposable> AcquireRunLockAsync(
        string statePath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        ValidatePath(statePath);
        var directory = Path.GetDirectoryName(statePath)
                        ?? throw new InvalidOperationException("IndexNow state path has no parent.");
        Directory.CreateDirectory(directory);
        ValidatePath(statePath);
        var lockPath = statePath + ".lock";
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.Asynchronous);
                return new RunLock(stream);
            }
            catch (IOException exception)
            {
                var remaining = timeout - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    throw new TimeoutException("IndexNow state is locked by another submission.", exception);
                }

                await Task.Delay(
                    remaining < TimeSpan.FromMilliseconds(25) ? remaining : TimeSpan.FromMilliseconds(25),
                    cancellationToken);
            }
        }
    }

    public async Task SaveAsync(string path, IndexNowState state, CancellationToken cancellationToken = default)
    {
        ValidatePath(path);
        var directory = Path.GetDirectoryName(path)
                        ?? throw new InvalidOperationException("IndexNow state path has no parent.");
        Directory.CreateDirectory(directory);
        ValidatePath(path);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                             temporary,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, Normalize(state), JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static IndexNowState Normalize(IndexNowState state)
        => state with
        {
            Deployed = state.Deployed.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            Notified = state.Notified.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            Pending = state.Pending
                .GroupBy(Fingerprint, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(change => change.Url, StringComparer.Ordinal)
                .ThenBy(change => change.Type, StringComparer.Ordinal)
                .ToArray()
        };

    private static void ValidatePath(string path)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)
                        ?? throw new InvalidOperationException("IndexNow state path has no parent.");
        var directoryInfo = new DirectoryInfo(directory);
        if (directoryInfo.Exists && directoryInfo.LinkTarget is not null)
        {
            throw new InvalidOperationException("IndexNow state directory must not be a symbolic link.");
        }

        EnsureNoSymbolicLinks(directory, full);
    }

    private static void EnsureNoSymbolicLinks(string boundary, string target)
    {
        var boundaryFull = Path.GetFullPath(boundary);
        var targetFull = Path.GetFullPath(target);
        if (!IsSameOrSubPath(targetFull, boundaryFull))
        {
            throw new InvalidOperationException("IndexNow managed path escapes its boundary.");
        }

        var relative = Path.GetRelativePath(boundaryFull, targetFull);
        var current = boundaryFull;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
            {
                throw new InvalidOperationException("IndexNow managed path must not traverse a symbolic link.");
            }
        }
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            PathComparison);

    private static bool IsSameOrSubPath(string path, string boundary)
    {
        var relative = Path.GetRelativePath(boundary, path);
        return relative == "." ||
               (!Path.IsPathFullyQualified(relative) &&
                relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, PathComparison) &&
                !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, PathComparison));
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static string Fingerprint(IndexNowPendingChange change)
        => $"{change.Type}\n{change.Url}\n{change.SemanticHash}";

    private sealed class RunLock(FileStream stream) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => stream.DisposeAsync();
    }
}
