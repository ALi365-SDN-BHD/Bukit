using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands;

internal static partial class SeoReportValidator
{
    internal static void ValidateReportContract(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("root must be a JSON object.");
        }

        var schema = ReadRequiredString(root, "$", "schema");
        if (string.Equals(schema, "https://bukit.dev/schemas/publish-audit-report.v1.json", StringComparison.Ordinal))
        {
            ValidatePublishReportContract(root);
            return;
        }

        if (!string.Equals(schema, "https://bukit.dev/schemas/seo-report.v1.json", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schema '{schema}'. Expected 'https://bukit.dev/schemas/seo-report.v1.json' or 'https://bukit.dev/schemas/publish-audit-report.v1.json'.");
        }

        EnsureAllowedProperties(root, "$", "schema", "schemaVersion", "generatedAt", "siteName", "siteUrl", "baseUrl", "routes", "issues", "summary");

        var schemaVersion = ReadRequiredString(root, "$", "schemaVersion");
        if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schemaVersion '{schemaVersion}'. Expected '1.0'.");
        }

        var routes = ReadRequiredArray(root, "$", "routes");
        var issues = ReadRequiredArray(root, "$", "issues");
        var summary = ReadRequiredObject(root, "$", "summary");

        ReadRequiredString(root, "$", "generatedAt");
        ReadRequiredString(root, "$", "siteName");
        ReadRequiredString(root, "$", "baseUrl");
        ReadOptionalString(root, "$", "siteUrl");
        EnsureAllowedProperties(summary, "summary", "routeCount", "indexableCount", "nonIndexableCount", "errorCount", "warningCount", "llmsTxtGenerated", "llmsFullTxtGenerated", "geoEnhancedCount", "geoScore", "publishIssueCount", "machineReadabilityIssueCount", "trustIssueCount", "representationGapCount");
        ReadRequiredInt(summary, "summary", "routeCount");
        ReadRequiredInt(summary, "summary", "indexableCount");
        ReadRequiredInt(summary, "summary", "nonIndexableCount");
        ReadRequiredInt(summary, "summary", "errorCount");
        ReadRequiredInt(summary, "summary", "warningCount");
        ReadOptionalBool(summary, "summary", "llmsTxtGenerated");
        ReadOptionalBool(summary, "summary", "llmsFullTxtGenerated");
        ReadOptionalInt(summary, "summary", "geoEnhancedCount");
        ReadOptionalInt(summary, "summary", "geoScore");
        ReadOptionalInt(summary, "summary", "publishIssueCount");
        ReadOptionalInt(summary, "summary", "machineReadabilityIssueCount");
        ReadOptionalInt(summary, "summary", "trustIssueCount");
        ReadOptionalInt(summary, "summary", "representationGapCount");

