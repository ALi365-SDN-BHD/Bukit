using System.Reflection;
using Bukit.Cli.Commands.Dev;
using Bukit.Shared;
using Xunit;

namespace Bukit.Cli.Tests.Dev;

public class DevFileWatcherTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly CancellationTokenSource _cts;

    public DevFileWatcherTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "bukit_watcher_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        try { Directory.Delete(_tmpDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task OnChange_Debounce_MultipleWritesTriggerSingleRebuild()
    {
        var rebuildCount = 0;
        var tcs = new TaskCompletionSource<bool>();

        var logger = new TestLogger();
        using var watcher = new DevFileWatcher(
            new[] { _tmpDir },
            _tmpDir,
            logger,
            (file, ct) =>
            {
                var count = Interlocked.Increment(ref rebuildCount);
                if (count == 1) tcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            debounceMs: 300);

        watcher.Start(_cts.Token);

        // Fire 5 rapid writes
        var testFile = Path.Combine(_tmpDir, "test.txt");
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(testFile, $"content {i}");
            await Task.Delay(20);
        }

        // Wait for debounce + some margin
        await Task.WhenAny(tcs.Task, Task.Delay(2000));

        Assert.Equal(1, rebuildCount);
    }

    [Fact]
    public async Task Dispose_StopsTriggeringRebuild()
    {
        var rebuildCount = 0;
        var logger = new TestLogger();
        var watcher = new DevFileWatcher(
            new[] { _tmpDir },
            _tmpDir,
            logger,
            (file, ct) =>
            {
                Interlocked.Increment(ref rebuildCount);
                return Task.CompletedTask;
            },
            debounceMs: 100);

        watcher.Start(_cts.Token);
        watcher.Dispose();

        var testFile = Path.Combine(_tmpDir, "disposed_test.txt");
        File.WriteAllText(testFile, "content");
        await Task.Delay(500);

        Assert.Equal(0, rebuildCount);
    }

    [Fact]
    public async Task Start_GcCollect_StillReceivesCallback()
    {
        var tcs = new TaskCompletionSource<bool>();
        var logger = new TestLogger();

        var watcher = new DevFileWatcher(
            new[] { _tmpDir },
            _tmpDir,
            logger,
            (file, ct) =>
            {
                tcs.TrySetResult(true);
                return Task.CompletedTask;
            },
            debounceMs: 100);

        watcher.Start(_cts.Token);

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);

        var testFile = Path.Combine(_tmpDir, "gc_test.txt");
        File.WriteAllText(testFile, "content");

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        Assert.True(completed is Task<bool>, "Callback should still fire after GC");

        watcher.Dispose();
    }

    [Fact]
    public void ErrorEvent_LogsWarn()
    {
        var logger = new TestLogger();
        using var watcher = new DevFileWatcher(
            new[] { _tmpDir },
            _tmpDir,
            logger,
            (file, ct) => Task.CompletedTask,
            debounceMs: 100);

        watcher.Start(_cts.Token);

        var watchersField = typeof(DevFileWatcher)
            .GetField("_watchers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var watchers = (List<FileSystemWatcher>)watchersField.GetValue(watcher)!;
        var fsw = watchers[0];

        // Invoke protected OnError to trigger the Error event handler
        var onErrorMethod = typeof(FileSystemWatcher)
            .GetMethod("OnError", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var ex = new InvalidOperationException("test error");
        onErrorMethod.Invoke(fsw, new object[] { new ErrorEventArgs(ex) });

        Assert.Contains(logger.Warns, w => w.Contains("dev.filewatcher") && w.Contains("test error"));
    }
}
