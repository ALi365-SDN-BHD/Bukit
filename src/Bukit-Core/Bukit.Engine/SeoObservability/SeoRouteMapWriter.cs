using System.Text.Json;

namespace Bukit.Engine;

internal static class SeoRouteMapWriter
{
    private const string FileName = "seo-route-map.json";

    internal static void Write(string outputDir, SeoRouteMap map)
    {
        var json = JsonSerializer.Serialize(map, SeoRouteMapJsonContext.Default.SeoRouteMap);
        FileWriter.WriteUtf8(
            outputDir,
            Path.Combine(BuildReporter.ReportDirectoryName, FileName),
            json + Environment.NewLine);
    }
}
