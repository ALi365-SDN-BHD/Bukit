using System.Text.Json;

namespace Bukit.Cli.Commands;

internal static class PublishAuditReportContractValidator
{
    internal static void Validate(JsonElement root)
    {
        AuditReportJsonReader.EnsureAllowedProperties(root, "$", "schema", "schemaVersion", "generatedAt", "siteName", "siteUrl", "baseUrl", "documents", "issues", "summary");

        var schemaVersion = AuditReportJsonReader.ReadRequiredString(root, "$", "schemaVersion");
        if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schemaVersion '{schemaVersion}'. Expected '1.0'.");
        }

        var documents = AuditReportJsonReader.ReadRequiredArray(root, "$", "documents");
        var issues = AuditReportJsonReader.ReadRequiredArray(root, "$", "issues");
        var summary = AuditReportJsonReader.ReadRequiredObject(root, "$", "summary");

        AuditReportJsonReader.ReadRequiredString(root, "$", "generatedAt");
        AuditReportJsonReader.ReadRequiredString(root, "$", "siteName");
        AuditReportJsonReader.ReadRequiredString(root, "$", "baseUrl");
        AuditReportJsonReader.ReadOptionalString(root, "$", "siteUrl");
        AuditReportJsonReader.EnsureAllowedProperties(summary, "summary", "documentCount", "indexableCount", "nonIndexableCount", "errorCount", "warningCount", "publishIssueCount", "machineReadabilityIssueCount", "trustIssueCount", "representationGapCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "documentCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "indexableCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "nonIndexableCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "errorCount");
        AuditReportJsonReader.ReadRequiredInt(summary, "summary", "warningCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "publishIssueCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "machineReadabilityIssueCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "trustIssueCount");
        AuditReportJsonReader.ReadOptionalInt(summary, "summary", "representationGapCount");

        var documentIndex = 0;
        foreach (var document in documents.EnumerateArray())
        {
            var path = $"documents[{documentIndex}]";
            AuditReportJsonReader.EnsureObject(document, path);
            AuditReportJsonReader.EnsureAllowedProperties(document, path, "routeUrl", "outputPath", "canonical", "indexable", "lastModified", "contentType", "sourceItemId", "title", "description", "language", "author", "organization", "source", "originalSource", "reviewStatus", "summary", "updatedAt", "sourceReferences", "entityNames", "entitySummaries", "representationKinds", "representations", "schemaTypes", "structuredDataTypes", "semanticOutline", "sitemapIncluded", "searchIncluded", "rssIncluded", "atomFeedIncluded", "jsonFeedIncluded", "llmsIncluded", "llmsFullIncluded", "robotsIncluded", "manifestIncluded");
            AuditReportJsonReader.ReadRequiredString(document, path, "routeUrl");
            AuditReportJsonReader.ReadRequiredString(document, path, "outputPath");
            AuditReportJsonReader.ReadRequiredString(document, path, "canonical");
            AuditReportJsonReader.ReadRequiredBool(document, path, "indexable");
            AuditReportJsonReader.ReadOptionalString(document, path, "lastModified");
            AuditReportJsonReader.ReadOptionalString(document, path, "contentType");
            AuditReportJsonReader.ReadOptionalString(document, path, "sourceItemId");
            AuditReportJsonReader.ReadOptionalString(document, path, "title");
            AuditReportJsonReader.ReadOptionalString(document, path, "description");
            AuditReportJsonReader.ReadOptionalString(document, path, "language");
            AuditReportJsonReader.ReadOptionalString(document, path, "author");
            AuditReportJsonReader.ReadOptionalString(document, path, "organization");
            AuditReportJsonReader.ReadOptionalString(document, path, "source");
            AuditReportJsonReader.ReadOptionalString(document, path, "originalSource");
            AuditReportJsonReader.ReadOptionalString(document, path, "reviewStatus");
            AuditReportJsonReader.ReadOptionalString(document, path, "summary");
            AuditReportJsonReader.ReadOptionalString(document, path, "updatedAt");
            AuditReportJsonReader.ReadOptionalStringArray(document, path, "sourceReferences");
            AuditReportJsonReader.ReadOptionalStringArray(document, path, "entityNames");
            ReadOptionalEntitySummaries(document, path, "entitySummaries");
            AuditReportJsonReader.ReadOptionalStringArray(document, path, "representationKinds");
            ReadOptionalRepresentations(document, path, "representations");
            AuditReportJsonReader.ReadOptionalStringArray(document, path, "schemaTypes");
            AuditReportJsonReader.ReadOptionalStringArray(document, path, "structuredDataTypes");
            ReadOptionalSemanticOutline(document, path, "semanticOutline");
            AuditReportJsonReader.ReadRequiredBool(document, path, "sitemapIncluded");
            AuditReportJsonReader.ReadRequiredBool(document, path, "searchIncluded");
            AuditReportJsonReader.ReadRequiredBool(document, path, "rssIncluded");
            AuditReportJsonReader.ReadOptionalBool(document, path, "atomFeedIncluded");
            AuditReportJsonReader.ReadOptionalBool(document, path, "jsonFeedIncluded");
            AuditReportJsonReader.ReadOptionalBool(document, path, "llmsIncluded");
            AuditReportJsonReader.ReadOptionalBool(document, path, "llmsFullIncluded");
            AuditReportJsonReader.ReadOptionalBool(document, path, "robotsIncluded");
            AuditReportJsonReader.ReadOptionalBool(document, path, "manifestIncluded");
            documentIndex++;
        }

        AuditReportIssueContractValidator.Validate(issues);
    }

