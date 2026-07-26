using System.Text.Json;

namespace Bukit.Engine;

internal sealed class PublishUrlSnapshotReportWriter : IBuildReportWriter
{
    public string Name => "publish-url-snapshot";

    public void Write(BuildReportWriterContext context)
    {
        var snapshotPath = Path.Combine(context.ReportDir, "publish-url-snapshot.json");
        if (string.IsNullOrWhiteSpace(context.Config.Site.Url))
        {
            File.Delete(snapshotPath);
            return;
        }

        var snapshot = PublishUrlSnapshotBuilder.Build(context.Config, context.Variants);
        using var stream = File.Create(snapshotPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        PublishUrlSnapshotJson.Write(writer, snapshot);
    }
}
