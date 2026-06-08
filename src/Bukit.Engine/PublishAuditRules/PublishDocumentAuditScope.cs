namespace Bukit.Engine.PublishAuditRules;

internal static class PublishDocumentAuditScope
{
    internal static bool IsContentBacked(PublishDocument document)
        => !string.IsNullOrWhiteSpace(document.SourceItemId) &&
           !string.Equals(document.ContentType, "list", StringComparison.OrdinalIgnoreCase) &&
           !HasGeneratedSourceItemId(document.SourceItemId);

    private static bool HasGeneratedSourceItemId(string sourceItemId)
        => sourceItemId.Equals("categories-index", StringComparison.OrdinalIgnoreCase) ||
           sourceItemId.Equals("tags-index", StringComparison.OrdinalIgnoreCase) ||
           sourceItemId.StartsWith("categories-", StringComparison.OrdinalIgnoreCase) ||
           sourceItemId.StartsWith("tags-", StringComparison.OrdinalIgnoreCase) ||
           sourceItemId.Contains("-archive-", StringComparison.OrdinalIgnoreCase);
}
