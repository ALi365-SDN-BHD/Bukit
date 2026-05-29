using Bukit.Shared;

namespace Bukit.Cli.Commands.Dev;

internal sealed class DevFileWatcher : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly string _rootDir;
    private readonly ILogger _logger;
    private readonly Func<string, CancellationToken, Task> _onRebuildAsync;
    private readonly int _debounceMs;
    private readonly SemaphoreSlim _rebuildLock = new(1, 1);

    private int _pending;
    private CancellationToken _ct;
    private bool _disposed;

    public DevFileWatcher(
        IReadOnlyList<string> dirs,
        string rootDir,
        ILogger logger,
        Func<string, CancellationToken, Task> onRebuildAsync,
        int debounceMs = 300)
    {
        _rootDir = rootDir ?? throw new ArgumentNullException(nameof(rootDir));
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
        _ct = ct;

        foreach (var watcher in _watchers)
        {
            watcher.Changed += OnChange;
            watcher.Created += OnChange;
            watcher.Deleted += OnChange;
            watcher.Renamed += (_, e) => _ = ScheduleRebuildAsync(e.FullPath);
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
        _rebuildLock.Dispose();
    }

    private void OnChange(object sender, FileSystemEventArgs e)
    {
        var name = e.Name ?? string.Empty;
        if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}.cache{Path.DirectorySeparatorChar}") ||
            e.FullPath.Contains($"{Path.DirectorySeparatorChar}dist{Path.DirectorySeparatorChar}") ||
            name.StartsWith('.'))
        {
            return;
        }

        _ = ScheduleRebuildAsync(e.FullPath);
    }

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
