using System.Text.Json;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class GeoCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var subcommand = command.GetArgument(0);
        if (string.Equals(subcommand, "audit", StringComparison.OrdinalIgnoreCase))
        {
            var dir = command.GetString("--dir") ?? "dist";
            return await AuditAsync(dir);
        }

        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("Usage: bukit geo audit [--dir dist]");
    }

    private static string? ResolveSeoReportPath(string outputDir)
    {
        var preferred = Path.Combine(outputDir, ".bukit", "seo-report.json");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        return null;
    }

    private static async Task<int> AuditAsync(string outputDir)
    {
        var fullDir = Path.GetFullPath(outputDir);
        if (!Directory.Exists(fullDir))
        {
            Console.Error.WriteLine($"Output directory not found: {fullDir}");
            return 2;
        }

        var reportPath = ResolveSeoReportPath(fullDir);
        var geoEnhanced = 0;
        var geoTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var llmsTxtExists = File.Exists(Path.Combine(fullDir, "llms.txt"));
        var llmsFullTxtExists = File.Exists(Path.Combine(fullDir, "llms-full.txt"));
        var robotsTxtExists = File.Exists(Path.Combine(fullDir, "robots.txt"));
        var geoReportExists = File.Exists(Path.Combine(fullDir, ".bukit", "geo-report.json"));

        if (reportPath is null)
        {
            Console.Error.WriteLine($"Audit report not found under {fullDir} (looked for .bukit/seo-report.json). Run a full build first.");
            return 1;
        }

        try
        {
            using var doc = await JsonDocument.ParseAsync(File.OpenRead(reportPath));

            foreach (var document in EnumerateAuditDocuments(doc.RootElement))
            {
                if (document.TryGetProperty("schemaTypes", out var types))
                {
                    var routeHasGeoType = false;
                    foreach (var type in types.EnumerateArray())
                    {
                        if (type.GetString() is not { } t)
                        {
                            continue;
                        }

                        geoTypes.Add(t);
                        if (IsGeoSchemaType(t))
                        {
                            routeHasGeoType = true;
                        }
                    }

                    if (routeHasGeoType)
                    {
                        geoEnhanced++;
                    }
                }
            }

            Console.WriteLine("=== GEO Audit ===");
            Console.WriteLine($"  llms.txt: {(llmsTxtExists ? "present" : "missing")}");
            Console.WriteLine($"  llms-full.txt: {(llmsFullTxtExists ? "present" : "missing")}");
            Console.WriteLine($"  robots.txt: {(robotsTxtExists ? "present" : "missing")}");
            Console.WriteLine($"  geo-report.json: {(geoReportExists ? "present" : "missing")}");
            Console.WriteLine($"  Geo-enhanced routes: {geoEnhanced}");
            Console.WriteLine($"  Schema types: {string.Join(", ", geoTypes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}");

            if (doc.RootElement.TryGetProperty("summary", out var summary))
            {
                if (summary.TryGetProperty("llmsTxtGenerated", out var llmsField) && llmsField.GetBoolean())
                {
                    Console.WriteLine($"  Report confirms llms.txt generated.");
                }

                if (summary.TryGetProperty("geoEnhancedCount", out var geoCountField) && geoCountField.TryGetInt32(out var geoCount))
                {
                    Console.WriteLine($"  Report geoEnhancedCount: {geoCount}");
                }

                if (summary.TryGetProperty("geoScore", out var geoScoreField) && geoScoreField.TryGetInt32(out var score))
                {
                    Console.WriteLine($"  GEO Score: {score}/100");
                }
            }

            if (!llmsTxtExists)
            {
                Console.WriteLine("  Recommendation: Enable site.seo.geo.llmsTxt to generate llms.txt.");
            }

            if (geoEnhanced == 0)
            {
                Console.WriteLine("  Recommendation: Use geo.schema_type, geo.faq, or geo.steps in content front matter.");
            }

            var issues = 0;
            if (doc.RootElement.TryGetProperty("issues", out var issuesArray))
            {
                foreach (var issue in issuesArray.EnumerateArray())
                {
                    var code = issue.TryGetProperty("code", out var c) ? c.GetString() : null;
                    if (code is not null && code.StartsWith("geo.", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"  {issue.GetProperty("severity").GetString()} {code} {issue.GetProperty("message").GetString()}");
                        issues++;
                    }
                }
            }

            if (issues > 0)
            {
                Console.WriteLine($"  Total GEO issues: {issues}");
            }
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid audit report JSON: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static IEnumerable<JsonElement> EnumerateAuditDocuments(JsonElement root)
    {
        if (root.TryGetProperty("documents", out var documents))
        {
            foreach (var document in documents.EnumerateArray())
            {
                yield return document;
            }

            yield break;
        }

        if (root.TryGetProperty("routes", out var routes))
        {
            foreach (var route in routes.EnumerateArray())
            {
                yield return route;
            }
        }
    }

    private static bool IsGeoSchemaType(string value)
        => value is "FAQPage" or "HowTo" or "Person" or "Article" or "NewsArticle" or "SpeakableSpecification";
}