        var routeIndex = 0;
        foreach (var route in routes.EnumerateArray())
        {
            var path = $"routes[{routeIndex}]";
            EnsureObject(route, path);
            EnsureAllowedProperties(route, path, "url", "outputPath", "title", "description", "canonical", "robots", "indexable", "lastModified", "contentType", "sourceItemId", "sitemapIncluded", "searchIncluded", "rssIncluded", "alternates", "schemaTypes", "language", "author", "organization", "source", "originalSource", "reviewStatus", "entityNames", "representationKinds");
            ReadRequiredString(route, path, "url");
            ReadRequiredString(route, path, "outputPath");
            ReadOptionalString(route, path, "title");
            ReadOptionalString(route, path, "description");
            ReadRequiredString(route, path, "canonical");
            ReadOptionalString(route, path, "robots");
            ReadRequiredBool(route, path, "indexable");
            ReadRequiredString(route, path, "lastModified");
            ReadOptionalString(route, path, "contentType");
            ReadOptionalString(route, path, "sourceItemId");
            ReadRequiredBool(route, path, "sitemapIncluded");
            ReadRequiredBool(route, path, "searchIncluded");
            ReadRequiredBool(route, path, "rssIncluded");
            ReadOptionalString(route, path, "language");
            ReadOptionalString(route, path, "author");
            ReadOptionalString(route, path, "organization");
            ReadOptionalString(route, path, "source");
            ReadOptionalString(route, path, "originalSource");
            ReadOptionalString(route, path, "reviewStatus");
            var alternates = ReadRequiredArray(route, path, "alternates");
            var altIndex = 0;
            foreach (var alternate in alternates.EnumerateArray())
            {
                var altPath = $"{path}.alternates[{altIndex}]";
                EnsureObject(alternate, altPath);
                EnsureAllowedProperties(alternate, altPath, "hreflang", "href");
                ReadRequiredString(alternate, altPath, "hreflang");
                ReadRequiredString(alternate, altPath, "href");
                altIndex++;
            }

            var schemaTypes = ReadRequiredArray(route, path, "schemaTypes");
            var schemaTypeIndex = 0;
            foreach (var schemaType in schemaTypes.EnumerateArray())
            {
                if (schemaType.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException($"{path}.schemaTypes[{schemaTypeIndex}] must be a string.");
                }

                schemaTypeIndex++;
            }

            ReadOptionalStringArray(route, path, "entityNames");
            ReadOptionalStringArray(route, path, "representationKinds");

            routeIndex++;
        }

