using System.Text;

using Bukit.Shared;

namespace Bukit.Importing;

public static class HtmlDemoImporter
{
    public static ImportResult Import(HtmlDemoImportOptions options)
    {
        var analysis = ImportAnalyzer.Analyze(options);

        if (options.DryRun)
            return ImportResultBuilder.Build(analysis, options);

        return ImportCommitter.Commit(analysis, options);
    }

    internal static bool SyncTemplates(HtmlDemoImportOptions options, bool force)
    {
        var layoutsDir = Path.Combine(GetThemeDir(options), "layouts");
        if (!Directory.Exists(layoutsDir))
            return false;

        var manifestPath = Path.Combine(layoutsDir, "bukit.templates.yaml");
        if (File.Exists(manifestPath) && !force)
            return true;

        var htmlFiles = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("templates:");

        foreach (var file in htmlFiles)
        {
            var relative = Path.GetRelativePath(layoutsDir, file).Replace('\\', '/');
            var text = File.ReadAllText(file);

            var needsPageContent = text.Contains("page.content", StringComparison.Ordinal) ||
                                   text.Contains("item.content", StringComparison.Ordinal);
            var supportsPagination = relative.StartsWith("pages/pagination", StringComparison.OrdinalIgnoreCase);
            var supportsTaxonomy = relative.StartsWith("pages/taxonomy", StringComparison.OrdinalIgnoreCase);
            var supportsSearch = relative.StartsWith("pages/search", StringComparison.OrdinalIgnoreCase);

            sb.AppendLine($"  {relative}:");
            sb.AppendLine("    capabilities:");
            sb.AppendLine($"      needs_page_content: {needsPageContent.ToString().ToLowerInvariant()}");
            sb.AppendLine($"      supports_pagination: {supportsPagination.ToString().ToLowerInvariant()}");
            sb.AppendLine($"      supports_taxonomy: {supportsTaxonomy.ToString().ToLowerInvariant()}");
            sb.AppendLine($"      supports_search_snippets: {supportsSearch.ToString().ToLowerInvariant()}");
        }

        File.WriteAllText(manifestPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return true;
    }

    internal static void RunStrictValidation(List<DiscoveredPage> pages, List<string> warnings)
    {
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Slug) && page.Type != PageType.Home)
                throw new ImportException(ImportErrorKind.UserInput, $"Strict mode: page missing slug: {page.RelativePath}");

