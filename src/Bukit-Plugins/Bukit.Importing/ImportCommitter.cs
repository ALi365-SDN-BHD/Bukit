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
        var siteDir = HtmlDemoImporter.GetSiteDir(options);
        Directory.CreateDirectory(siteDir);
        using var importLock = AcquireImportLock(siteDir);

        var manifest = ImportManifest.Load(siteDir, options.Overwrite);
        var saved = false;
        try
        {
            return CommitCore(analysis, options, manifest, () => saved = true);
        }
        catch when (!saved)
        {
            // Record what was written before the failure, so the next import still
            // recognises those files as its own; remove nothing.
            try
            {
                manifest.Save(cleanUp: false);
            }
            catch (Exception saveError) when (saveError is IOException or UnauthorizedAccessException)
            {
                // The import's own error matters more than a manifest that could not be written.
            }

            throw;
        }
    }

    private static FileStream AcquireImportLock(string siteDir)
    {
        var path = Path.Combine(siteDir, ".bukit-import.lock");
        try
        {
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImportException(ImportErrorKind.UserInput,
                $"Another import is already running for {siteDir}. " +
                $"Wait for it to finish; if the previous run was killed, remove '{path}' and retry. ({ex.Message})");
        }
    }

    private static ImportResult CommitCore(
        ImportAnalysis analysis, HtmlDemoImportOptions options, ImportManifest manifest, Action markSaved)
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
            ContentDraftWriter.Write(options, content, manifest);
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
                var seedGenerated = SeedGenerator.Generate(seedOptions, content, components, pages, manifest);
                result = result with { SeedGenerated = seedGenerated };
            }
        }

        // Generate site.yaml
        var siteYamlCreated = SiteConfigGenerator.Generate(options, routeMap, result.PageTypes, result.PostListSlug, analysis.TemplatePlan, pages, manifest);
        // Only a site.yaml this import wrote can carry the placeholder; a kept one is the user's.
        if (siteYamlCreated && SiteIdentity.Url(pages, options.BaseUrl) is null)
        {
            result.Warnings.Add(
                $"The demo declares no canonical or og:url address, so site.yaml uses '{SiteIdentity.PlaceholderUrl}'. " +
                "Set site.url to the real address (or pass an absolute --base-url), or canonical links and the sitemap point at the placeholder.");
        }

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

        // Run residue analysis and strict check before the manifest deletes stale files,
        // so a failing --strict run leaves the previous import's files in place.
        var hardcodedReport = TemplateResidueAnalyzer.Analyze(themeDir, null, routeMap);
        result = result with { HardcodedContentReport = hardcodedReport };

        if (options.StrictMode != null && hardcodedReport != null && options.StrictMode != "warn")
            HtmlDemoImporter.ThrowIfStrictResidue(hardcodedReport);

        // Update or remove what the last import wrote, keeping files people edited.
        manifest.Save();
        markSaved();
        result.Warnings.AddRange(manifest.Warnings);
        AddMissingTemplateWarnings(HtmlDemoImporter.GetSiteDir(options), themeDir, result.Warnings);

        // Write report
        ImportReportWriter.Write(options, result, diagnostics);

        return result;
    }

    // A draft kept from an earlier import can name a template this import no longer
    // writes; Core would fail the build on it, so say which file to fix.
    private static void AddMissingTemplateWarnings(string siteDir, string themeDir, List<string> warnings)
    {
        var contentDir = Path.Combine(siteDir, "content");
        if (!Directory.Exists(contentDir))
            return;

        foreach (var draft in Directory.EnumerateFiles(contentDir, "*.md", SearchOption.AllDirectories))
        {
            var template = ReadFrontMatterValue(draft, "template");
            if (string.IsNullOrWhiteSpace(template) || File.Exists(Path.Combine(themeDir, "layouts", template)))
                continue;

            warnings.Add(
                $"Draft '{Path.GetRelativePath(siteDir, draft).Replace('\\', '/')}' uses template '{template}', " +
                "which this import did not generate. Change its template or delete the draft, or the build fails.");
        }
    }

    private static string? ReadFrontMatterValue(string path, string key)
    {
        using var reader = new StreamReader(path);
        if (reader.ReadLine()?.Trim() != "---")
            return null;

        for (var line = reader.ReadLine(); line is not null && line.Trim() != "---"; line = reader.ReadLine())
        {
            if (line.StartsWith(key + ":", StringComparison.Ordinal))
                return line[(key.Length + 1)..].Trim().Trim('"', '\'');
        }

        return null;
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
