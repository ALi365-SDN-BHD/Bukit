using System.Text.Json;

namespace Bukit.Engine;

internal static class SeoRouteMapWriter
{
    private const string FileName = "seo-route-map.json";

    internal static void Write(string outputDir, SeoRouteMap map, string? reportDir = null)
    {
        var json = JsonSerializer.Serialize(map, SeoRouteMapJsonContext.Default.SeoRouteMap);
        var directory = reportDir ?? Path.Combine(outputDir, BuildReportPaths.ReportDirectoryName);
        FileWriter.WriteUtf8(directory, FileName, json + Environment.NewLine);
    }
}
