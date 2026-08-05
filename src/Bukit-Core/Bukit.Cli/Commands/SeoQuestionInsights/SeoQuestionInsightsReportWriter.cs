using System.Text.Json;
using Bukit.Engine;

namespace Bukit.Cli.Commands.SeoQuestionInsights;

internal static class SeoQuestionInsightsReportWriter
{
    internal const string FileName = "seo-question-insights-report.json";

    internal static void Write(string outputDir, SeoQuestionInsightsReport report)
    {
        var json = JsonSerializer.Serialize(report, SeoQuestionInsightsJsonContext.Default.SeoQuestionInsightsReport);
        FileWriter.WriteUtf8(
            outputDir,
            Path.Combine(BuildReporter.ReportDirectoryName, FileName),
            json + Environment.NewLine);
    }
}
