using Bukit.Config;
using Bukit.Content.Notion.Snapshot;
using Bukit.Shared;

namespace Bukit.Engine;

/// <summary>
/// Entry point to the Notion offline snapshot for the CLI, which may not depend on
/// Bukit.Content. Failures are recognised with <see cref="IsSnapshotError"/>.
/// </summary>
internal sealed class NotionSnapshot : IDisposable
{
    private readonly NotionSnapshotSession _session;

    private NotionSnapshot(NotionSnapshotSession session) => _session = session;

    /// <summary>
    /// Makes <paramref name="snapshot"/> the ambient snapshot until the scope is disposed (null
    /// clears it). Call it in the frame that awaits the build or check.
    /// </summary>
    public static IDisposable Enter(NotionSnapshot? snapshot) => NotionSnapshotScope.Enter(snapshot?._session);

    /// <summary>True for a snapshot failure: missing, unreadable, another format, or lacking data.</summary>
    public static bool IsSnapshotError(Exception exception) => exception is NotionSnapshotException;

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
            ? $"format={manifest.Format} bukitVersion={manifest.BukitVersion} recorded_at={manifest.CreatedAt.ToUniversalTime():yyyy-MM-dd'T'HH:mm:ss'Z'} databases={manifest.Databases.Count}"
            : "format=recording";

    /// <summary>Snapshot identity without its location (the path is never printed).</summary>
    public string Describe()
        => _session.Manifest is { } manifest
            ? $"format {manifest.Format}, Bukit {manifest.BukitVersion}, recorded {manifest.CreatedAt.ToUniversalTime():yyyy-MM-dd'T'HH:mm:ss'Z'}, databases {manifest.Databases.Count}"
            : "recording";

    public void Dispose() => _session.Dispose();
}
