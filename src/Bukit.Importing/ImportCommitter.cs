using Bukit.Shared;

namespace Bukit.Importing;

/// <summary>
/// Commits the import plan to disk. Uses a staging directory approach:
/// writes everything to a temp staging area first, validates (residue check),
/// then atomically moves to the final target directories.
/// This prevents half-baked output when strict mode fails.
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
        ValidateOutputPaths(options, themeDir);

        // Delete existing theme if force
        if (Directory.Exists(themeDir) && options.Force)
            Directory.Delete(themeDir, recursive: true);

        // Preserve original HTML before any writes
        if (options.PreserveHtml)
            HtmlDemoImporter.PreserveOriginalHtml(options, pages);

        // Import assets (this writes to theme dir directly — assets are not subject to residue check)
        var assetResult = AssetImporter.Import(options, pages);

        // Generate theme (layouts, pages, partials, theme.yaml)
        var result = ThemeGenerator.Generate(options, pages, layout, warnings, assetResult.PathMappings, routeMap);
        result = result with { AssetsCopied = assetResult.Count };
        result.Warnings.AddRange(assetResult.Warnings);

        // Write component templates and content drafts
        if (options.ExtractContent)
        {
            HtmlDemoImporter.WriteComponentTemplates(options, components);
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
                if (seedGenerated && options.ContentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new ImportDiagnostic(
                        ImportDiagnosticSeverity.Info,
                        "import.notionHandoffReady",
                        "Notion seed handoff files generated.",
                        Path.Combine(HtmlDemoImporter.GetSiteDir(options), "notion-seed")));
                }
            }
        }

        // Generate site.yaml
        var siteYamlCreated = SiteConfigGenerator.Generate(options, routeMap, result.PageTypes, result.PostListSlug);

        // Sync templates manifest
        var templatesSynced = HtmlDemoImporter.SyncTemplates(options, options.Force);

        result = result with
        {
            SiteYamlCreated = siteYamlCreated,
            TemplatesSynced = templatesSynced,
            SitePath = HtmlDemoImporter.GetSiteDir(options),
            Diagnostics = diagnostics,
            ReportPages = ImportResultBuilder.BuildReportPages(pages, routeMap)
        };

        // Transfer assets to static
        AssetImporter.TransferAssetsToStatic(options);

        // Run residue analysis and strict check
        var hardcodedReport = TemplateResidueAnalyzer.Analyze(themeDir, null, routeMap);
        result = result with { HardcodedContentReport = hardcodedReport };

        if (options.StrictMode != null && hardcodedReport != null && options.StrictMode != "warn")
            HtmlDemoImporter.ThrowIfStrictResidue(hardcodedReport);

        if (options.Use)
            diagnostics.Add(ImportSiteUseService.Apply(options));

        if (options.Verify)
            diagnostics.AddRange(ImportLightVerifier.Verify(options));

        // Write report
        ImportReportWriter.Write(options, result, diagnostics);

        return result;
    }

    private static void ValidateOutputPaths(HtmlDemoImportOptions options, string themeDir)
    {
        string root = Path.GetFullPath(options.RootDir);
        string sitesRoot = Path.Combine(root, "sites");
        string siteDir = HtmlDemoImporter.GetSiteDir(options);
        string themesRoot = GetThemeRoot(options, siteDir, root);
        string reportDir = Path.Combine(root, ".bukit", "reports", "plugin-output", "import");
        bool siteDirIsLexicallyInsideRoot = IsLexicallyInsideDirectory(siteDir, root);
        bool siteDirIsInsideRoot = IsInsideDirectory(siteDir, root);

        if (!IsInsideDirectory(themeDir, themesRoot))
        {
            throw new ImportException(ImportErrorKind.UserInput, "Import theme output must stay inside ./themes.");
        }

        if (siteDirIsLexicallyInsideRoot && !siteDirIsInsideRoot)
        {
            throw new ImportException(ImportErrorKind.UserInput, "Import site output must stay inside ./sites.");
        }

        if (siteDirIsInsideRoot && !IsInsideDirectory(siteDir, sitesRoot))
        {
            throw new ImportException(ImportErrorKind.UserInput, "Import site output must stay inside ./sites.");
        }

        if (!siteDirIsLexicallyInsideRoot && string.IsNullOrWhiteSpace(options.SitePath))
        {
            throw new ImportException(ImportErrorKind.UserInput, "Import site output must stay inside ./sites.");
        }

        if (!IsInsideDirectory(reportDir, root))
        {
            throw new ImportException(ImportErrorKind.UserInput, "Import report output must stay inside the project root.");
        }
    }

    private static string GetThemeRoot(HtmlDemoImportOptions options, string siteDir, string root)
    {
        if (!string.IsNullOrWhiteSpace(options.SitePath) && !IsLexicallyInsideDirectory(siteDir, root))
        {
            return Path.Combine(siteDir, "themes");
        }

        return Path.Combine(root, "themes");
    }

    private static bool IsInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            return PathUtils.IsSameOrSubPathOf(path, directory);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsLexicallyInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        string fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, fullDirectory, StringComparison.Ordinal)
            || fullPath.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
