namespace Bukit.Engine.PublishAuditRules;

internal static class PublishDocumentAuditScope
{
    internal static bool IsContentBacked(PublishDocument document)
        => !document.IsDerived &&
           !string.IsNullOrWhiteSpace(document.SourceItemId);
}
