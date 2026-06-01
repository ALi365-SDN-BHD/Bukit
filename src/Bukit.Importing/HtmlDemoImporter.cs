using System.Text;

namespace Bukit.Importing;

public static class HtmlDemoImporter
{
    private static readonly string[] DangerousInputPatterns =
    [
        ".env", ".npmrc", ".git", "node_modules", ".vscode", "dist", "build",
        "*.key", "*.pfx", "*.p12", "*.pem", "*.crt", "*.cert"
    ];

    public static ImportResult Import(HtmlDemoImportOptions options)
    {
        ValidateInput(options);

        var pages = HtmlDemoScanner.Scan(options.InputPath);
        var warnings = new List<string>();
        var layout = LayoutExtractor.Extract(pages, warnings);
        var diagnostics = ImportSafetyScanner.Scan(options, pages);

        if (options.Strict)
        {
            RunStrictValidation(pages, warnings);
            ThrowIfStrictDiagnostics(diagnostics);
        }

        if (options.DryRun)
        {
            var dryResult = new ImportResult
            {
                ThemePath = Path.Combine(options.RootDir, "themes", options.ThemeName),
                SitePath = GetSiteDir(options),
                PagesFound = pages.Count,
                TemplatesGenerated = pages.Count + 2,
                PartialsGenerated = CountEstimatedPartials(layout),
                ComponentsGenerated = 0,
                RecordsExtracted = pages.Count,
                AssetsCopied = pages.Sum(p => p.AssetPaths.Count),
                Warnings = warnings,
                Diagnostics = diagnostics
            };
            ImportReportWriter.Write(options, dryResult, diagnostics);
            return dryResult;
        }

        var themeDir = Path.Combine(options.RootDir, "themes", options.ThemeName);
        if (Directory.Exists(themeDir) && options.Force)
            Directory.Delete(themeDir, recursive: true);

        if (options.PreserveHtml)
            PreserveOriginalHtml(options, pages);

        var assetResult = AssetImporter.Import(options, pages);

        var result = ThemeGenerator.Generate(options, pages, layout, warnings, assetResult.PathMappings);
        result = result with { AssetsCopied = assetResult.Count };
        result.Warnings.AddRange(assetResult.Warnings);

        if (options.ExtractContent)
        {
            var components = ComponentExtractor.Extract(pages);
            var content = ContentExtractor.Extract(pages);

            if (!options.DryRun)
                WriteComponentTemplates(options, components);

            ContentDraftWriter.Write(options, content);

            result = result with
            {
                ComponentsGenerated = components.Count,
                RecordsExtracted = content.Pages.Count + content.Posts.Count +
                    content.Companies.Count + content.Services.Count + content.Faqs.Count + content.Sections.Count
            };

            if (options.GenerateSeed)
            {
                var seedGenerated = SeedGenerator.Generate(options, content, components, pages);
                result = result with { SeedGenerated = seedGenerated };
            }
        }

        var siteYamlCreated = SiteConfigGenerator.Generate(options);
        var templatesSynced = SyncTemplates(options.RootDir, options.ThemeName, options.Force);
        result = result with
        {
            SiteYamlCreated = siteYamlCreated,
            TemplatesSynced = templatesSynced,
            SitePath = GetSiteDir(options),
            Diagnostics = diagnostics
        };

        AssetImporter.TransferAssetsToStatic(options.RootDir, options.ThemeName);

        ImportReportWriter.Write(options, result, diagnostics);

        return result;
    }

