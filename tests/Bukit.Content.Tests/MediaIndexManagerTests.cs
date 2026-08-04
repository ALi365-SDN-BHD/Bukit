using System.Text.Json;
using System.Reflection;
using Bukit.Content.Media;
using Bukit.Shared;
using Xunit;

namespace Bukit.Content.Tests;

public sealed class MediaIndexManagerTests
{
    [Fact]
    public async Task PersistIndex_ExplicitSameValueRememberAfterExternalDelete_RestoresEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-media-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string key = "https://example.com/x.jpg";
            var seed = new MediaIndexManager(root, "/media", logger: null);
            seed.EnsureIndexLoaded(root);
            seed.RememberIndex(key, "x.jpg");
            seed.PersistIndex();

            var rememberingManager = new MediaIndexManager(root, "/media", logger: null);
            var deletingManager = new MediaIndexManager(root, "/media", logger: null);
            rememberingManager.EnsureIndexLoaded(root);
            deletingManager.EnsureIndexLoaded(root);

            deletingManager.ForgetIndex(key);
            deletingManager.PersistIndex();
            rememberingManager.RememberIndex(key, "x.jpg");
            rememberingManager.PersistIndex();

            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(root, ".media-index.json")));
            Assert.Equal(
                "x.jpg",
                document.RootElement.GetProperty("entries").GetProperty(key).GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PersistIndex_AfterAnotherManagerDeletesEntry_DoesNotResurrectDeletedEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-media-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string deletedKey = "https://example.com/x.jpg";
            const string changedKey = "https://example.com/y.jpg";
            var seed = new MediaIndexManager(root, "/media", logger: null);
            seed.EnsureIndexLoaded(root);
            seed.RememberIndex(deletedKey, "x.jpg");
            seed.RememberIndex(changedKey, "y.jpg");
            seed.PersistIndex();

            var staleManager = new MediaIndexManager(root, "/media", logger: null);
            var deletingManager = new MediaIndexManager(root, "/media", logger: null);
            staleManager.EnsureIndexLoaded(root);
            deletingManager.EnsureIndexLoaded(root);

            deletingManager.ForgetIndex(deletedKey);
            deletingManager.PersistIndex();
            staleManager.RememberIndex(changedKey, "y-updated.jpg");
            staleManager.PersistIndex();

            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(root, ".media-index.json")));
            var entries = document.RootElement.GetProperty("entries");
            Assert.False(entries.TryGetProperty(deletedKey, out _));
            Assert.Equal("y-updated.jpg", entries.GetProperty(changedKey).GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PersistIndex_WhenFailureInjectedBeforeReplace_PreservesExistingIndexBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-media-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var seed = new MediaIndexManager(root, "/media", logger: null);
            seed.EnsureIndexLoaded(root);
            seed.RememberIndex("https://example.com/old.jpg", "old.jpg");
            seed.PersistIndex();
            var indexPath = Path.Combine(root, ".media-index.json");
            var originalBytes = await File.ReadAllBytesAsync(indexPath);

            var constructor = typeof(MediaIndexManager).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(string), typeof(string), typeof(ILogger), typeof(Action)],
                modifiers: null);
            Assert.NotNull(constructor);
            var manager = Assert.IsType<MediaIndexManager>(constructor.Invoke(
                [root, "/media", null, (Action)(() => throw new IOException("injected"))]));
            manager.EnsureIndexLoaded(root);
            manager.RememberIndex("https://example.com/new.jpg", "new.jpg");

            manager.PersistIndex();

            Assert.Equal(originalBytes, await File.ReadAllBytesAsync(indexPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PersistIndex_AfterForgettingLoadedEntry_DoesNotResurrectEntryFromDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-media-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string key = "https://example.com/stale.jpg";
            var seed = new MediaIndexManager(root, "/media", logger: null);
            seed.EnsureIndexLoaded(root);
            seed.RememberIndex(key, "stale.jpg");
            seed.PersistIndex();

            var manager = new MediaIndexManager(root, "/media", logger: null);
            manager.EnsureIndexLoaded(root);
            manager.ForgetIndex(key);
            manager.PersistIndex();

            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(root, ".media-index.json")));
            Assert.False(document.RootElement.GetProperty("entries").TryGetProperty(key, out _));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PersistIndex_TwoInstancesMergeWithoutLostEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-media-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var first = new MediaIndexManager(root, "/media", logger: null);
            var second = new MediaIndexManager(root, "/media", logger: null);
            first.EnsureIndexLoaded(root);
            second.EnsureIndexLoaded(root);
            first.RememberIndex("https://example.com/first.jpg", "first.jpg");
            second.RememberIndex("https://example.com/second.jpg", "second.jpg");

            await Task.WhenAll(Task.Run(first.PersistIndex), Task.Run(second.PersistIndex));

            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(root, ".media-index.json")));
            var entries = document.RootElement.GetProperty("entries");
            Assert.Equal("first.jpg", entries.GetProperty("https://example.com/first.jpg").GetString());
            Assert.Equal("second.jpg", entries.GetProperty("https://example.com/second.jpg").GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PersistIndex_WhenCrossProcessLockIsTemporarilyBusy_WaitsAndCommitsPendingEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), "bukit-media-index-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var manager = new MediaIndexManager(root, "/media", logger: null);
            manager.EnsureIndexLoaded(root);
            manager.RememberIndex("https://example.com/image.jpg", "image.jpg");
            var lockPath = Path.Combine(root, ".media-index.json.lock");
            using var competingLock = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            var persistTask = Task.Run(manager.PersistIndex);
            await Task.Delay(100);

            Assert.False(persistTask.IsCompleted);
            competingLock.Dispose();
            await persistTask.WaitAsync(TimeSpan.FromSeconds(5));

            using var document = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(root, ".media-index.json")));
            Assert.Equal(
                "image.jpg",
                document.RootElement
                    .GetProperty("entries")
                    .GetProperty("https://example.com/image.jpg")
                    .GetString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
