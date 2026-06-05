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

    internal static void Write(string outputDir, PublishAuditReport publishReport)
    {
        var json = JsonSerializer.Serialize(publishReport, PublishAuditReportJsonContext.Default.PublishAuditReport);
        FileWriter.WriteUtf8(outputDir, Path.Combine(BuildReporter.ReportDirectoryName, "publish-audit-report.json"), json + Environment.NewLine);
    }
}
