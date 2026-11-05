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

        // Both files would claim the home route; index.html wins.
        if (htmlFiles.Any(file => IsRootIndex(file, inputPath, ".html")))
        {
            var htmIndex = htmlFiles.Where(file => IsRootIndex(file, inputPath, ".htm")).ToList();
            if (htmIndex.Count > 0)
            {
                htmlFiles = htmlFiles.Except(htmIndex).ToList();
                Console.WriteLine("  Both index.html and index.htm exist; using index.html.");
            }
        }

        if (htmlFiles.Count == 0)
            throw new InvalidOperationException($"在 {inputPath} 中未找到 .html/.htm 文件");

        return htmlFiles.Select(f => HtmlDocumentParser.Parse(f, inputPath, routeMap)).ToList();
    }

    private static bool IsRootIndex(string file, string root, string extension)
        => string.Equals(Path.GetFileName(file), "index" + extension, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(
               Path.GetDirectoryName(Path.GetRelativePath(root, file)),
               "",
               StringComparison.Ordinal);

    internal static bool IsPageFile(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".htm", StringComparison.OrdinalIgnoreCase);
    }
}
