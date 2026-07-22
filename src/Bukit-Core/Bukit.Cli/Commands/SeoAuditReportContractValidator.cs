using System.Text.Json;

namespace Bukit.Cli.Commands;

internal static class SeoAuditReportContractValidator
{
    internal static void Validate(JsonElement root)
    {
        AuditReportJsonReader.EnsureAllowedProperties(root, "$", "schema", "schemaVersion", "generatedAt", "siteName", "siteUrl", "baseUrl", "routes", "issues", "summary");

        var schemaVersion = AuditReportJsonReader.ReadRequiredString(root, "$", "schemaVersion");
        if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schemaVersion '{schemaVersion}'. Expected '1.0'.");
        }

        var routes = AuditReportJsonReader.ReadRequiredArray(root, "$", "routes");
        var issues = AuditReportJsonReader.ReadRequiredArray(root, "$", "issues");
        var summary = AuditReportJsonReader.ReadRequiredObject(root, "$", "summary");

        AuditReportJsonReader.ReadRequiredString(root, "$", "generatedAt");
        AuditReportJsonReader.ReadRequiredString(root, "$", "siteName");
        AuditReportJsonReader.ReadRequiredString(root, "$", "baseUrl");
        AuditReportJsonReader.ReadOptionalString(root, "$", "siteUrl");
        AuditReportJsonReader.EnsureAllowedProperties(summary, "summary", "routeCount", "indexableCount", "nonIndexableCount", "errorCount", "warningCount", "llmsTxtGenerated", "llmsFullTxtGenerated", "geoEnhancedCount", "geoScore", "publishIssueCount", "machineReadabilityIssueCount", "trustIssueCount", "representationGapCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "routeCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "indexableCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "nonIndexableCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "errorCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "warningCount");
        AuditReportJsonReader.ReadOptionalBool(summary, "summary", "llmsTxtGenerated");
        AuditReportJsonReader.ReadOptionalBool(summary, "summary", "llmsFullTxtGenerated");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "geoEnhancedCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "geoScore");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "publishIssueCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "machineReadabilityIssueCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "trustIssueCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "representationGapCount");

        var routeIndex = 0;
        foreach (var route in routes.EnumerateArray())
        {
            var path = $"routes[{routeIndex}]";
            AuditReportJsonReader.EnsureObject(route, path);
            AuditReportJsonReader.EnsureAllowedProperties(route, path, "url", "outputPath", "title", "description", "canonical", "robots", "indexable", "lastModified", "contentType", "sourceItemId", "sitemapIncluded", "searchIncluded", "rssIncluded", "alternates", "schemaTypes", "language", "author", "organization", "source", "originalSource", "reviewStatus", "entityNames", "representationKinds");
            AuditReportJsonReader.ReadRequiredString(route, path, "url");
            AuditReportJsonReader.ReadRequiredString(route, path, "outputPath");
            AuditReportJsonReader.ReadOptionalString(route, path, "title");
            AuditReportJsonReader.ReadOptionalString(route, path, "description");
            AuditReportJsonReader.ReadRequiredString(route, path, "canonical");
            AuditReportJsonReader.ReadOptionalString(route, path, "robots");
            AuditReportJsonReader.ReadRequiredBool(route, path, "indexable");
            AuditReportJsonReader.ReadRequiredString(route, path, "lastModified");
            AuditReportJsonReader.ReadOptionalString(route, path, "contentType");
            AuditReportJsonReader.ReadOptionalString(route, path, "sourceItemId");
            AuditReportJsonReader.ReadRequiredBool(route, path, "sitemapIncluded");
            AuditReportJsonReader.ReadRequiredBool(route, path, "searchIncluded");
            AuditReportJsonReader.ReadRequiredBool(route, path, "rssIncluded");
            AuditReportJsonReader.ReadOptionalString(route, path, "language");
            AuditReportJsonReader.ReadOptionalString(route, path, "author");
            AuditReportJsonReader.ReadOptionalString(route, path, "organization");
            AuditReportJsonReader.ReadOptionalString(route, path, "source");
            AuditReportJsonReader.ReadOptionalString(route, path, "originalSource");
            AuditReportJsonReader.ReadOptionalString(route, path, "reviewStatus");
            var alternates = AuditReportJsonReader.ReadRequiredArray(route, path, "alternates");
            var altIndex = 0;
            foreach (var alternate in alternates.EnumerateArray())
            {
                var altPath = $"{path}.alternates[{altIndex}]";
                AuditReportJsonReader.EnsureObject(alternate, altPath);
                AuditReportJsonReader.EnsureAllowedProperties(alternate, altPath, "hreflang", "href");
                AuditReportJsonReader.ReadRequiredString(alternate, altPath, "hreflang");
                AuditReportJsonReader.ReadRequiredString(alternate, altPath, "href");
                altIndex++;
            }

            var schemaTypes = AuditReportJsonReader.ReadRequiredArray(route, path, "schemaTypes");
            var schemaTypeIndex = 0;
            foreach (var schemaType in schemaTypes.EnumerateArray())
            {
                if (schemaType.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"{path}.schemaTypes[{schemaTypeIndex}] must be a string.");
                }

                schemaTypeIndex++;
            }

            AuditReportJsonReader.ReadOptionalStringArray(route, path, "entityNames");
            AuditReportJsonReader.ReadOptionalStringArray(route, path, "representationKinds");

            routeIndex++;
        }

        AuditReportIssueContractValidator.Validate(issues);
    }
}
