using System.Text.Json;

namespace Bukit.Cli.Commands;

public static class SeoCommand
{
    private const string ExpectedSchema = "https://bukit.dev/schemas/seo-report.v1.json";
    private const string ExpectedSchemaVersion = "1.0";

    private static string? ResolveSeoReportPath(string outputDir)
    {
        var preferred = Path.Combine(outputDir, ".bukit", "seo-report.json");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        var legacy = Path.Combine(outputDir, "seo-report.json");
        return File.Exists(legacy) ? legacy : null;
    }

    public static async Task<int> RunAsync(ArgReader reader)
    {
        var subcommand = reader.GetArg(1);
        if (string.Equals(subcommand, "audit", StringComparison.OrdinalIgnoreCase))
        {
            var reportPath = reader.GetOption("--report");
            var dir = reader.GetOption("--dir") ?? "dist";
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                reportPath = ResolveSeoReportPath(dir);
                if (reportPath is null)
                {
                    Console.Error.WriteLine($"SEO report not found under {Path.GetFullPath(dir)} (looked for .bukit/seo-report.json and seo-report.json). Run a full build first.");
                    return 1;
                }
            }

            return await AuditAsync(reportPath, dir, strict: reader.HasFlag("--strict"), external: reader.HasFlag("--external"));
        }

