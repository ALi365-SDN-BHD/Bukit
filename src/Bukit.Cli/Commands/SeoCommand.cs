using System.Text.Json;

namespace Bukit.Cli.Commands;

public static class SeoCommand
{
    private const string ExpectedSchema = "https://bukit.dev/schemas/seo-report.v1.json";
    private const string ExpectedSchemaVersion = "1.0";

    public static Task<int> RunAsync(ArgReader reader)
    {
        var subcommand = reader.GetArg(1);
        if (!string.Equals(subcommand, "audit", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Usage: bukit seo audit [--dir dist] [--report seo-report.json] [--strict]");
            return Task.FromResult(2);
        }

        var reportPath = reader.GetOption("--report");
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            var dir = reader.GetOption("--dir") ?? "dist";
            reportPath = Path.Combine(dir, "seo-report.json");
        }

        return Task.FromResult(Audit(reportPath, strict: reader.HasFlag("--strict")));
    }

    internal static int Audit(string reportPath, bool strict)
    {
        var fullPath = Path.GetFullPath(reportPath);
        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"SEO report not found: {fullPath}");
            return 2;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(fullPath));
            ValidateReportContract(doc.RootElement);

            var summary = doc.RootElement.GetProperty("summary");
            var errorCount = ReadRequiredInt(summary, "summary", "errorCount");
            var warningCount = ReadRequiredInt(summary, "summary", "warningCount");
            var routeCount = ReadRequiredInt(summary, "summary", "routeCount");

            Console.WriteLine($"SEO audit: routes={routeCount} errors={errorCount} warnings={warningCount}");
            if (doc.RootElement.TryGetProperty("issues", out var issues))
            {
                foreach (var issue in issues.EnumerateArray())
                {
                    var severity = ReadString(issue, "severity");
                    var code = ReadString(issue, "code");
                    var route = ReadString(issue, "route") ?? "-";
                    var message = ReadString(issue, "message");
                    Console.WriteLine($"{severity} {code} {route} {message}");
                }
            }

            return errorCount > 0 || (strict && warningCount > 0) ? 1 : 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report JSON: {ex.Message}");
            return 2;
        }
        catch (KeyNotFoundException)
        {
            Console.Error.WriteLine("Invalid SEO report: missing summary.");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report: {ex.Message}");
            return 2;
        }
    }

    private static void ValidateReportContract(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("root must be a JSON object.");
        }

        var schema = ReadRequiredString(root, "$", "schema");
        if (!string.Equals(schema, ExpectedSchema, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schema '{schema}'. Expected '{ExpectedSchema}'.");
        }

        var schemaVersion = ReadRequiredString(root, "$", "schemaVersion");
        if (!string.Equals(schemaVersion, ExpectedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"unsupported schemaVersion '{schemaVersion}'. Expected '{ExpectedSchemaVersion}'.");
        }

        var routes = ReadRequiredArray(root, "$", "routes");
        var issues = ReadRequiredArray(root, "$", "issues");
        var summary = ReadRequiredObject(root, "$", "summary");

        ReadRequiredInt(summary, "summary", "routeCount");
        ReadRequiredInt(summary, "summary", "indexableCount");
        ReadRequiredInt(summary, "summary", "nonIndexableCount");
        ReadRequiredInt(summary, "summary", "errorCount");
        ReadRequiredInt(summary, "summary", "warningCount");

        var routeIndex = 0;
        foreach (var route in routes.EnumerateArray())
        {
            var path = $"routes[{routeIndex}]";
            EnsureObject(route, path);
            ReadRequiredString(route, path, "url");
            ReadRequiredString(route, path, "outputPath");
            ReadRequiredString(route, path, "canonical");
            ReadRequiredBool(route, path, "indexable");
            ReadRequiredBool(route, path, "sitemapIncluded");
            ReadRequiredBool(route, path, "searchIncluded");
            ReadRequiredBool(route, path, "rssIncluded");
            ReadRequiredArray(route, path, "alternates");
            ReadRequiredArray(route, path, "schemaTypes");
            routeIndex++;
        }

        var issueIndex = 0;
        foreach (var issue in issues.EnumerateArray())
        {
            var path = $"issues[{issueIndex}]";
            EnsureObject(issue, path);
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

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static JsonElement ReadRequiredObject(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path}.{property} must be an object.");
        }

        return value;
    }

    private static JsonElement ReadRequiredArray(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException($"{path}.{property} must be an array.");
        }

        return value;
    }

    private static string ReadRequiredString(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException($"{path}.{property} must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static int ReadRequiredInt(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new InvalidDataException($"{path}.{property} must be an integer.");
        }

        return result;
    }

    private static bool ReadRequiredBool(JsonElement element, string path, string property)
    {
        if (!element.TryGetProperty(property, out var value) ||
            (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False))
        {
            throw new InvalidDataException($"{path}.{property} must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static void EnsureObject(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"{path} must be an object.");
        }
    }
}
