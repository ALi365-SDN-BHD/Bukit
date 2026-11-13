using Bukit.Config;
using Bukit.Content.Notion.Snapshot;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Public entry point to the Notion offline snapshot for the CLI, which may not depend on
/// Bukit.Content. Enter <see cref="Handle"/> with <see cref="NotionSnapshotScope.Enter"/> in the
/// frame that awaits the build or check. Failures throw <see cref="NotionSnapshotException"/>.
/// </summary>
public sealed class NotionSnapshot : IDisposable
{
    private readonly NotionSnapshotSession _session;

    private NotionSnapshot(NotionSnapshotSession session) => _session = session;

    /// <summary>The ambient handle: the session itself, so content code can find it.</summary>
    public INotionSnapshotHandle Handle => _session;

    /// <summary>Directory read or written by this process. Never log it.</summary>
    public string Directory => _session.Directory;

    public bool Offline => _session.Offline;

    /// <summary>Starts recording a new snapshot; nothing replaces the old one until <see cref="Commit"/>.</summary>
    public static NotionSnapshot BeginWrite(string targetDirectory) => new(NotionSnapshotSession.BeginWrite(targetDirectory));

    /// <summary>Opens a committed snapshot for offline reads (no token, no network).</summary>
    public static NotionSnapshot OpenOffline(string directory) => new(NotionSnapshotSession.OpenOffline(directory));

    /// <summary>Joins the parent's snapshot from a multilingual worker process.</summary>
    public static NotionSnapshot AttachWorker(string directory, bool offline) => new(NotionSnapshotSession.AttachWorker(directory, offline));

    /// <summary>Replaces the previous snapshot with the one just recorded. Call only after a successful build.</summary>
    public void Commit() => _session.Commit();

    /// <summary>Fails when the configured Notion sources need databases the offline snapshot lacks.</summary>
    public void EnsureDatabases(AppConfig config)
    {
        if (config.Content.Sources is not { Count: > 0 } sources)
        {
            return;
        }

        foreach (var source in sources)
        {
            if (source.Type.Equals("notion", StringComparison.OrdinalIgnoreCase) && source.Notion is { } notion)
            {
                _session.EnsureDatabase(notion.DatabaseId);
            }
        }
    }

    /// <summary>Snapshot identity as log fields, without its location.</summary>
    public string DescribeFields()
        => _session.Manifest is { } manifest
            ? $"format={manifest.Format} recorded_at={manifest.CreatedAt.ToUniversalTime():yyyy-MM-dd'T'HH:mm:ss'Z'} databases={manifest.Databases.Count}"
            : "format=recording";

    /// <summary>Snapshot identity without its location (the path is never printed).</summary>
    public string Describe()
        => _session.Manifest is { } manifest
            ? $"format {manifest.Format}, recorded {manifest.CreatedAt.ToUniversalTime():yyyy-MM-dd'T'HH:mm:ss'Z'}, databases {manifest.Databases.Count}"
            : "recording";

    public void Dispose() => _session.Dispose();
}
