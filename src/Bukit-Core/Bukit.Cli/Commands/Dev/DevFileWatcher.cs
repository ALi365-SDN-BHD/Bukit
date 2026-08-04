using System.Collections.Concurrent;
using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevFileWatcher : IDisposable, IAsyncDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly string _rootDir;
    private readonly IReadOnlyList<string> _excludedDirs;
    private readonly ILogger _logger;
    private readonly Func<string, CancellationToken, Task> _onRebuildAsync;
    private readonly int _debounceMs;
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);
    private readonly ConcurrentDictionary<Task, byte> _scheduledTasks = new();

    private int _pending;
    private CancellationToken _ct;
    private CancellationTokenSource? _lifetimeCts;
    private bool _disposed;

    public DevFileWatcher(
        IReadOnlyList<string> dirs,
        string rootDir,
        ILogger logger,
        Func<string, CancellationToken, Task> onRebuildAsync,
        IReadOnlyList<string>? excludedDirs = null,
        int debounceMs = 300)
    {
        _rootDir = Path.GetFullPath(rootDir ?? throw new ArgumentNullException(nameof(rootDir)));
        _excludedDirs = (excludedDirs ?? Array.Empty<string>())
            .Where(static d => !string.IsNullOrWhiteSpace(d))
            .Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _onRebuildAsync = onRebuildAsync ?? throw new ArgumentNullException(nameof(onRebuildAsync));
        _debounceMs = debounceMs;

        foreach (var dir in dirs)
        {
            var watcher = new FileSystemWatcher(dir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName |
                               NotifyFilters.DirectoryName | NotifyFilters.CreationTime,
                EnableRaisingEvents = false
            };

            _watchers.Add(watcher);
        }
    }

    public void Start(CancellationToken ct)
    {
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ct = _lifetimeCts.Token;

        foreach (var watcher in _watchers)
        {
            watcher.Changed += OnChange;
            watcher.Created += OnChange;
            watcher.Deleted += OnChange;
            watcher.Renamed += (_, e) =>
            {
                if (!ShouldIgnore(e.FullPath, e.Name))
                {
                    TrackRebuild(ScheduleRebuildAsync(e.FullPath));
                }
            };
            watcher.Error += (_, e) => _logger.Warn($"dev.filewatcher: {e.GetException().Message}");

            watcher.EnableRaisingEvents = true;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        try
        {
            _lifetimeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        _lifetimeCts?.Dispose();
        _rebuildLock.Dispose();
    }

    /// <summary>
    /// Asynchronous shutdown: stop events, cancel the lifetime token, await every
    /// tracked rebuild/debounce task, and only then dispose the rebuild gate.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
        }

        try
        {
            _lifetimeCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        // Drain tracked tasks until the set converges; cancellation makes pending
        // debounces return promptly and no new events can be admitted.
        while (!_scheduledTasks.IsEmpty)
        {
            var snapshot = _scheduledTasks.Keys.ToArray();
            foreach (var task in snapshot)
            {
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch
                {
                    // Tracked failures are already observed by their owner task.
                }
            }
        }

        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
        _lifetimeCts?.Dispose();
        _rebuildLock.Dispose();
    }

    private void TrackRebuild(Task task)
    {
        if (!_scheduledTasks.TryAdd(task, 0))
        {
            return;
        }

        _ = ObserveTrackedAsync(task);
    }

    private async Task ObserveTrackedAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Observed here so a canceled debounce never surfaces as an unobserved fault.
        }
        finally
        {
            _scheduledTasks.TryRemove(task, out _);
        }
    }

    private void OnChange(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath, e.Name))
        {
            return;
        }

        TrackRebuild(ScheduleRebuildAsync(e.FullPath));
    }

    /// <summary>Deterministic change injection for tests; follows the tracked rebuild path.</summary>
    internal void RaiseChangeForTest(string fullPath)
    {
        if (ShouldIgnore(fullPath, Path.GetFileName(fullPath)))
        {
            return;
        }

        TrackRebuild(ScheduleRebuildAsync(fullPath));
    }

    internal bool ShouldIgnore(string fullPath, string? eventName)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return true;
        }

        if (IsDotPrefixed(eventName))
        {
            return true;
        }

        try
        {
            foreach (var excluded in _excludedDirs)
            {
                if (PathUtils.IsSameOrSubPathOf(fullPath, excluded))
                {
                    return true;
                }
            }

            var relative = Path.GetRelativePath(_rootDir, Path.GetFullPath(fullPath));
            if (relative.StartsWith("..", PlatformPathHelper.PathComparison) || Path.IsPathRooted(relative))
            {
                return false;
            }

            foreach (var segment in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (IsExcludedSegment(segment) || segment.StartsWith(".", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.Warn($"dev.filewatcher.path_filter: {ex.Message}");
            return true;
        }

        return false;
    }

    private static bool IsDotPrefixed(string? eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return false;
        }

        var name = Path.GetFileName(eventName);
        return name.StartsWith(".", StringComparison.Ordinal);
    }

    private static bool IsExcludedSegment(string segment)
        => segment is ".cache" or ".git" or "node_modules" or ".bukit" or "bin" or "obj";

    private async Task ScheduleRebuildAsync(string file)
    {
        Interlocked.Increment(ref _pending);

        try
        {
            await Task.Delay(_debounceMs, _ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref _pending);
            return;
        }

        if (Interlocked.Decrement(ref _pending) > 0) return;

        await _rebuildLock.WaitAsync(_ct).ConfigureAwait(false);
        try
        {
            await _onRebuildAsync(file, _ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.Error($"dev.rebuild.error: {ex.Message}");
        }
        finally
        {
            _rebuildLock.Release();
        }
    }
}
