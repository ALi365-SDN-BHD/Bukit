using System.Text.Json;

namespace Bukit.Engine;

internal sealed class ReleaseBundleChecksumsReportWriter : IBuildReportWriter
{
    public string Name => "release-bundle";

    public void Write(BuildReportWriterContext context)
    {
        var files = ReleaseBundleInventory.Create(context.ReportDir, context.OutputDir);
        using var stream = File.Create(Path.Combine(context.ReportDir, "release-bundle-checksums.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.ReleaseBundleChecksumsSchema);
        writer.WriteNumber("fileCount", files.Count);
        writer.WriteString("bundleHash", BuildReportHashing.ComputeBundleHash(files));
        writer.WritePropertyName("files");
        writer.WriteStartArray();
        foreach (var file in files)
        {
            BuildReportFileEntryWriter.Write(writer, file);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
