namespace Bukit.Importing;

/// <summary>
/// Builds ImportResult from analysis data. Used by both dry-run preview and real execution,
/// ensuring consistent statistics and report data regardless of execution path.
/// </summary>
internal static class ImportResultBuilder
{
    internal static ImportResult Build(ImportAnalysis analysis, HtmlDemoImportOptions options)
    {
        var routeMap = analysis.RouteMap;
        var pages = analysis.Pages;
        var components = analysis.Components;
        var content = analysis.Content;
        var layout = analysis.Layout;
        var warnings = analysis.Warnings;
        var diagnostics = analysis.Diagnostics;

        var themeDir = HtmlDemoImporter.GetThemeDir(options);
        var siteDir = HtmlDemoImporter.GetSiteDir(options);

        var pageTypes = new HashSet<PageType>();
        foreach (var page in pages)
            pageTypes.Add(page.Type);

        return new ImportResult
        {
            ThemePath = themeDir,
            SitePath = siteDir,
            PagesFound = pages.Count,
            TemplatesGenerated = pages.Count + 2, // pages + index + list
            PartialsGenerated = CountEstimatedPartials(layout),
            ComponentsGenerated = components.Count,
            RecordsExtracted = CountRecords(content),
            AssetsCopied = pages.Sum(p => p.AssetPaths.Count),
            Warnings = warnings,
            Diagnostics = diagnostics,
            ReportPages = BuildReportPages(pages, routeMap),
            ReportComponents = BuildReportComponents(components),
            ReportSeedFiles = options.GenerateSeed ? BuildReportSeedFiles(options, content, pages, components) : [],
            PageTypes = pageTypes,
            PostListSlug = pages.FirstOrDefault(p => p.Type == PageType.PostList)?.Slug
        };
    }

    private static int CountEstimatedPartials(LayoutExtractor.LayoutInfo layout)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(layout.Header)) count++;
        if (!string.IsNullOrWhiteSpace(layout.Nav) && !layout.HeaderContainsNav) count++;
        if (!string.IsNullOrWhiteSpace(layout.Footer)) count++;
        return count;
    }

    internal static int CountRecords(ExtractedContent content)
        => content.Pages.Count + content.Navigation.Count + content.Posts.Count + content.Companies.Count +
           content.Services.Count + content.Faqs.Count + content.Sections.Count;

    internal static List<ImportReportPage> BuildReportPages(List<DiscoveredPage> pages, RouteMapConfig? routeMap)
        => pages.Select(p => new ImportReportPage(
            p.RelativePath,
            RouteForPage(p, routeMap),
            p.Type.ToString(),
            TemplateForPage(p, routeMap),
            "generated")).ToList();

    internal static List<ImportReportComponent> BuildReportComponents(List<DiscoveredComponent> components)
        => components.Select(c => new ImportReportComponent(
            c.Name,
            string.Join(", ", c.UsedBy.Select(p => p.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase)),
            string.IsNullOrWhiteSpace(c.NormalizedTemplate) ? "skipped" : "generated")).ToList();

    internal static List<ImportReportSeedFile> BuildReportSeedFiles(
        HtmlDemoImportOptions options,
        ExtractedContent content,
        List<DiscoveredPage> pages,
        List<DiscoveredComponent> components)
    {
        var ext = options.ContentSource.Equals("yaml", StringComparison.OrdinalIgnoreCase) ? "yaml" : "json";
        return
        [
            new($"pages.{ext}", content.Pages.Count),
            new($"navigation.{ext}", content.Navigation.Count),
            new($"sections.{ext}", content.Sections.Count),
            new($"posts.{ext}", content.Posts.Count),
            new($"companies.{ext}", content.Companies.Count),
            new($"services.{ext}", content.Services.Count),
            new($"faqs.{ext}", content.Faqs.Count),
            new($"media.{ext}", pages.Sum(p => p.AssetPaths.Count)),
            new($"components.{ext}", components.Count)
        ];
    }

    private static string RouteForPage(DiscoveredPage page, RouteMapConfig? routeMap = null)
    {
        var fileName = Path.GetFileNameWithoutExtension(page.RelativePath);
        var routeMapRoute = PageClassifier.GetRoute(routeMap, fileName);
        if (routeMapRoute != null)
            return routeMapRoute;

        return page.Type switch
        {
            PageType.Home => "/",
            PageType.PostDetail => $"/insights/{page.Slug}/",
            PageType.CompanyDetail => $"/companies/{page.Slug}/",
            PageType.ServiceDetail => $"/services/{page.Slug}/",
            _ => string.IsNullOrWhiteSpace(page.Slug) ? "/" : $"/{page.Slug}/"
        };
    }

    private static string TemplateForPage(DiscoveredPage page, RouteMapConfig? routeMap = null)
    {
        var fileName = Path.GetFileNameWithoutExtension(page.RelativePath);
        var routeMapTemplate = PageClassifier.GetTemplate(routeMap, fileName);
        if (routeMapTemplate != null)
            return routeMapTemplate;

        return page.Type switch
        {
            PageType.Home => "index",
            PageType.PostList => "insights",
            PageType.PostDetail => "article",
            PageType.CompanyList => "companies",
            PageType.CompanyDetail => "company",
            PageType.ServiceList => "services",
            PageType.ServiceDetail => "service",
            _ => "generic"
        };
    }
}
