namespace Bukit.Importing;

/// <summary>
/// Pure analysis — scans, extracts, and validates without writing any files.
/// Asset importing is deferred to the commit phase.
/// </summary>
internal static class ImportAnalyzer
{
    internal static ImportAnalysis Analyze(HtmlDemoImportOptions options)
    {
        HtmlDemoImporter.ValidateInput(options);

        var routeMap = RouteMapLoader.Load(options.RouteMapPath);
        var pages = HtmlDemoScanner.Scan(options.InputPath, routeMap);
        var warnings = new List<string>();
        var layout = LayoutExtractor.Extract(pages, warnings);

        if (string.IsNullOrWhiteSpace(layout.Header) && string.IsNullOrWhiteSpace(layout.Footer))
        {
            Console.WriteLine();
            Console.WriteLine("  ! No shared layout (header/footer) extracted. Possible causes:");
            Console.WriteLine("    - HTML file format inconsistency (minified vs line-separated)");
            Console.WriteLine("    - Page structure differs too greatly");
            Console.WriteLine("  Suggestions:");
            Console.WriteLine("    - Use --route-map route-map.yaml to precisely specify page structure");
            Console.WriteLine("    - After import, manually create themes/<name>/layouts/partials/header.html and footer.html");
            Console.WriteLine("    - Original files are preserved in sites/<name>/original-demo/ for reference");
            Console.WriteLine();
        }

        var diagnostics = ImportSafetyScanner.Scan(options, pages);
        HtmlDemoImporter.ThrowIfErrorDiagnostics(diagnostics);

        if (options.StrictMode != null)
        {
            HtmlDemoImporter.RunStrictValidation(pages, warnings);
            if (options.StrictMode != "warn")
                HtmlDemoImporter.ThrowIfStrictDiagnostics(diagnostics);
        }

        var components = options.ExtractContent ? ComponentExtractor.Extract(pages) : [];
        var content = options.ExtractContent ? ContentExtractor.Extract(pages) : new ExtractedContent();

        if (options.ExtractContent)
        {
            NavigationImportAdvisor.AddMissingNavigationWarnings(pages, content, warnings);
            ImportContentMetadataAuditor.AddDiagnostics(options, content, diagnostics);
        }

        // Asset import happens during commit, not analysis.
        // Return a placeholder — the real AssetImportResult is produced during commit.
        var assetResult = new AssetImporter.AssetImportResult(0, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return new ImportAnalysis(pages, layout, warnings, diagnostics, components, content, routeMap, assetResult);
    }
}
