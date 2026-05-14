using System.Text.Json;

namespace Bukit.Cli.Commands;

public static class SeoCommand
{
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
            var summary = doc.RootElement.GetProperty("summary");
            var errorCount = summary.TryGetProperty("errorCount", out var e) ? e.GetInt32() : 0;
            var warningCount = summary.TryGetProperty("warningCount", out var w) ? w.GetInt32() : 0;
            var routeCount = summary.TryGetProperty("routeCount", out var r) ? r.GetInt32() : 0;

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
    }

    private static string? ReadString(JsonElement element, string property)
        => element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
}
