using System.Text.Json;

namespace Bukit.Cli.Commands;

internal static class SeoReportDiffSnapshotReader
{
    internal static SeoReportValidator.SeoReportSnapshot Read(JsonElement root)
    {
        var routes = new Dictionary<string, SeoReportValidator.SeoRouteSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("documents", out var documents))
        {
            foreach (var document in documents.EnumerateArray())
            {
                var url = AuditReportJsonReader.ReadRequiredString(document, "document", "routeUrl");
                routes[url] = new SeoReportValidator.SeoRouteSnapshot(
                    url,
                    AuditReportJsonReader.ReadRequiredBool(document, "document", "indexable"));
            }
        }
        else
        {
            foreach (var route in root.GetProperty("routes").EnumerateArray())
            {
                var url = AuditReportJsonReader.ReadRequiredString(route, "route", "url");
                routes[url] = new SeoReportValidator.SeoRouteSnapshot(
                    url,
                    AuditReportJsonReader.ReadRequiredBool(route, "route", "indexable"));
            }
        }

        var issues = root.GetProperty("issues").EnumerateArray()
            .Select(x => new SeoReportValidator.SeoIssueSnapshot(
                AuditReportJsonReader.ReadRequiredString(x, "issue", "severity"),
                AuditReportJsonReader.ReadRequiredString(x, "issue", "code"),
                AuditReportJsonReader.ReadString(x, "route"),
                AuditReportJsonReader.ReadRequiredString(x, "issue", "message")))
            .ToArray();

        return new SeoReportValidator.SeoReportSnapshot(routes, issues);
    }
}
