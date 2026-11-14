using Bukit.Shared;

namespace Bukit.Content.Notion.Snapshot;

/// <summary>
/// The Notion offline snapshot is missing, unreadable, from another format, or lacks data
/// the build needs. Never swallowed: offline builds fail instead of silently skipping Notion
/// data. Messages never contain the snapshot path.
/// </summary>
internal sealed class NotionSnapshotException : BukitException
{
    internal const string RebuildHint =
        "Run a build with NOTION_TOKEN and --notion-snapshot (without --notion-offline) to refresh the snapshot.";

    public NotionSnapshotException(string message, DiagnosticCode code)
        : base(message, code)
    {
    }

    public NotionSnapshotException(string message, Exception innerException, DiagnosticCode code)
        : base(message, innerException, code)
    {
    }

    internal static NotionSnapshotException Missing(
        string what,
        DiagnosticCode code = DiagnosticCode.NotionSnapshotEntryMissing)
        => new($"Notion offline snapshot has no {what}. {RebuildHint}", code);
}