    private static int CountEstimatedPartials(LayoutExtractor.LayoutInfo layout)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(layout.Header)) count++;
        if (!string.IsNullOrWhiteSpace(layout.Nav) && !layout.HeaderContainsNav) count++;
        if (!string.IsNullOrWhiteSpace(layout.Footer)) count++;
        return count;
    }

    private static bool SyncTemplates(string rootDir, string themeName, bool force)
    {
        var layoutsDir = Path.Combine(rootDir, "themes", themeName, "layouts");
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

    private static void RunStrictValidation(List<DiscoveredPage> pages, List<string> warnings)
    {
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Slug) && page.Type != PageType.Home)
                throw new ImportException(ImportErrorKind.UserInput, $"Strict 模式: 页面缺少 slug: {page.RelativePath}");

            if (!string.IsNullOrWhiteSpace(page.Slug) && !slugs.Add(page.Slug))
                throw new ImportException(ImportErrorKind.UserInput, $"Strict 模式: 重复 slug: {page.Slug} ({page.RelativePath})");
        }
    }

    private static void ThrowIfStrictDiagnostics(List<ImportDiagnostic> diagnostics)
    {
        var strictDiagnostics = diagnostics
            .Where(d => d.Severity >= ImportDiagnosticSeverity.Warning)
            .ToList();
        if (strictDiagnostics.Count == 0) return;

        var summary = string.Join(", ", strictDiagnostics.Select(d => d.Code).Distinct());
        throw new ImportException(ImportErrorKind.UserInput, $"Strict 模式: 导入诊断失败: {summary}");
    }

    internal static string GetSiteDir(HtmlDemoImportOptions options)
    {
        return options.SitePath ?? Path.Combine(options.RootDir, "sites", options.ThemeName);
    }

    private static void PreserveOriginalHtml(HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var siteDir = GetSiteDir(options);
        var preserveDir = Path.Combine(siteDir, "original-demo");
        Directory.CreateDirectory(preserveDir);

        foreach (var page in pages)
        {
            var relativeDir = Path.GetDirectoryName(page.RelativePath);
            var destDir = string.IsNullOrEmpty(relativeDir)
                ? preserveDir
                : Path.Combine(preserveDir, relativeDir);
            Directory.CreateDirectory(destDir);

            var destFile = Path.Combine(destDir, Path.GetFileName(page.FilePath));
            File.Copy(page.FilePath, destFile, overwrite: true);
        }

        Console.WriteLine($"  原始 HTML 已保留: {preserveDir}");
    }

    private static void WriteComponentTemplates(HtmlDemoImportOptions options,
        List<DiscoveredComponent> components)
    {
        if (components.Count == 0) return;

        var compDir = Path.Combine(options.RootDir, "themes", options.ThemeName,
            "layouts", "components");
        Directory.CreateDirectory(compDir);

        foreach (var component in components)
        {
            if (string.IsNullOrWhiteSpace(component.NormalizedTemplate)) continue;

            var filePath = Path.Combine(compDir, $"{component.Name}.html");
            if (File.Exists(filePath) && !options.Overwrite) continue;
            File.WriteAllText(filePath, component.NormalizedTemplate);
        }
    }

    private static void ValidateInput(HtmlDemoImportOptions options)
    {
        if (!Directory.Exists(options.InputPath))
            throw new ImportException(ImportErrorKind.UserInput, $"输入目录不存在: {options.InputPath}");

        var indexPath = Path.Combine(options.InputPath, "index.html");
        if (!File.Exists(indexPath))
            throw new ImportException(ImportErrorKind.UserInput, $"输入目录中缺少 index.html: {options.InputPath}");

        ValidateThemeName(options.ThemeName);

        if (!options.DryRun)
        {
            var themeDir = Path.Combine(options.RootDir, "themes", options.ThemeName);
            if (Directory.Exists(themeDir) && !options.Force)
                throw new ImportException(ImportErrorKind.UserInput, $"主题已存在: {options.ThemeName}。使用 --force 覆盖。");
        }

        ScanDangerousFiles(options.InputPath);
    }

    private static void ScanDangerousFiles(string inputPath)
    {
        foreach (var pattern in DangerousInputPatterns)
        {
            if (pattern.StartsWith("*."))
            {
                var ext = pattern[1..];
                var matches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    throw new ImportException(ImportErrorKind.UserInput, $"输入目录包含敏感文件 (扩展名 {ext}): {Path.GetRelativePath(inputPath, matches[0])}");
            }
            else
            {
                var fullPath = Path.Combine(inputPath, pattern);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                    throw new ImportException(ImportErrorKind.UserInput, $"输入目录包含敏感项: {pattern}");
            }
        }
    }

    private static void ValidateThemeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ImportException(ImportErrorKind.UserInput, "主题名不能为空");

        if (name is "." or "..")
            throw new ImportException(ImportErrorKind.UserInput, $"无效的主题名: {name}");

        if (Path.IsPathRooted(name))
            throw new ImportException(ImportErrorKind.UserInput, $"无效的主题名（绝对路径）: {name}");

        if (name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw new ImportException(ImportErrorKind.UserInput, $"无效的主题名（包含路径分隔符）: {name}");
    }
}