            if (!string.IsNullOrWhiteSpace(page.Slug) && !slugs.Add(page.Slug))
                throw new ImportException(ImportErrorKind.UserInput, $"Strict mode: duplicate slug: {page.Slug} ({page.RelativePath})");
        }
    }

    internal static void ThrowIfStrictDiagnostics(List<ImportDiagnostic> diagnostics)
    {
        var strictDiagnostics = diagnostics
            .Where(d => d.Severity >= ImportDiagnosticSeverity.Warning)
            .ToList();
        if (strictDiagnostics.Count == 0) return;

        var summary = string.Join(", ", strictDiagnostics.Select(d => d.Code).Distinct());
        throw new ImportException(ImportErrorKind.UserInput, $"Strict mode: import diagnostics failed: {summary}");
    }

    internal static void ThrowIfStrictResidue(HardcodedContentReport hardcodedReport)
    {
        var residues = hardcodedReport.Residues
            .Where(r => r.ResidualTextCount > 0)
            .ToList();
        if (residues.Count == 0) return;

        var summary = string.Join(", ", residues
            .OrderByDescending(r => r.ResidualTextCount)
            .Take(3)
            .Select(r => $"{Path.GetFileName(r.TemplatePath)}:{r.ResidualTextCount}"));
        throw new ImportException(ImportErrorKind.UserInput,
            $"Strict mode: hardcoded content residue: {summary}");
    }

    internal static void ThrowIfErrorDiagnostics(List<ImportDiagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity == ImportDiagnosticSeverity.Error)
            .ToList();
        if (errors.Count == 0) return;

        var summary = string.Join(", ", errors.Select(d => d.Code).Distinct());
        throw new ImportException(ImportErrorKind.UserInput, $"Import diagnostics failed: {summary}");
    }

    internal static string GetSiteDir(HtmlDemoImportOptions options)
    {
        return options.SitePath ?? Path.Combine(options.RootDir, "sites", options.ThemeName);
    }

    internal static string GetThemeDir(HtmlDemoImportOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SitePath))
        {
            var relative = Path.GetRelativePath(options.RootDir, options.SitePath);
            if (relative.Equals("..", StringComparison.Ordinal) ||
                relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                relative.StartsWith("../", StringComparison.Ordinal))
            {
                return Path.Combine(options.SitePath, "themes", options.ThemeName);
            }
        }

        return Path.Combine(options.RootDir, "themes", options.ThemeName);
    }

    /// <summary>
    /// Output folders of this import that sit strictly inside the demo, such as
    /// <c>sites/x</c> when the demo is the project root. A folder that is the demo
    /// or contains it (<c>--site-path .</c> with the demo under the root) is not
    /// listed, or every demo file would be skipped.
    /// </summary>
    internal static IReadOnlyList<string> GetOutputRootsInsideInput(HtmlDemoImportOptions options)
    {
        var siteDir = GetSiteDir(options);
        return new[] { siteDir, GetThemeDir(options), Path.Combine(siteDir, "original-demo") }
            .Where(root => PathUtils.IsSameOrSubPathOf(root, options.InputPath) &&
                           !PathUtils.IsSameOrSubPathOf(options.InputPath, root))
            .ToList();
    }

    internal static void PreserveOriginalHtml(HtmlDemoImportOptions options)
    {
        var siteDir = GetSiteDir(options);
        var preserveDir = Path.Combine(siteDir, "original-demo");
        // The demo may be the project root; never copy this import's own output.
        var outputRoots = GetOutputRootsInsideInput(options);
        var sourceFiles = DemoFiles.Enumerate(options.InputPath)
            .Where(file => !ImportSafetyPatterns.IsBuiltOutputPath(options.InputPath, file))
            .Where(file => !outputRoots.Any(root => PathUtils.IsSameOrSubPathOf(file, root)))
            .Where(file => !IsImportLockFile(file))
            .ToList();
        Directory.CreateDirectory(preserveDir);

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(options.InputPath, file);
            var relativeDir = Path.GetDirectoryName(relativePath);
            var destDir = string.IsNullOrEmpty(relativeDir)
                ? preserveDir
                : Path.Combine(preserveDir, relativeDir);
            Directory.CreateDirectory(destDir);

            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }

        Console.WriteLine($"  Original HTML preserved: {preserveDir}");
    }

    private static bool IsImportLockFile(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals(".bukit-import.lock", StringComparison.OrdinalIgnoreCase) ||
               (name.StartsWith(".import-", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".lock", StringComparison.OrdinalIgnoreCase));
    }

    internal static void WriteComponentTemplates(HtmlDemoImportOptions options,
        List<DiscoveredComponent> components,
        Dictionary<string, string> pathMappings,
        List<DiscoveredPage> pages,
        RouteMapConfig? routeMap,
        PageTemplatePlan? plan = null)
    {
        if (components.Count == 0) return;
        plan ??= PageTemplatePlan.Build(pages, routeMap);

        var compDir = Path.Combine(GetThemeDir(options), "layouts", "components");
        Directory.CreateDirectory(compDir);

        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.NormalizedTemplate)) continue;

            var filePath = Path.Combine(compDir, $"{component.Name}.html");
            if (File.Exists(filePath) && !options.Overwrite) continue;

            var content = AssetImporter.RewritePaths(component.NormalizedTemplate, pathMappings);
            content = PageAssetCollector.RewriteCssUrls(content, pathMappings);
            content = PageAssetCollector.RewriteSrcsets(content, pathMappings, "{{ base_url }}");
            // Components render inside page templates, which set base_url.
            var source = component.UsedBy.FirstOrDefault()?.RelativePath ?? "index.html";
            content = InternalLinkRewriter.RewriteTemplate(content, source, plan);
            File.WriteAllText(filePath, content);
        }
    }

    internal static void ValidateInput(HtmlDemoImportOptions options)
    {
        if (!Directory.Exists(options.InputPath))
            throw new ImportException(ImportErrorKind.UserInput, $"Input directory does not exist: {options.InputPath}");

        var hasIndex = File.Exists(Path.Combine(options.InputPath, "index.html")) ||
                       File.Exists(Path.Combine(options.InputPath, "index.htm"));
        if (!hasIndex)
            throw new ImportException(ImportErrorKind.UserInput, $"Missing index.html or index.htm in input directory: {options.InputPath}");

        ValidateThemeName(options.ThemeName);
        SiteConfigGenerator.ValidateLanguageOption(options.Language);

        if (!options.DryRun)
        {
            var themeDir = GetThemeDir(options);
            if (Directory.Exists(themeDir) && !options.Force)
                throw new ImportException(ImportErrorKind.UserInput, $"Theme already exists: {options.ThemeName}. Use --force to overwrite.");
        }

        ScanDangerousFiles(options.InputPath);
    }

    private static void ScanDangerousFiles(string inputPath)
    {
        var tree = DemoFiles.Walk(inputPath, ImportSafetyPatterns.SkipDirectoryNames);

        foreach (var file in tree.Files)
        {
            if (ImportSafetyPatterns.IsSensitiveFileName(Path.GetFileName(file)))
            {
                throw new ImportException(ImportErrorKind.UserInput,
                    $"Input directory contains sensitive file: {Path.GetRelativePath(inputPath, file)}");
            }
        }

        foreach (var directory in tree.Directories)
        {
            if (ImportSafetyPatterns.IsSensitiveDirectoryName(Path.GetFileName(directory)))
            {
                throw new ImportException(ImportErrorKind.UserInput,
                    $"Input directory contains sensitive directory: {Path.GetRelativePath(inputPath, directory)}");
            }
        }
    }

    private static void ValidateThemeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ImportException(ImportErrorKind.UserInput, "Theme name cannot be empty.");

        if (name is "." or "..")
            throw new ImportException(ImportErrorKind.UserInput, $"Invalid theme name: {name}");

        if (Path.IsPathRooted(name))
            throw new ImportException(ImportErrorKind.UserInput, $"Invalid theme name (absolute path): {name}");

        if (name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw new ImportException(ImportErrorKind.UserInput, $"Invalid theme name (contains path separators): {name}");
    }
}
