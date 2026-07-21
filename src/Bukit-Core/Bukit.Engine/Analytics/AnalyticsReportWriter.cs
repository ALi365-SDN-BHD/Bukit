using System.Text.Json;
using Bukit.Config;

namespace Bukit.Engine.Analytics;

internal static class AnalyticsReportWriter
{
    internal const string Schema = "https://bukit.dev/schemas/analytics-report.v2.json";
    internal const string SchemaVersion = "2.0";
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
        WriteGoogleConsent(writer, snapshot.GoogleConsent);
        WriteCsp(writer, snapshot.Csp);
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

    private static void WriteGoogleConsent(Utf8JsonWriter writer, ResolvedGoogleConsent? consent)
    {
        writer.WritePropertyName("googleConsent");
        if (consent is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("mode", consent.Mode);
        writer.WritePropertyName("defaults");
        writer.WriteStartObject();
        writer.WriteString("adStorage", consent.AdStorage);
        writer.WriteString("analyticsStorage", consent.AnalyticsStorage);
        writer.WriteString("adUserData", consent.AdUserData);
        writer.WriteString("adPersonalization", consent.AdPersonalization);
        writer.WriteEndObject();
        if (consent.WaitForUpdateMs is { } waitForUpdateMs)
        {
            writer.WriteNumber("waitForUpdateMs", waitForUpdateMs);
        }
        else
        {
            writer.WriteNull("waitForUpdateMs");
        }

        writer.WriteEndObject();
    }

    private static void WriteCsp(Utf8JsonWriter writer, AnalyticsCspRequirements? csp)
    {
        writer.WritePropertyName("csp");
        if (csp is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("mode", "requirements-report");
        writer.WriteBoolean("completePolicy", false);
        WriteStringArray(writer, "inlineScriptSha256", csp.InlineScriptSha256);
        WriteStringArray(writer, "scriptSrcOrigins", csp.ScriptSrcOrigins);
        WriteStringArray(writer, "frameSrcOrigins", csp.FrameSrcOrigins);
        writer.WriteBoolean(
            "dynamicContainerDestinationsUnknown",
            csp.DynamicContainerDestinationsUnknown);
        writer.WriteEndObject();
    }

    private static void WriteStringArray(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
