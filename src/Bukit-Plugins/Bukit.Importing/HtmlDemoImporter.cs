using System.Text;

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

    internal static void PreserveOriginalHtml(HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var siteDir = GetSiteDir(options);
        var preserveDir = Path.Combine(siteDir, "original-demo");
        var sourceFiles = Directory.GetFiles(options.InputPath, "*", SearchOption.AllDirectories)
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

    internal static void WriteComponentTemplates(HtmlDemoImportOptions options,
        List<DiscoveredComponent> components)
    {
        if (components.Count == 0) return;

        var compDir = Path.Combine(GetThemeDir(options), "layouts", "components");
        Directory.CreateDirectory(compDir);

        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.NormalizedTemplate)) continue;

            var filePath = Path.Combine(compDir, $"{component.Name}.html");
            if (File.Exists(filePath) && !options.Overwrite) continue;
            File.WriteAllText(filePath, component.NormalizedTemplate);
        }
    }

    internal static void ValidateInput(HtmlDemoImportOptions options)
    {
        if (!Directory.Exists(options.InputPath))
            throw new ImportException(ImportErrorKind.UserInput, $"Input directory does not exist: {options.InputPath}");

        var indexPath = Path.Combine(options.InputPath, "index.html");
        if (!File.Exists(indexPath))
            throw new ImportException(ImportErrorKind.UserInput, $"Missing index.html in input directory: {options.InputPath}");

        ValidateThemeName(options.ThemeName);

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
        foreach (var pattern in ImportSafetyPatterns.DangerousInputPatterns)
        {
            if (pattern.Contains('*', StringComparison.Ordinal))
            {
                var matches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    throw new ImportException(ImportErrorKind.UserInput, $"Input directory contains sensitive files ({pattern}): {Path.GetRelativePath(inputPath, matches[0])}");
            }
            else
            {
                var fullPath = Path.Combine(inputPath, pattern);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                    throw new ImportException(ImportErrorKind.UserInput, $"Input directory contains sensitive item: {pattern}");

                var fileMatches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
                if (fileMatches.Length > 0)
                    throw new ImportException(ImportErrorKind.UserInput, $"Input directory contains sensitive files ({pattern}): {Path.GetRelativePath(inputPath, fileMatches[0])}");

                var dirMatches = Directory.GetDirectories(inputPath, pattern, SearchOption.AllDirectories);
                if (dirMatches.Length > 0)
                    throw new ImportException(ImportErrorKind.UserInput, $"Input directory contains sensitive directory ({pattern}): {Path.GetRelativePath(inputPath, dirMatches[0])}");
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
