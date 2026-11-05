namespace Bukit.Importing;

/// <summary>
/// Commits the import plan to disk. Writes directly to the target directories.
/// Note: does not use a staging directory. If an exception occurs mid-commit (e.g. strict
/// residue check), partial output may remain on disk. Callers should be aware of this
/// limitation and handle cleanup if needed.
/// </summary>
internal static class ImportCommitter
{
    internal static ImportResult Commit(ImportAnalysis analysis, HtmlDemoImportOptions options)
    {
        var pages = analysis.Pages;
        var layout = analysis.Layout;
        var warnings = analysis.Warnings;
        var diagnostics = analysis.Diagnostics;
        var components = analysis.Components;
        var content = analysis.Content;
        var routeMap = analysis.RouteMap;

        var themeDir = HtmlDemoImporter.GetThemeDir(options);

        // Delete existing theme if force
        if (Directory.Exists(themeDir) && options.Force)
            Directory.Delete(themeDir, recursive: true);

        // Preserve original HTML before any writes
        if (options.PreserveHtml)
            HtmlDemoImporter.PreserveOriginalHtml(options, pages);

        // Import assets (this writes to theme dir directly — assets are not subject to residue check)
        var assetResult = AssetImporter.Import(options, pages);
        content = RewriteContentAssetPaths(content, assetResult.PathMappings);
        content = PrefixContentBasePath(content, InternalLinkRewriter.BasePath(options.BaseUrl));

        // Generate theme (layouts, pages, partials, theme.yaml)
        var result = ThemeGenerator.Generate(options, pages, layout, warnings, assetResult.PathMappings, routeMap, analysis.TemplatePlan);
        result = result with { AssetsCopied = assetResult.Count };
        result.Warnings.AddRange(assetResult.Warnings);

        // Write component templates and content drafts
        if (options.ExtractContent)
        {
            HtmlDemoImporter.WriteComponentTemplates(options, components, assetResult.PathMappings, pages, routeMap, analysis.TemplatePlan);
            ContentDraftWriter.Write(options, content);
            NavigationImportAdvisor.AddMissingNavigationWarnings(pages, content, result.Warnings);

            result = result with
            {
                ComponentsGenerated = components.Count,
                RecordsExtracted = ImportResultBuilder.CountRecords(content),
                ReportComponents = ImportResultBuilder.BuildReportComponents(components),
                ReportSeedFiles = options.GenerateSeed ? ImportResultBuilder.BuildReportSeedFiles(options, content, pages, components) : []
            };

            if (options.GenerateSeed)
            {
                var seedOptions = options with { Overwrite = options.Overwrite || options.Force };
                var seedGenerated = SeedGenerator.Generate(seedOptions, content, components, pages);
                result = result with { SeedGenerated = seedGenerated };
            }
        }

        // Generate site.yaml
        var siteYamlCreated = SiteConfigGenerator.Generate(options, routeMap, result.PageTypes, result.PostListSlug, analysis.TemplatePlan, pages);

        // Sync templates manifest
        var templatesSynced = HtmlDemoImporter.SyncTemplates(options, options.Force);

        result = result with
        {
            SiteYamlCreated = siteYamlCreated,
            TemplatesSynced = templatesSynced,
            SitePath = HtmlDemoImporter.GetSiteDir(options),
            Diagnostics = diagnostics,
            ReportPages = ImportResultBuilder.BuildReportPages(pages, routeMap, analysis.TemplatePlan)
        };

        // Transfer assets to static
        AssetImporter.TransferAssetsToStatic(options);

        // Run residue analysis and strict check
        var hardcodedReport = TemplateResidueAnalyzer.Analyze(themeDir, null, routeMap);
        result = result with { HardcodedContentReport = hardcodedReport };

        if (options.StrictMode != null && hardcodedReport != null && options.StrictMode != "warn")
            HtmlDemoImporter.ThrowIfStrictResidue(hardcodedReport);

        // Write report
        ImportReportWriter.Write(options, result, diagnostics);

        return result;
    }

    // Record content cannot read base_url, so a sub-path deploy needs it written in.
    private static ExtractedContent PrefixContentBasePath(ExtractedContent content, string basePath)
    {
        if (basePath.Length == 0)
            return content;

        string? Prefix(string? value) => value is null ? null : InternalLinkRewriter.PrefixRootUrls(value, basePath);
        content.Pages = content.Pages.Select(page => page with { Content = Prefix(page.Content) }).ToList();
        content.Posts = content.Posts.Select(post => post with { Content = Prefix(post.Content) }).ToList();
        content.Companies = content.Companies.Select(company => company with { Content = Prefix(company.Content) }).ToList();
        content.Services = content.Services.Select(service => service with { Content = Prefix(service.Content) }).ToList();
        return content;
    }

    private static ExtractedContent RewriteContentAssetPaths(
        ExtractedContent content,
        Dictionary<string, string> mappings)
    {
        if (mappings.Count == 0)
            return content;

        string? Rewrite(string? value)
            => string.IsNullOrWhiteSpace(value) ? value : AssetImporter.RewritePaths(value, mappings);

        content.Pages = content.Pages
            .Select(page => page with { Content = Rewrite(page.Content) })
            .ToList();
        content.Posts = content.Posts
            .Select(post => post with { Content = Rewrite(post.Content) })
            .ToList();
        content.Companies = content.Companies
            .Select(company => company with { Content = Rewrite(company.Content) })
            .ToList();
        content.Services = content.Services
            .Select(service => service with { Content = Rewrite(service.Content) })
            .ToList();

        return content;
    }

}
