using System.Globalization;
using System.Text.Json;

namespace Bukit.Engine;

internal sealed class BuildManifestDigestReportWriter : IBuildReportWriter
{
    public string Name => "digest";

    public void Write(BuildReportWriterContext context)
    {
        var reports = BuildManifestDigestInventory.Create(context.ReportDir);
        var releaseBundle = Find(reports, "release-bundle-checksums.json");
        var artifactManifest = Find(reports, "artifact-manifest.json");
        var securityReport = Find(reports, "security-report.json");

        using var stream = File.Create(Path.Combine(context.ReportDir, "build-manifest-digest.json"));
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        BuildReporter.WriteArtifactContract(writer, BuildReporter.BuildManifestDigestSchema);
        writer.WriteString("generatedAt", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteNumber("reportCount", reports.Count);
        writer.WriteString("reportSetHash", BuildReportHashing.ComputeBundleHash(reports));
        writer.WriteString("artifactManifestHash", artifactManifest?.Hash);
        writer.WriteString("releaseBundleHash", releaseBundle?.Hash);
        writer.WriteString("securityReportHash", securityReport?.Hash);
        writer.WritePropertyName("reports");
        writer.WriteStartArray();
        foreach (var report in reports)
        {
            BuildReportFileEntryWriter.Write(writer, report);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static BuildReportFileEntry? Find(IReadOnlyList<BuildReportFileEntry> reports, string path)
        => reports.FirstOrDefault(report => string.Equals(report.Path, path, StringComparison.OrdinalIgnoreCase));
}