    private static void ReadOptionalRepresentations(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var representations) || representations.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (representations.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array.");
        }

        var index = 0;
        foreach (var representation in representations.EnumerateArray())
        {
            var itemPath = $"{path}.{property}[{index}]";
            AuditReportJsonReader.EnsureObject(representation, itemPath);
            AuditReportJsonReader.EnsureAllowedProperties(representation, itemPath, "kind", "url", "path", "generated", "indexable");
            AuditReportJsonReader.ReadRequiredString(representation, itemPath, "kind");
            AuditReportJsonReader.ReadRequiredString(representation, itemPath, "url");
            var generated = AuditReportJsonReader.ReadRequiredBool(representation, itemPath, "generated");
            ReadRepresentationPath(representation, itemPath, generated);
            AuditReportJsonReader.ReadRequiredBool(representation, itemPath, "indexable");
            index++;
        }
    }

    private static void ReadRepresentationPath(JsonElement element, string path, bool generated)
    {
        if (!element.TryGetProperty("path", out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{path}.path must be a string.");
        }

        if (generated && string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{path}.path must be a non-empty string when generated is true.");
        }
    }

    private static void ReadOptionalEntitySummaries(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array.");
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = $"{path}.{property}[{index}]";
            AuditReportJsonReader.EnsureObject(item, itemPath);
            AuditReportJsonReader.EnsureAllowedProperties(item, itemPath, "type", "name", "description");
            AuditReportJsonReader.ReadRequiredString(item, itemPath, "type");
            AuditReportJsonReader.ReadRequiredString(item, itemPath, "name");
            AuditReportJsonReader.ReadOptionalString(item, itemPath, "description");
            index++;
        }
    }

    private static void ReadOptionalSemanticOutline(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array.");
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemPath = $"{path}.{property}[{index}]";
            AuditReportJsonReader.EnsureObject(item, itemPath);
            AuditReportJsonReader.EnsureAllowedProperties(item, itemPath, "level", "text");
            AuditReportJsonReader.ReadRequiredInt(item, itemPath, "level");
            AuditReportJsonReader.ReadRequiredString(item, itemPath, "text");
            index++;
        }
    }
}
