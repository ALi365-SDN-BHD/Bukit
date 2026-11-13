namespace Bukit.Shared;

/// <summary>
/// The Notion offline snapshot is missing, unreadable, from another format or Bukit
/// version, or lacks data the build needs. Never swallowed: offline builds fail instead of
/// silently skipping Notion data. Messages never contain the snapshot path.
/// </summary>
public sealed class NotionSnapshotException : BukitException
{
    public const string RebuildHint =
        "Run a build with NOTION_TOKEN and --notion-snapshot (without --notion-offline) to refresh the snapshot.";

    public NotionSnapshotException(string message)
        : base(message, DiagnosticCode.ConfigInvalidValue)
    {
    }

    public NotionSnapshotException(string message, Exception innerException)
        : base(message, innerException, DiagnosticCode.ConfigInvalidValue)
    {
    }

    public static NotionSnapshotException Missing(string what)
        => new($"Notion offline snapshot has no {what}. {RebuildHint}");
}
