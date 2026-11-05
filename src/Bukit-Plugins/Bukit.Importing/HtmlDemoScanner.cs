namespace Bukit.Importing;

internal static class HtmlDemoScanner
{
    internal static List<DiscoveredPage> Scan(string inputPath, RouteMapConfig? routeMap = null)
    {
        var pageFiles = DemoFiles.Enumerate(inputPath)
            .Where(IsPageFile)
            .ToList();
        var htmlFiles = pageFiles
            .Where(file => !ImportSafetyPatterns.IsBuiltOutputPath(inputPath, file))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var skippedBuilt = pageFiles.Count - htmlFiles.Count;
        if (skippedBuilt > 0)
            Console.WriteLine($"  Skipped {skippedBuilt} built page(s) under {string.Join("/, ", ImportSafetyPatterns.BuiltOutputDirectories)}/.");

        if (htmlFiles.Count == 0)
            throw new InvalidOperationException($"在 {inputPath} 中未找到 .html/.htm 文件");

        return htmlFiles.Select(f => HtmlDocumentParser.Parse(f, inputPath, routeMap)).ToList();
    }

    internal static bool IsPageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);
    }
}
