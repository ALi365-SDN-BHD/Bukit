using System.Globalization;
using System.Text.Json;

namespace Bukit.Engine;

internal sealed class ArtifactManifestReportWriter : IBuildReportWriter
{
    public string Name => "artifact-manifest";

    public void Write(BuildReportWriterContext context)
    {
        var artifacts = ArtifactManifestInventory.Create(
            context.ReportDir,
            context.ReportsEnabled ? context.OutputDir : null,
            context.ReportsEnabled ? context.Variants : null);
        using var stream = File.Create(Path.Combine(context.ReportDir, "artifact-manifest.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.ArtifactManifestSchema);
        writer.WriteString("generatedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteNumber("artifactCount", artifacts.Count);
        writer.WriteString("artifactSetHash", BuildReportHashing.ComputeBundleHash(artifacts));
        writer.WritePropertyName("artifacts");
        writer.WriteStartArray();
        foreach (var artifact in artifacts)
        {
            BuildReportFileEntryWriter.Write(writer, artifact);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
