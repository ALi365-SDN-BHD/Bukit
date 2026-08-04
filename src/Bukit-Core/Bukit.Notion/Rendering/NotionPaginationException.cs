namespace Bukit.Notion.Rendering;

/// <summary>
/// Stable failure for Notion pagination violations (missing cursor, repeated cursor,
/// request budget exceeded). <see cref="Reason"/> carries the machine-readable cause.
/// </summary>
public sealed class NotionPaginationException : Exception
{
    public NotionPaginationException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
