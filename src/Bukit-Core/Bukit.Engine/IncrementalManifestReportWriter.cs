using System.Text.Json;

namespace Bukit.Engine;

internal sealed class IncrementalManifestReportWriter : IBuildReportWriter
{
    public string Name => "incremental";

    public void Write(BuildReportWriterContext context)
    {
        using var stream = File.Create(Path.Combine(context.ReportDir, "incremental-manifest.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.IncrementalManifestSchema);
        writer.WriteBoolean("enabled", context.Result.Incremental.Enabled);
        writer.WriteNumber("cacheHitCount", context.Result.Incremental.CacheHitCount);
        writer.WriteNumber("cacheMissCount", context.Result.Incremental.CacheMissCount);
        writer.WritePropertyName("renderReasons");
        writer.WriteStartObject();
        foreach (var reason in context.Variants
                     .SelectMany(variant => variant.RenderReasons)
                     .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteNumber(reason.Key, reason.Sum(item => item.Value));
        }

        writer.WriteEndObject();
        writer.WritePropertyName("variants");
        writer.WriteStartArray();
        foreach (var variant in context.Variants.OrderBy(item => item.Language, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteStartObject();
            writer.WriteString("language", variant.Language);
            writer.WriteString("outputDir", variant.OutputDir);
            writer.WriteNumber("renderedCount", variant.RenderedCount);
            writer.WriteNumber("skippedCount", variant.SkippedCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
