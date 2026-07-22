using System.Text.Json;

namespace Bukit.Engine;

internal sealed class BuildSummaryReportWriter : IBuildReportWriter
{
    public string Name => "build";

    public void Write(BuildReportWriterContext context)
    {
        var result = context.Result;
        using var stream = File.Create(Path.Combine(context.ReportDir, "build-report.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.BuildReportSchema);
        writer.WriteString("version", result.Version);
        writer.WriteString("startedAt", result.StartedAt);
        writer.WriteString("endedAt", result.EndedAt);
        writer.WriteNumber("durationMs", result.DurationMs);
        writer.WritePropertyName("environment");
        writer.WriteStartObject();
        writer.WriteString("os", result.Environment.OS);
        writer.WriteString("runtime", result.Environment.Runtime);
        writer.WriteBoolean("aot", result.Environment.Aot);
        writer.WriteEndObject();
        writer.WritePropertyName("project");
        writer.WriteStartObject();
        writer.WriteString("root", result.Project.Root);
        writer.WriteString("output", result.Project.Output);
        writer.WriteString("contentSource", result.Project.ContentSource);
        writer.WriteString("themeName", result.Project.ThemeName);
        writer.WriteEndObject();
        writer.WritePropertyName("summary");
        writer.WriteStartObject();
        writer.WriteNumber("pageCount", result.Summary.PageCount);
        writer.WriteNumber("routeCount", result.Summary.RouteCount);
        writer.WriteNumber("assetCount", result.Summary.AssetCount);
        writer.WriteNumber("mediaCount", result.Summary.MediaCount);
        writer.WriteNumber("pluginCount", result.Summary.PluginCount);
        writer.WriteNumber("warningCount", result.Summary.WarningCount);
        writer.WriteNumber("errorCount", result.Summary.ErrorCount);
        writer.WriteNumber("schemaErrorCount", result.Summary.SchemaErrorCount);
        writer.WriteEndObject();
        writer.WritePropertyName("incremental");
        writer.WriteStartObject();
        writer.WriteBoolean("enabled", result.Incremental.Enabled);
        writer.WriteNumber("cacheHitCount", result.Incremental.CacheHitCount);
        writer.WriteNumber("cacheMissCount", result.Incremental.CacheMissCount);
        writer.WriteEndObject();
        writer.WritePropertyName("generatedFiles");
        writer.WriteStartArray();
        foreach (var file in result.GeneratedFiles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStringValue(file);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
