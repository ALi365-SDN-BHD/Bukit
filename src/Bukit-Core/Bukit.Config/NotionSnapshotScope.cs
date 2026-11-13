namespace Bukit.Config;

/// <summary>
/// The Notion offline snapshot selected for the current command: the directory this
/// process reads (offline) or writes (a staging directory while a build records it).
/// </summary>
internal interface INotionSnapshotHandle
{
    /// <summary>Directory read or written by this process. Never log it.</summary>
    string Directory { get; }

    /// <summary>True when Notion data comes only from the snapshot: no token, no network.</summary>
    bool Offline { get; }
}

/// <summary>
/// Ambient Notion offline snapshot for the current async flow. Set by the CLI for
/// <c>build</c>, <c>config check</c> and <c>doctor</c> when <c>--notion-snapshot</c> is given;
/// absent otherwise, which keeps every existing code path unchanged.
/// </summary>
internal static class NotionSnapshotScope
{
    private static readonly AsyncLocal<INotionSnapshotHandle?> s_current = new();

    public static INotionSnapshotHandle? Current => s_current.Value;

    /// <summary>True when Notion must be read from the offline snapshot only.</summary>
    public static bool IsOffline => s_current.Value?.Offline == true;

    /// <summary>
    /// Makes <paramref name="handle"/> current until the returned scope is disposed. Call it
    /// in the frame that awaits the work: an async local set inside an awaited helper does
    /// not flow back to its caller.
    /// </summary>
    public static IDisposable Enter(INotionSnapshotHandle? handle)
    {
        var previous = s_current.Value;
        s_current.Value = handle;
        return new Restore(previous);
    }

    private sealed class Restore(INotionSnapshotHandle? previous) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                s_current.Value = previous;
            }
        }
    }
}
