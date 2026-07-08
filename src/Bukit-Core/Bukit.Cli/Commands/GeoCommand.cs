using System.Text.Json;
using Bukit.Cli.Shared.Cli.Binding;

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

    internal static string? ResolveGeoReportPath(string outputDir)
    {
        var path = Path.Combine(outputDir, ".bukit", "geo-report.json");
        return File.Exists(path) ? path : null;
    }

    private static async Task<int> AuditAsync(string outputDir)
    {
        var fullDir = Path.GetFullPath(outputDir);
        if (!Directory.Exists(fullDir))
        {
            Console.Error.WriteLine($"Output directory not found: {fullDir}");
            return 2;
        }

        var reportPath = ResolveGeoReportPath(fullDir);
        var llmsTxtExists = File.Exists(Path.Combine(fullDir, "llms.txt"));
        var llmsFullTxtExists = File.Exists(Path.Combine(fullDir, "llms-full.txt"));
        var robotsTxtExists = File.Exists(Path.Combine(fullDir, "robots.txt"));

        if (reportPath is null)
        {
            Console.Error.WriteLine($"GEO report not found under {fullDir} (looked for .bukit/geo-report.json). Run a full build first.");
            return 1;
        }

        try
        {
            using var doc = await JsonDocument.ParseAsync(File.OpenRead(reportPath));
            ValidateGeoReportContract(doc.RootElement);

            var geoScore = doc.RootElement.GetProperty("geoScore").GetInt32();
            var llmsTxtGenerated = doc.RootElement.GetProperty("llmsTxtGenerated").GetBoolean();
            var llmsFullTxtGenerated = doc.RootElement.GetProperty("llmsFullTxtGenerated").GetBoolean();
            var geoEnhanced = doc.RootElement.GetProperty("geoEnhancedCount").GetInt32();
            var geoTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var route in doc.RootElement.GetProperty("geoEnhancedRoutes").EnumerateArray())
            {
                foreach (var type in route.GetProperty("schemaTypes").EnumerateArray())
                {
                    if (type.GetString() is { } value)
                    {
                        geoTypes.Add(value);
                    }
                }
            }

            Console.WriteLine("=== GEO Audit ===");
            Console.WriteLine($"  llms.txt: {(llmsTxtExists ? "present" : "missing")}");
            Console.WriteLine($"  llms-full.txt: {(llmsFullTxtExists ? "present" : "missing")}");
            Console.WriteLine($"  robots.txt: {(robotsTxtExists ? "present" : "missing")}");
            Console.WriteLine("  geo-report.json: present");
            Console.WriteLine($"  Geo-enhanced routes: {geoEnhanced}");
            Console.WriteLine($"  Schema types: {string.Join(", ", geoTypes.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}");
            Console.WriteLine($"  Report llms.txt generated: {llmsTxtGenerated}");
            Console.WriteLine($"  Report llms-full.txt generated: {llmsFullTxtGenerated}");
            Console.WriteLine($"  GEO Score: {geoScore}/100");

            if (!llmsTxtExists && llmsTxtGenerated)
            {
                Console.WriteLine("  Warning: geo report says llms.txt was generated, but the file is missing.");
            }
            else if (!llmsTxtExists)
            {
                Console.WriteLine("  Recommendation: Enable site.seo.geo.llmsTxt to generate llms.txt.");
            }
            else if (!llmsTxtGenerated)
            {
                Console.WriteLine("  Warning: llms.txt exists, but geo report marks it as not generated.");
            }

            if (!llmsFullTxtExists && llmsFullTxtGenerated)
            {
                Console.WriteLine("  Warning: geo report says llms-full.txt was generated, but the file is missing.");
            }
            else if (llmsFullTxtExists && !llmsFullTxtGenerated)
            {
                Console.WriteLine("  Warning: llms-full.txt exists, but geo report marks it as not generated.");
            }

            if (geoEnhanced == 0)
            {
                Console.WriteLine("  Recommendation: Use geo.schema_type, geo.faq, or geo.steps in content front matter.");
            }
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid GEO report JSON: {ex.Message}");
            return 1;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid GEO report: {ex.Message}");
            return 1;
        }

        return 0;
    }

    internal static void ValidateGeoReportContract(JsonElement root)
    {
        var schema = root.TryGetProperty("schema", out var schemaElement) ? schemaElement.GetString() : null;
        if (!string.Equals(schema, "https://bukit.dev/schemas/geo-report.v1.json", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected 'https://bukit.dev/schemas/geo-report.v1.json' in schema.");
        }

        var schemaVersion = root.TryGetProperty("schemaVersion", out var versionElement) ? versionElement.GetString() : null;
        if (!string.Equals(schemaVersion, "1.0", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Expected schemaVersion '1.0'.");
        }

        RequireProperty(root, "geoScore", JsonValueKind.Number);
        RequireProperty(root, "llmsTxtGenerated", JsonValueKind.True, JsonValueKind.False);
        RequireProperty(root, "llmsFullTxtGenerated", JsonValueKind.True, JsonValueKind.False);
        RequireProperty(root, "geoEnhancedCount", JsonValueKind.Number);
        RequireProperty(root, "geoEnhancedRoutes", JsonValueKind.Array);
    }

    private static void RequireProperty(JsonElement root, string name, params JsonValueKind[] allowedKinds)
    {
        if (!root.TryGetProperty(name, out var value) || !allowedKinds.Contains(value.ValueKind))
        {
            throw new InvalidDataException($"{name} is required.");
        }
    }
}