        if (string.Equals(subcommand, "diff", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var baseline = reader.GetOption("--baseline") ?? reader.GetArg(2);
                var current = reader.GetOption("--current") ?? reader.GetArg(3);
                return Diff(
                    baseline,
                    current,
                    maxNewErrors: SeoReportValidator.ReadOptionalInt(reader.GetOption("--max-new-errors")),
                    maxNewWarnings: SeoReportValidator.ReadOptionalInt(reader.GetOption("--max-new-warnings")),
                    maxNewIssues: SeoReportValidator.ReadOptionalInt(reader.GetOption("--max-new-issues")),
                    failOnNewCodes: SeoReportValidator.SplitCsv(reader.GetOption("--fail-on-new-code")),
                    failOnRouteRemoved: reader.HasFlag("--fail-on-route-removed"),
                    failOnIndexableDrop: reader.HasFlag("--fail-on-indexable-drop"));
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"Invalid SEO diff option: {ex.Message}");
                return 2;
            }
        }

        Console.Error.WriteLine("Usage: bukit seo audit [--dir dist] [--report seo-report.json] [--strict] [--external]");
        Console.Error.WriteLine("       bukit seo diff --baseline old-report.json --current new-report.json [--max-new-errors n] [--max-new-warnings n] [--max-new-issues n] [--fail-on-new-code code1,code2] [--fail-on-route-removed] [--fail-on-indexable-drop]");
        return 2;
    }

    internal static int Audit(string reportPath, bool strict)
        => AuditAsync(reportPath, Path.GetDirectoryName(Path.GetFullPath(reportPath)) ?? ".", strict, external: false).GetAwaiter().GetResult();

    internal static async Task<int> AuditAsync(string reportPath, string outputDir, bool strict, bool external)
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
            SeoReportValidator.ValidateReportContract(doc.RootElement);

            var summary = doc.RootElement.GetProperty("summary");
            var errorCount = SeoReportValidator.ReadRequiredInt(summary, "summary", "errorCount");
            var warningCount = SeoReportValidator.ReadRequiredInt(summary, "summary", "warningCount");
            var routeCount = SeoReportValidator.ReadRequiredInt(summary, "summary", "routeCount");

            Console.WriteLine($"SEO audit: routes={routeCount} errors={errorCount} warnings={warningCount}");
            if (doc.RootElement.TryGetProperty("issues", out var issues))
            {
                foreach (var issue in issues.EnumerateArray())
                {
                    var severity = SeoReportValidator.ReadString(issue, "severity");
                    var code = SeoReportValidator.ReadString(issue, "code");
                    var route = SeoReportValidator.ReadString(issue, "route") ?? "-";
                    var message = SeoReportValidator.ReadString(issue, "message");
                    Console.WriteLine($"{severity} {code} {route} {message}");
                }
            }

            if (external)
            {
                var externalResult = await SeoExternalAuditor.RunExternalAuditAsync(doc.RootElement, outputDir);
                errorCount += externalResult.Errors;
                warningCount += externalResult.Warnings;
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

    internal static int Diff(
        string? baselinePath,
        string? currentPath,
        int? maxNewErrors,
        int? maxNewWarnings,
        int? maxNewIssues,
        IReadOnlySet<string> failOnNewCodes,
        bool failOnRouteRemoved,
        bool failOnIndexableDrop)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || string.IsNullOrWhiteSpace(currentPath))
        {
            Console.Error.WriteLine("Usage: bukit seo diff --baseline old-report.json --current new-report.json");
            return 2;
        }

        try
        {
            using var baselineDoc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(baselinePath)));
            using var currentDoc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(currentPath)));
            SeoReportValidator.ValidateReportContract(baselineDoc.RootElement);
            SeoReportValidator.ValidateReportContract(currentDoc.RootElement);

            var baseline = SeoReportValidator.SeoReportSnapshot.From(baselineDoc.RootElement);
            var current = SeoReportValidator.SeoReportSnapshot.From(currentDoc.RootElement);

            var baselineIssues = baseline.Issues.ToHashSet();
            var currentIssues = current.Issues.ToHashSet();
            var newIssues = currentIssues.Except(baselineIssues).OrderBy(x => x.SortKey, StringComparer.Ordinal).ToArray();
            var resolvedIssues = baselineIssues.Except(currentIssues).OrderBy(x => x.SortKey, StringComparer.Ordinal).ToArray();
            var newErrors = newIssues.Count(x => string.Equals(x.Severity, "error", StringComparison.OrdinalIgnoreCase));
            var newWarnings = newIssues.Count(x => string.Equals(x.Severity, "warning", StringComparison.OrdinalIgnoreCase));

            var addedRoutes = current.Routes.Keys.Except(baseline.Routes.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var removedRoutes = baseline.Routes.Keys.Except(current.Routes.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            var indexableDrops = baseline.Routes
                .Where(x => x.Value.Indexable &&
                            current.Routes.TryGetValue(x.Key, out var route) &&
                            !route.Indexable)
                .Select(x => x.Key)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Console.WriteLine($"SEO diff: newIssues={newIssues.Length} newErrors={newErrors} newWarnings={newWarnings} resolvedIssues={resolvedIssues.Length} addedRoutes={addedRoutes.Length} removedRoutes={removedRoutes.Length} indexableDrops={indexableDrops.Length}");
            foreach (var issue in newIssues)
            {
                Console.WriteLine($"+ {issue.Severity} {issue.Code} {issue.Route ?? "-"} {issue.Message}");
            }

            foreach (var route in removedRoutes)
            {
                Console.WriteLine($"- route {route}");
            }

            foreach (var route in indexableDrops)
            {
                Console.WriteLine($"! indexable-drop {route}");
            }

            var failed = false;
            if (maxNewErrors is not null && newErrors > maxNewErrors.Value)
            {
                Console.Error.WriteLine($"SEO diff budget exceeded: new errors {newErrors} > {maxNewErrors.Value}.");
                failed = true;
            }

            if (maxNewWarnings is not null && newWarnings > maxNewWarnings.Value)
            {
                Console.Error.WriteLine($"SEO diff budget exceeded: new warnings {newWarnings} > {maxNewWarnings.Value}.");
                failed = true;
            }

            if (maxNewIssues is not null && newIssues.Length > maxNewIssues.Value)
            {
                Console.Error.WriteLine($"SEO diff budget exceeded: new issues {newIssues.Length} > {maxNewIssues.Value}.");
                failed = true;
            }

            foreach (var code in failOnNewCodes)
            {
                if (newIssues.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.Error.WriteLine($"SEO diff budget exceeded: new issue code {code}.");
                    failed = true;
                }
            }

            if (failOnRouteRemoved && removedRoutes.Length > 0)
            {
                Console.Error.WriteLine("SEO diff budget exceeded: routes were removed.");
                failed = true;
            }

            if (failOnIndexableDrop && indexableDrops.Length > 0)
            {
                Console.Error.WriteLine("SEO diff budget exceeded: indexable routes became non-indexable.");
                failed = true;
            }

            return failed ? 1 : 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report JSON: {ex.Message}");
            return 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Failed to read SEO report: {ex.Message}");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid SEO report: {ex.Message}");
            return 2;
        }
    }
}