        ValidateIssues(issues);
    }

    private static void ValidatePublishReportContract(JsonElement root)
    {
        EnsureAllowedProperties(root, "$", "schema", "schemaVersion", "generatedAt", "siteName", "siteUrl", "baseUrl", "documents", "issues", "summary");

        var schemaVersion = ReadRequiredString(root, "$", "schemaVersion");
        if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schemaVersion '{schemaVersion}'. Expected '1.0'.");
        }

        var documents = ReadRequiredArray(root, "$", "documents");
        var issues = ReadRequiredArray(root, "$", "issues");
        var summary = ReadRequiredObject(root, "$", "summary");

        ReadRequiredString(root, "$", "generatedAt");
        ReadRequiredString(root, "$", "siteName");
        ReadRequiredString(root, "$", "baseUrl");
        ReadOptionalString(root, "$", "siteUrl");
        EnsureAllowedProperties(summary, "summary", "documentCount", "indexableCount", "nonIndexableCount", "errorCount", "warningCount", "publishIssueCount", "machineReadabilityIssueCount", "trustIssueCount", "representationGapCount");
        ReadRequiredInt(summary, "summary", "documentCount");
        ReadRequiredInt(summary, "summary", "indexableCount");
        ReadRequiredInt(summary, "summary", "nonIndexableCount");
        ReadRequiredInt(summary, "summary", "errorCount");
        ReadRequiredInt(summary, "summary", "warningCount");
        ReadOptionalInt(summary, "summary", "publishIssueCount");
        ReadOptionalInt(summary, "summary", "machineReadabilityIssueCount");
        ReadOptionalInt(summary, "summary", "trustIssueCount");
        ReadOptionalInt(summary, "summary", "representationGapCount");

        var documentIndex = 0;
        foreach (var document in documents.EnumerateArray())
        {
            var path = $"documents[{documentIndex}]";
            EnsureObject(document, path);
            EnsureAllowedProperties(document, path, "routeUrl", "outputPath", "canonical", "indexable", "lastModified", "contentType", "sourceItemId", "title", "description", "language", "author", "organization", "source", "originalSource", "reviewStatus", "summary", "updatedAt", "sourceReferences", "entityNames", "entitySummaries", "representationKinds", "representations", "schemaTypes", "structuredDataTypes", "semanticOutline", "sitemapIncluded", "searchIncluded", "rssIncluded", "atomFeedIncluded", "jsonFeedIncluded", "llmsIncluded", "llmsFullIncluded", "robotsIncluded", "manifestIncluded");
            ReadRequiredString(document, path, "routeUrl");
            ReadRequiredString(document, path, "outputPath");
            ReadRequiredString(document, path, "canonical");
            ReadRequiredBool(document, path, "indexable");
            ReadRequiredString(document, path, "lastModified");
            ReadOptionalString(document, path, "contentType");
            ReadOptionalString(document, path, "sourceItemId");
            ReadOptionalString(document, path, "title");
            ReadOptionalString(document, path, "description");
            ReadOptionalString(document, path, "language");
            ReadOptionalString(document, path, "author");
            ReadOptionalString(document, path, "organization");
            ReadOptionalString(document, path, "source");
            ReadOptionalString(document, path, "originalSource");
            ReadOptionalString(document, path, "reviewStatus");
            ReadOptionalString(document, path, "summary");
            ReadOptionalString(document, path, "updatedAt");
            ReadOptionalStringArray(document, path, "sourceReferences");
            ReadOptionalStringArray(document, path, "entityNames");
            ReadOptionalEntitySummaries(document, path, "entitySummaries");
            ReadOptionalStringArray(document, path, "representationKinds");
            ReadOptionalRepresentations(document, path, "representations");
            ReadOptionalStringArray(document, path, "schemaTypes");
            ReadOptionalStringArray(document, path, "structuredDataTypes");
            ReadOptionalSemanticOutline(document, path, "semanticOutline");
            ReadRequiredBool(document, path, "sitemapIncluded");
            ReadRequiredBool(document, path, "searchIncluded");
            ReadRequiredBool(document, path, "rssIncluded");
            ReadOptionalBool(document, path, "atomFeedIncluded");
            ReadOptionalBool(document, path, "jsonFeedIncluded");
            ReadOptionalBool(document, path, "llmsIncluded");
            ReadOptionalBool(document, path, "llmsFullIncluded");
            ReadOptionalBool(document, path, "robotsIncluded");
            ReadOptionalBool(document, path, "manifestIncluded");
            documentIndex++;
        }

        ValidateIssues(issues);
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
            EnsureObject(representation, itemPath);
            EnsureAllowedProperties(representation, itemPath, "kind", "url", "path", "generated", "indexable");
            ReadRequiredString(representation, itemPath, "kind");
            ReadRequiredString(representation, itemPath, "url");
            ReadRequiredString(representation, itemPath, "path");
            ReadRequiredBool(representation, itemPath, "generated");
            ReadRequiredBool(representation, itemPath, "indexable");
            index++;
        }
    }

    private static void ValidateIssues(JsonElement issues)
    {
        var issueIndex = 0;
        foreach (var issue in issues.EnumerateArray())
        {
            var path = $"issues[{issueIndex}]";
            EnsureObject(issue, path);
            EnsureAllowedProperties(issue, path, "severity", "code", "route", "message");
            var severity = ReadRequiredString(issue, path, "severity");
            if (!string.Equals(severity, "error", StringComparison.Ordinal) &&
                !string.Equals(severity, "warning", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{path}.severity must be 'error' or 'warning'.");
            }

            ReadRequiredString(issue, path, "code");
            ReadRequiredString(issue, path, "message");
            if (issue.TryGetProperty("route", out var route) &&
                route.ValueKind != JsonValueKind.Null &&
                route.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"{path}.route must be a string or null.");
            }

            issueIndex++;
        }
    }

    internal static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    internal static JsonElement ReadRequiredObject(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path}.{property} must be an object.");
        }

        return value;
    }

    internal static JsonElement ReadRequiredArray(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array.");
        }

        return value;
    }

    internal static string ReadRequiredString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{path}.{property} must be a non-empty string.");
        }

        return value.GetString()!;
    }

    internal static void ReadOptionalString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"{path}.{property} must be a string or null.");
        }
    }

    internal static int ReadRequiredInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException($"{path}.{property} must be an integer.");
        }

        return result;
    }

    internal static void ReadOptionalInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out _))
        {
            throw new InvalidDataException($"{path}.{property} must be an integer.");
        }
    }

    internal static int? TryReadOptionalInt(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;
    }

    internal static bool ReadRequiredBool(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException($"{path}.{property} must be a boolean.");
        }

        return value.GetBoolean();
    }

    internal static void ReadOptionalBool(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new InvalidDataException($"{path}.{property} must be a boolean.");
        }
    }

    internal static void ReadOptionalStringArray(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array or null.");
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException($"{path}.{property}[{index}] must be a string.");
            }

            index++;
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
            EnsureObject(item, itemPath);
            EnsureAllowedProperties(item, itemPath, "type", "name", "description");
            ReadRequiredString(item, itemPath, "type");
            ReadRequiredString(item, itemPath, "name");
            ReadOptionalString(item, itemPath, "description");
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
            EnsureObject(item, itemPath);
            EnsureAllowedProperties(item, itemPath, "level", "text");
            ReadRequiredInt(item, itemPath, "level");
            ReadRequiredString(item, itemPath, "text");
            index++;
        }
    }

    internal static void EnsureObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path} must be an object.");
        }
    }

    internal static void EnsureAllowedProperties(JsonElement element, string path, params string[] allowed)
    {
        var set = allowed.ToHashSet(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!set.Contains(property.Name))
            {
                throw new InvalidDataException($"{path}.{property.Name} is not allowed by the SEO report schema.");
            }
        }
    }

    internal static int? ReadOptionalInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!int.TryParse(value, out var result) || result < 0)
        {
            throw new InvalidDataException($"Expected a non-negative integer, got '{value}'.");
        }

        return result;
    }

    internal static IReadOnlySet<string> SplitCsv(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal static bool IsHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    internal sealed record SeoReportSnapshot(
        IReadOnlyDictionary<string, SeoRouteSnapshot> Routes,
        IReadOnlyList<SeoIssueSnapshot> Issues)
    {
        public static SeoReportSnapshot From(JsonElement root)
        {
            var routes = new Dictionary<string, SeoRouteSnapshot>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("documents", out var documents))
            {
                foreach (var document in documents.EnumerateArray())
                {
                    var url = ReadRequiredString(document, "document", "routeUrl");
                    routes[url] = new SeoRouteSnapshot(url, ReadRequiredBool(document, "document", "indexable"));
                }
            }
            else
            {
                foreach (var route in root.GetProperty("routes").EnumerateArray())
                {
                    var url = ReadRequiredString(route, "route", "url");
                    routes[url] = new SeoRouteSnapshot(url, ReadRequiredBool(route, "route", "indexable"));
                }
            }

            var issues = root.GetProperty("issues").EnumerateArray()
                .Select(x => new SeoIssueSnapshot(
                    ReadRequiredString(x, "issue", "severity"),
                    ReadRequiredString(x, "issue", "code"),
                    ReadString(x, "route"),
                    ReadRequiredString(x, "issue", "message")))
                .ToArray();

            return new SeoReportSnapshot(routes, issues);
        }
    }

    internal sealed record SeoRouteSnapshot(string Url, bool Indexable);

    internal sealed record SeoIssueSnapshot(string Severity, string Code, string? Route, string Message)
    {
        public string SortKey => $"{Severity}\u001f{Route}\u001f{Code}\u001f{Message}";
    }

    [GeneratedRegex(@"<meta\b(?=[^>]*(?:property|name)\s*=\s*[""'](?:og:image|twitter:image)[""'])(?=[^>]*content\s*=\s*[""']([^""']+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex SocialImageRegex();

    [GeneratedRegex(@"<img\b(?=[^>]*src\s*=\s*[""']([^""']+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex ImageSourceRegex();

    [GeneratedRegex(@"<a\b(?=[^>]*href\s*=\s*[""']([^""'#]+)[""'])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    internal static partial Regex AnchorHrefRegex();
}
