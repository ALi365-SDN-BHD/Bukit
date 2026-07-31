using System.Text.Json;

namespace Bukit.Engine;

internal sealed class PublishUrlSnapshotReportWriter : IBuildReportWriter
{
    public string Name => "publish-url-snapshot";

    public void Write(BuildReportWriterContext context)
    {
        // Use WriteAsync instead.
    }

    public async Task WriteAsync(BuildReportWriterContext context, CancellationToken cancellationToken = default)
    {
        var snapshotPath = Path.Combine(context.ReportDir, "publish-url-snapshot.json");
        if (string.IsNullOrWhiteSpace(context.Config.Site.Url))
        {
            File.Delete(snapshotPath);
            return;
        }

        var snapshot = await PublishUrlSnapshotBuilder.BuildAsync(context.Config, context.Variants, cancellationToken).ConfigureAwait(false);
        using var stream = File.Create(snapshotPath);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        PublishUrlSnapshotJson.Write(writer, snapshot);
    }
}
