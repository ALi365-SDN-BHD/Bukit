using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class PublishCommand
{
    internal static string? ResolveAuditReportPath(string outputDir)
    {
        var publish = Path.Combine(outputDir, ".bukit", "publish-audit-report.json");
        return File.Exists(publish) ? publish : null;
    }

    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var subcommand = command.GetArgument(0);
        if (string.Equals(subcommand, "audit", StringComparison.OrdinalIgnoreCase))
        {
            var reportPath = command.GetString("--report");
            var dir = command.GetString("--dir") ?? "dist";
            if (string.IsNullOrWhiteSpace(reportPath))
            {
                reportPath = ResolveAuditReportPath(dir);
                if (reportPath is null)
                {
                    Console.Error.WriteLine($"Publish report not found under {Path.GetFullPath(dir)} (looked for .bukit/publish-audit-report.json). Run a full build first.");
                    return 1;
                }
            }

            return await SeoCommand.AuditAsync(
                reportPath,
                dir,
                strict: command.GetBool("--strict"),
                external: command.GetBool("--external"),
                label: "Publish",
                contract: SeoReportValidator.AuditReportContract.PublishOnly);
        }

        if (string.Equals(subcommand, "diff", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var baseline = command.GetString("--baseline") ?? command.GetArgument(1);
                var current = command.GetString("--current") ?? command.GetArgument(2);
                return SeoCommand.Diff(
                    baseline,
                    current,
                    maxNewErrors: command.GetInt("--max-new-errors"),
                    maxNewWarnings: command.GetInt("--max-new-warnings"),
                    maxNewIssues: command.GetInt("--max-new-issues"),
                    failOnNewCodes: SeoReportValidator.SplitCsv(command.GetString("--fail-on-new-code")),
                    failOnRouteRemoved: command.GetBool("--fail-on-route-removed"),
                    failOnIndexableDrop: command.GetBool("--fail-on-indexable-drop"),
                    contract: SeoReportValidator.AuditReportContract.PublishOnly,
                    label: "Publish",
                    commandName: "publish");
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine($"Invalid Publish diff option: {ex.Message}");
                return 2;
            }
        }

        Console.Error.WriteLine("Usage: bukit publish audit [--dir dist] [--report publish-audit-report.json] [--strict] [--external]");
        Console.Error.WriteLine("       bukit publish diff --baseline old-report.json --current new-report.json [--max-new-errors n] [--max-new-warnings n] [--max-new-issues n] [--fail-on-new-code code1,code2] [--fail-on-route-removed] [--fail-on-indexable-drop]");
        return 2;
    }
}
