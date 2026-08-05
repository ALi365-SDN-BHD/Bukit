namespace Bukit.Notion.Rendering;

/// <summary>
/// Guards one logical Notion pagination loop. Records every cursor seen, rejects
/// has_more without a cursor, rejects repeated cursors, and enforces a hard request
/// budget so a pathological API cannot loop forever or accumulate unbounded pages.
/// </summary>
internal sealed class NotionPaginationGuard
{
    public const int MaxRequests = 10_000;

    public const string ReasonMissingCursor = "missing_cursor";
    public const string ReasonRepeatedCursor = "repeated_cursor";
    public const string ReasonRequestBudgetExceeded = "request_budget_exceeded";

    private readonly HashSet<string> _seenCursors = new(StringComparer.Ordinal);
    private int _requestCount;

    /// <summary>Accounts one API request against the budget.</summary>
    public void CountRequest()
    {
        _requestCount++;
        if (_requestCount > MaxRequests)
        {
            throw new NotionPaginationException(
                ReasonRequestBudgetExceeded,
                $"Notion pagination exceeded the request budget of {MaxRequests.ToString(System.Globalization.CultureInfo.InvariantCulture)} requests.");
        }
    }

    /// <summary>Validates and records the cursor returned with has_more=true.</summary>
    public void Advance(string? nextCursor)
    {
        if (string.IsNullOrWhiteSpace(nextCursor))
        {
            throw new NotionPaginationException(
                ReasonMissingCursor,
                "Notion pagination reported has_more without a next cursor.");
        }

        if (!_seenCursors.Add(nextCursor))
        {
            throw new NotionPaginationException(
                ReasonRepeatedCursor,
                $"Notion pagination returned the repeated cursor '{nextCursor}'.");
        }
    }
}
