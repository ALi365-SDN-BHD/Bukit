namespace Bukit.Importing;

internal static class HtmlDemoScanner
{
    internal static List<DiscoveredPage> Scan(string inputPath, RouteMapConfig? routeMap = null)
    {
        var htmlFiles = Directory.GetFiles(inputPath, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (htmlFiles.Count == 0)
            throw new InvalidOperationException($"在 {inputPath} 中未找到 .html 文件");

        return htmlFiles.Select(f => HtmlDocumentParser.Parse(f, inputPath, routeMap)).ToList();
    }
}
