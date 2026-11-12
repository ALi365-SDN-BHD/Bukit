using System.Text.Json;
using Bukit.Config;

namespace Bukit.Engine;

internal static class PublishAuditReportWriter
{
    internal const string Schema = "https://bukit.dev/schemas/publish-audit-report.v1.json";

    internal static void Write(AppConfig config, string outputDir, SeoAuditReport report)
    {
        _ = config;
        var publishReport = PublishAuditBuilder.Build(report);
        Write(outputDir, publishReport);
    }

    internal static void Write(string outputDir, PublishAuditReport publishReport, string? reportDir = null)
    {
        var json = JsonSerializer.Serialize(publishReport, PublishAuditReportJsonContext.Default.PublishAuditReport);
        var directory = reportDir ?? Path.Combine(outputDir, BuildReportPaths.ReportDirectoryName);
        FileWriter.WriteUtf8(directory, "publish-audit-report.json", json + Environment.NewLine);
    }
}
