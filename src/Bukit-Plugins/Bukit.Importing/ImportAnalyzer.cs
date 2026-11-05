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
        var pages = DetailPageLinker.Apply(HtmlDemoScanner.Scan(options.InputPath, routeMap), routeMap);
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

        // Fails early (also on dry runs) when two pages claim the same template.
        var templatePlan = PageTemplatePlan.Build(pages, routeMap);
        AddUnmatchedRouteMapWarnings(pages, routeMap, warnings);
        warnings.AddRange(templatePlan.RenamedSlugs);
        var components = options.ExtractContent ? ComponentExtractor.Extract(pages) : [];
        var content = options.ExtractContent ? ContentExtractor.Extract(pages, routeMap, templatePlan) : new ExtractedContent();

        if (options.ExtractContent)
        {
            NavigationImportAdvisor.AddMissingNavigationWarnings(pages, content, warnings);
            ContentExtractor.EnsureUniqueCollectionSlugs(content, warnings);
            AddFallbackTemplateWarnings(content, warnings);
            ImportContentMetadataAuditor.AddDiagnostics(options, content, diagnostics);
        }

        // Asset import happens during commit, not analysis.
        // Return a placeholder — the real AssetImportResult is produced during commit.
        var assetResult = new AssetImporter.AssetImportResult(0, [], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        return new ImportAnalysis(pages, layout, warnings, diagnostics, components, content, routeMap, assetResult, templatePlan);
    }

    // An entry that matches no demo file (a typo, a moved page) would otherwise do nothing silently.
    private static void AddUnmatchedRouteMapWarnings(List<DiscoveredPage> pages, RouteMapConfig? routeMap, List<string> warnings)
    {
        if (routeMap is null)
            return;

        var used = new HashSet<RouteMapPage>(
            pages.Select(page => PageClassifier.FindEntry(routeMap, page.RelativePath)).OfType<RouteMapPage>(),
            ReferenceEqualityComparer.Instance);
        foreach (var entry in routeMap.Pages.Where(entry => !used.Contains(entry)))
            warnings.Add($"Route map entry '{entry.Source}' matches no page in the demo and was ignored.");
    }

    // Content without a demo page of its own renders with a generic fallback
    // template, which carries no page styles or scripts.
    private static void AddFallbackTemplateWarnings(ExtractedContent content, List<string> warnings)
    {
        AddFallbackWarning("post", "pages/post.html", content.Posts.Where(p => p.TemplatePath is null).Select(p => p.Slug), warnings);
        AddFallbackWarning("company", "pages/company.html", content.Companies.Where(c => c.TemplatePath is null).Select(c => c.Slug), warnings);
        AddFallbackWarning("service", "pages/service.html", content.Services.Where(s => s.TemplatePath is null).Select(s => s.Slug), warnings);
        AddFallbackWarning("page", "pages/page.html", content.Pages.Where(p => p.TemplatePath is null).Select(p => p.Slug), warnings);
    }

    private static void AddFallbackWarning(string kind, string template, IEnumerable<string> slugs, List<string> warnings)
    {
        var list = slugs.Select(slug => string.IsNullOrEmpty(slug) ? "(no slug)" : slug).ToList();
        if (list.Count == 0)
            return;

        const int shown = 10;
        var names = string.Join(", ", list.Take(shown)) + (list.Count > shown ? $", … (+{list.Count - shown} more)" : "");
        warnings.Add(
            $"{list.Count} {kind} item(s) have no demo page of their own and will render with the fallback template " +
            $"'{template}', which has no page styles or scripts: {names}. Add a template for them or style the fallback.");
    }
}
