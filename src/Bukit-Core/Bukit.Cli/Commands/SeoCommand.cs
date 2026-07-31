using System.Text.Json;
using Bukit.Cli.Shared.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class SeoCommand
{
    internal static string? ResolveAuditReportPath(string outputDir)
    {
        var preferred = Path.Combine(outputDir, ".bukit", "seo-report.json");
        if (File.Exists(preferred))
        {
            return preferred;
        }

        return null;
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
        => await RunAsync(command, "SEO", "seo");

    internal static async Task<int> RunAsync(CliBoundCommand command, string label, string commandName)
    {
        var subcommand = command.GetArgument(0);
        if (string.Equals(subcommand, "audit", StringComparison.OrdinalIgnoreCase))
        {
            var reportPath = command.GetString("--report");
            var reportSpecified = !string.IsNullOrWhiteSpace(reportPath);
            var dir = command.GetString("--dir") ?? "dist";
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                reportPath = ResolveAuditReportPath(dir);
                if (reportPath is null)
                {
                    Console.Error.WriteLine($"{label} report not found under {Path.GetFullPath(dir)} (looked for .bukit/seo-report.json). Run a full build first.");
                    return 1;
                }
            }
            return await AuditAsync(
                reportPath,
                dir,
                strict: command.GetBool("--strict"),
                external: command.GetBool("--external"),
                label: label,
                contract: SeoReportValidator.AuditReportContract.SeoOnly,
                reportSpecified: reportSpecified);
        }

        if (string.Equals(subcommand, "diff", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var baseline = command.GetString("--baseline") ?? command.GetArgument(1);
                var current = command.GetString("--current") ?? command.GetArgument(2);
                return Diff(
                    baseline,
                    current,
                    maxNewErrors: command.GetInt("--max-new-errors"),
                    maxNewWarnings: command.GetInt("--max-new-warnings"),
                    maxNewIssues: command.GetInt("--max-new-issues"),
                    failOnNewCodes: SeoReportValidator.SplitCsv(command.GetString("--fail-on-new-code")),
                    failOnRouteRemoved: command.GetBool("--fail-on-route-removed"),
                    failOnIndexableDrop: command.GetBool("--fail-on-indexable-drop"),
                    contract: SeoReportValidator.AuditReportContract.SeoOnly,
                    label: label,
                    commandName: commandName);
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"Invalid {label} diff option: {ex.Message}");
                return 2;
            }
        }

        Console.Error.WriteLine($"Usage: bukit {commandName} audit [--dir dist] [--report seo-report.json] [--strict] [--external]");
        Console.Error.WriteLine($"       bukit {commandName} diff --baseline old-report.json --current new-report.json [--max-new-errors n] [--max-new-warnings n] [--max-new-issues n] [--fail-on-new-code code1,code2] [--fail-on-route-removed] [--fail-on-indexable-drop]");
        return 2;
    }

    internal static async Task<int> AuditAsync(
        string reportPath,
        string outputDir,
        bool strict,
        bool external,
        string label,
        SeoReportValidator.AuditReportContract contract = SeoReportValidator.AuditReportContract.SeoOnly,
        bool reportSpecified = false)
    {
        var fullPath = Path.GetFullPath(reportPath);
        if (!File.Exists(fullPath))
        {
            Console.Error.WriteLine($"{label} report not found: {fullPath}");
            return 2;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(fullPath));
            AuditReportContractValidator.ValidateReportContract(doc.RootElement, contract);
            var summary = doc.RootElement.GetProperty("summary");
            var errorCount = SeoReportValidator.ReadRequiredInt(summary, "summary", "errorCount");
            var warningCount = SeoReportValidator.ReadRequiredInt(summary, "summary", "warningCount");
            var routeCount = SeoReportValidator.ReadRequiredInt(
                summary,
                "summary",
                doc.RootElement.TryGetProperty("documents", out _) ? "documentCount" : "routeCount");

            Console.WriteLine($"{label} audit: routes={routeCount} errors={errorCount} warnings={warningCount}");
            WriteSummaryBuckets(summary, label);
            if (doc.RootElement.TryGetProperty("issues", out var issues))
            {
                WriteIssues(ReadIssueRows(issues), label);
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
            Console.Error.WriteLine($"Invalid {label} report JSON: {ex.Message}");
            return 2;
        }
        catch (KeyNotFoundException)
        {
            Console.Error.WriteLine($"Invalid {label} report: missing summary.");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid {label} report: {ex.Message}");
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
        bool failOnIndexableDrop,
        SeoReportValidator.AuditReportContract contract,
        string label,
        string commandName)
    {
        if (string.IsNullOrWhiteSpace(baselinePath) || string.IsNullOrWhiteSpace(currentPath))
        {
            Console.Error.WriteLine($"Usage: bukit {commandName} diff --baseline old-report.json --current new-report.json");
            return 2;
        }

        try
        {
            using var baselineDoc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(baselinePath)));
            using var currentDoc = JsonDocument.Parse(File.ReadAllText(Path.GetFullPath(currentPath)));
            var baselineContract = AuditReportContractValidator.ValidateReportContract(baselineDoc.RootElement, contract);
            var currentContract = AuditReportContractValidator.ValidateReportContract(currentDoc.RootElement, contract);
            if (baselineContract != currentContract &&
                contract != SeoReportValidator.AuditReportContract.SeoOrPublish)
            {
                throw new InvalidDataException(
                    $"Cannot diff different report schema kinds: baseline uses {baselineContract} and current uses {currentContract}. " +
                    "SEO diff only supports seo-report schema.");
            }

            var baseline = AuditReportContractValidator.ReadDiffSnapshot(baselineDoc.RootElement);
            var current = AuditReportContractValidator.ReadDiffSnapshot(currentDoc.RootElement);

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

            Console.WriteLine($"{label} diff: newIssues={newIssues.Length} newErrors={newErrors} newWarnings={newWarnings} resolvedIssues={resolvedIssues.Length} addedRoutes={addedRoutes.Length} removedRoutes={removedRoutes.Length} indexableDrops={indexableDrops.Length}");
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
                Console.Error.WriteLine($"{label} diff budget exceeded: new errors {newErrors} > {maxNewErrors.Value}.");
                failed = true;
            }

            if (maxNewWarnings is not null && newWarnings > maxNewWarnings.Value)
            {
                Console.Error.WriteLine($"{label} diff budget exceeded: new warnings {newWarnings} > {maxNewWarnings.Value}.");
                failed = true;
            }

            if (maxNewIssues is not null && newIssues.Length > maxNewIssues.Value)
            {
                Console.Error.WriteLine($"{label} diff budget exceeded: new issues {newIssues.Length} > {maxNewIssues.Value}.");
                failed = true;
            }

            foreach (var code in failOnNewCodes)
            {
                if (newIssues.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)))
                {
                    Console.Error.WriteLine($"{label} diff budget exceeded: new issue code {code}.");
                    failed = true;
                }
            }

            if (failOnRouteRemoved && removedRoutes.Length > 0)
            {
                Console.Error.WriteLine($"{label} diff budget exceeded: routes were removed.");
                failed = true;
            }

            if (failOnIndexableDrop && indexableDrops.Length > 0)
            {
                Console.Error.WriteLine($"{label} diff budget exceeded: indexable routes became non-indexable.");
                failed = true;
            }

            return failed ? 1 : 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid {label} report JSON: {ex.Message}");
            return 2;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Failed to read {label} report: {ex.Message}");
            return 2;
        }
        catch (InvalidDataException ex)
        {
            Console.Error.WriteLine($"Invalid {label} report: {ex.Message}");
            return 2;
        }
    }

    private static void WriteSummaryBuckets(JsonElement summary, string label)
    {
        var publishIssues = SeoReportValidator.TryReadOptionalInt(summary, "publishIssueCount");
        var machineReadability = SeoReportValidator.TryReadOptionalInt(summary, "machineReadabilityIssueCount");
        var trustIssues = SeoReportValidator.TryReadOptionalInt(summary, "trustIssueCount");
        var representationGaps = SeoReportValidator.TryReadOptionalInt(summary, "representationGapCount");
        if (publishIssues is null && machineReadability is null && trustIssues is null && representationGaps is null)
        {
            return;
        }

        Console.WriteLine($"{label} summary: publishIssues={publishIssues ?? 0} machineReadability={machineReadability ?? 0} trust={trustIssues ?? 0} representationGaps={representationGaps ?? 0}");
    }

    private static IReadOnlyList<AuditIssueRow> ReadIssueRows(JsonElement issues)
        => issues.EnumerateArray()
            .Select(issue => new AuditIssueRow(
                Severity: SeoReportValidator.ReadString(issue, "severity") ?? "warning",
                Code: SeoReportValidator.ReadString(issue, "code") ?? "unknown",
                Route: SeoReportValidator.ReadString(issue, "route") ?? "-",
                Message: SeoReportValidator.ReadString(issue, "message") ?? string.Empty))
            .ToArray();

    private static void WriteIssues(IReadOnlyList<AuditIssueRow> issues, string label)
    {
        if (issues.Count == 0)
        {
            return;
        }

        var grouped = issues
            .GroupBy(issue => GetIssueGroup(issue.Code), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => IssueGroupOrder(group.Key))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Console.WriteLine($"{label} issues by group:");
        foreach (var group in grouped)
        {
            var errors = group.Count(issue => issue.Severity.Equals("error", StringComparison.OrdinalIgnoreCase));
            var warnings = group.Count(issue => issue.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase));
            Console.WriteLine($"  {group.Key}: errors={errors} warnings={warnings}");
        }

        foreach (var group in grouped)
        {
            Console.WriteLine($"=== {FormatIssueGroupName(group.Key)} Issues ===");
            foreach (var issue in group)
            {
                Console.WriteLine($"{issue.Severity} {issue.Code} {issue.Route} {issue.Message}");
            }
        }
    }

    private static string GetIssueGroup(string? code)
    {
        if (code is null)
        {
            return "seo";
        }

        if (code.StartsWith("publish.", StringComparison.OrdinalIgnoreCase))
        {
            return "publish";
        }

        if (code.StartsWith("geo.", StringComparison.OrdinalIgnoreCase))
        {
            return "geo";
        }

        return "seo";
    }

    private static int IssueGroupOrder(string group)
        => group.ToLowerInvariant() switch
        {
            "seo" => 0,
            "publish" => 1,
            "geo" => 2,
            _ => 3
        };

    private static string FormatIssueGroupName(string group)
        => group.ToLowerInvariant() switch
        {
            "seo" => "SEO",
            "geo" => "GEO",
            _ => char.ToUpperInvariant(group[0]) + group[1..].ToLowerInvariant()
        };

    private sealed record AuditIssueRow(string Severity, string Code, string Route, string Message);
}
