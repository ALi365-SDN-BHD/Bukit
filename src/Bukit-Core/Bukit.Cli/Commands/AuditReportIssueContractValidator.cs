using System.Text.Json;

namespace Bukit.Cli.Commands;

internal static class AuditReportIssueContractValidator
{
    internal static void Validate(JsonElement issues)
    {
        var issueIndex = 0;
        foreach (var issue in issues.EnumerateArray())
        {
            var path = $"issues[{issueIndex}]";
            AuditReportJsonReader.EnsureObject(issue, path);
            AuditReportJsonReader.EnsureAllowedProperties(issue, path, "severity", "code", "route", "message");
            var severity = AuditReportJsonReader.ReadRequiredString(issue, path, "severity");
            if (!string.Equals(severity, "error", StringComparison.Ordinal) &&
                !string.Equals(severity, "warning", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{path}.severity must be 'error' or 'warning'.");
            }

            AuditReportJsonReader.ReadRequiredString(issue, path, "code");
            AuditReportJsonReader.ReadRequiredString(issue, path, "message");
            if (issue.TryGetProperty("route", out var route) &&
                route.ValueKind != JsonValueKind.Null &&
                route.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"{path}.route must be a string or null.");
            }

            issueIndex++;
        }
    }
}
