using System.Text.Json;

namespace Bukit.Engine;

internal sealed class AssetsReportWriter : IBuildReportWriter
{
    public string Name => "assets";

    public void Write(BuildReportWriterContext context)
    {
        var assets = AssetsReportInventory.Create(context.OutputDir);
        using var stream = File.Create(Path.Combine(context.ReportDir, "assets.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.AssetsReportSchema);
        writer.WritePropertyName("assets");
        writer.WriteStartArray();
        foreach (var asset in assets)
        {
            writer.WriteStartObject();
            writer.WriteString("path", asset.Path);
            writer.WriteString("source", asset.Source);
            writer.WriteString("hash", asset.Hash);
            writer.WriteNumber("size", asset.Size);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
