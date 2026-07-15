using System.Text.Json;
using Bukit.Config;

namespace Bukit.Engine.Analytics;

internal static class AnalyticsReportWriter
{
    internal const string Schema = "https://bukit.dev/schemas/analytics-report.v1.json";
    internal const string SchemaVersion = "1.0";
    internal const string FileName = "analytics-report.json";

    internal static void WriteIfEnabled(
        AppConfig config,
        string outputDir,
        AnalyticsBuildSnapshot snapshot)
    {
        var relativePath = Path.Combine(BuildReporter.ReportDirectoryName, FileName);
        var path = FileWriter.GetSafeFullPath(outputDir, relativePath);
        if (!config.Build.Report.Enabled)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        var reportDir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(reportDir);
        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString("schema", Schema);
        writer.WriteString("schemaVersion", SchemaVersion);
        writer.WriteBoolean("pluginEnabled", snapshot.PluginEnabled);
        writer.WriteBoolean("analyticsEnabled", snapshot.AnalyticsEnabled);
        writer.WriteBoolean("productionOnly", snapshot.ProductionOnly);
        writer.WriteString(
            "executionMode",
            snapshot.ExecutionMode == BuildExecutionMode.Production ? "production" : "development");
        writer.WritePropertyName("providerTypes");
        writer.WriteStartArray();
        foreach (var providerType in snapshot.ProviderTypes)
        {
            writer.WriteStringValue(providerType);
        }

        writer.WriteEndArray();
        writer.WriteNumber("processedHtml", snapshot.ProcessedHtml);
        writer.WriteNumber("injectedHtml", snapshot.InjectedHtml);
        writer.WritePropertyName("skippedByReason");
        writer.WriteStartObject();
        foreach (var reason in AnalyticsSkipReason.All)
        {
            if (snapshot.SkippedByReason.TryGetValue(reason, out var count))
            {
                writer.WriteNumber(reason, count);
            }
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
