using AngleSharp.Html.Parser;

namespace Bukit.Importing.HtmlDemo;

public static class HtmlDemoDryRunScanner
{
    private static readonly HtmlParser Parser = new();

    public static HtmlDemoDryRunScanResult Scan(HtmlDemoDryRunOptions options)
    {
        string projectRoot = NormalizeFullPath(options.ProjectRoot);
        string demoDirectory = NormalizeFullPath(options.DemoDirectory);
        if (string.IsNullOrWhiteSpace(demoDirectory) || !Directory.Exists(demoDirectory))
        {
            return Failure("import.htmlDemoDirNotFound", "HTML demo directory was not found.", RelativePath(projectRoot, demoDirectory));
        }

        if (!IsInsideDirectory(demoDirectory, projectRoot))
        {
            return Failure("import.htmlDemoDirInvalid", "HTML demo directory must stay inside the project root.", RelativePath(projectRoot, demoDirectory));
        }

        RouteMapConfig? routeMap = RouteMapLoader.Load(options.RouteMapPath);
        IReadOnlyList<DiscoveredPage> discoveredPages;
        try
        {
            discoveredPages = Bukit.Importing.HtmlDemoScanner.Scan(demoDirectory, routeMap);
        }
        catch (InvalidOperationException ex)
        {
            return Failure("import.htmlDemoNoHtmlFiles", ex.Message, RelativePath(projectRoot, demoDirectory));
        }

        var diagnostics = new List<HtmlDemoDryRunDiagnostic>();
        var pages = discoveredPages
            .Select(page => new HtmlDemoPageCandidate(
                Source: RelativePath(projectRoot, page.FilePath),
                Slug: page.Slug,
                Type: page.Type.ToString(),
                Title: page.Title))
            .ToArray();

        if (!File.Exists(Path.Combine(demoDirectory, "index.html")))
        {
            diagnostics.Add(new HtmlDemoDryRunDiagnostic(
                "import.htmlDemoMissingIndex",
                "warning",
                "HTML demo does not contain index.html.",
                RelativePath(projectRoot, demoDirectory)));
        }

        var assets = new List<HtmlDemoDryRunAsset>();
        var links = new List<HtmlDemoDryRunLink>();
        foreach (DiscoveredPage page in discoveredPages)
        {
            foreach (string reference in page.AssetPaths)
            {
                if (IsHtmlLink(reference))
                {
                    string targetPath = ResolveReference(demoDirectory, page.FilePath, reference);
                    links.Add(new HtmlDemoDryRunLink(
                        RelativePath(projectRoot, page.FilePath),
                        NormalizeSeparators(reference),
                        File.Exists(targetPath)));
                    continue;
                }

                if (!IsAssetReference(reference))
                {
                    continue;
                }

                string assetPath = ResolveReference(demoDirectory, page.FilePath, reference);
                bool exists = File.Exists(assetPath);
                string relativeAssetPath = RelativePath(projectRoot, assetPath);
                assets.Add(new HtmlDemoDryRunAsset(
                    Source: RelativePath(projectRoot, page.FilePath),
                    Reference: NormalizeSeparators(reference),
                    Path: relativeAssetPath,
                    Exists: exists));

                if (!exists)
                {
                    diagnostics.Add(new HtmlDemoDryRunDiagnostic(
                        "import.htmlDemoAssetMissing",
                        "warning",
                        $"Referenced asset was not found: {reference}",
                        relativeAssetPath));
                }
            }
        }

        return new HtmlDemoDryRunScanResult(
            Success: true,
            ExitCode: 0,
            Pages: pages,
            Assets: assets,
            Links: links,
            Diagnostics: diagnostics,
            Artifacts:
            [
                new HtmlDemoDryRunArtifact(
                    "scan-report",
                    "reports/import/html-demo-dry-run.json",
                    $"HTML demo dry-run scan: pages={pages.Length}, assets={assets.Count}, links={links.Count}.")
            ]);
    }

    private static HtmlDemoDryRunScanResult Failure(string code, string message, string? path = null)
        => new(
            Success: false,
            ExitCode: 2,
            Diagnostics:
            [
                new HtmlDemoDryRunDiagnostic(code, "error", message, path)
            ]);

    private static bool IsHtmlLink(string reference)
        => Path.GetExtension(reference.Split('#', '?')[0]).Equals(".html", StringComparison.OrdinalIgnoreCase);

    private static bool IsAssetReference(string reference)
    {
        string extension = Path.GetExtension(reference.Split('#', '?')[0]);
        return extension.Equals(".css", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".svg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".avif", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveReference(string demoDirectory, string sourceFile, string reference)
    {
        string cleanReference = reference.Split('#', '?')[0].Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(cleanReference))
        {
            return Path.GetFullPath(Path.Combine(demoDirectory, cleanReference.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }

        return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, cleanReference));
    }

    private static string NormalizeFullPath(string path)
        => string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFullPath(path);

    private static bool IsInsideDirectory(string path, string directory)
    {
        string fullPath = NormalizeFullPath(path);
        string fullDirectory = NormalizeFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullDirectory, StringComparison.Ordinal)
            || fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string RelativePath(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        string fullPath = NormalizeFullPath(path);
        string relative = IsInsideDirectory(fullPath, projectRoot)
            ? Path.GetRelativePath(projectRoot, fullPath)
            : fullPath;
        return NormalizeSeparators(relative);
    }

    private static string NormalizeSeparators(string path)
        => path.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
}
