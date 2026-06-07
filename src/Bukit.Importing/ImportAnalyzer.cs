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
            Console.WriteLine("  ! 未提取到共享布局（header/footer）。可能原因：");
            Console.WriteLine("    - HTML 文件格式不一致（压缩 vs 分行）");
            Console.WriteLine("    - 页面结构差异过大");
            Console.WriteLine("  建议：");
            Console.WriteLine("    - 使用 --route-map route-map.yaml 精确指定页面结构");
            Console.WriteLine("    - 导入后手动创建 themes/<name>/layouts/partials/header.html 和 footer.html");
            Console.WriteLine("    - 原始文件已保留在 sites/<name>/original-demo/ 中供参考");
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
            NavigationImportAdvisor.AddMissingNavigationWarnings(pages, content, warnings);

        // Asset import happens during commit, not analysis.
        // Return a placeholder — the real AssetImportResult is produced during commit.
        var assetResult = new AssetImporter.AssetImportResult(0, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return new ImportAnalysis(pages, layout, warnings, diagnostics, components, content, routeMap, assetResult);
    }
}
