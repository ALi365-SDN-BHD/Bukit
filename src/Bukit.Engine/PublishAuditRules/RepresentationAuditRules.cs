namespace Bukit.Engine.PublishAuditRules;

internal static class RepresentationAuditRules
{
    internal static void Analyze(PublishDocument document, List<SeoAuditIssue> issues)
    {
        if (!document.RepresentationKinds.Contains("html", StringComparer.OrdinalIgnoreCase) ||
            !document.RepresentationKinds.Contains("json", StringComparer.OrdinalIgnoreCase) ||
            !document.RepresentationKinds.Contains("markdown", StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new SeoAuditIssue("error", "publish.representation_missing", document.RouteUrl, "Published content is missing one or more required representations (html/json/markdown)."));
        }
    }
}
