using System.Text;

namespace Bukit.Importing;

public static class HtmlDemoImporter
{
    private static readonly string[] DangerousInputPatterns =
    [
        ".env", ".env.*", ".npmrc", ".git", "node_modules", ".vscode", "dist", "build",
        "id_rsa", "id_dsa", "id_ecdsa", "id_ed25519",
        "*.key", "*.pfx", "*.p12", "*.pem", "*.crt", "*.cert"
    ];

    public static ImportResult Import(HtmlDemoImportOptions options)
    {
        ValidateInput(options);

        var routeMap = RouteMapLoader.Load(options.RouteMapPath);

        var pages = HtmlDemoScanner.Scan(options.InputPath, routeMap);
        var warnings = new List<string>();
        var layout = LayoutExtractor.Extract(pages, warnings);

        // 空布局检测：提示用户布局提取失败的常见原因和解决方案
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
        ThrowIfErrorDiagnostics(diagnostics);

        if (options.StrictMode != null)
        {
            RunStrictValidation(pages, warnings);
            if (options.StrictMode != "warn")
                ThrowIfStrictDiagnostics(diagnostics);
        }

        if (options.DryRun)
        {
            var dryComponents = options.ExtractContent ? ComponentExtractor.Extract(pages) : [];
            var dryContent = options.ExtractContent ? ContentExtractor.Extract(pages) : new ExtractedContent();
            NavigationImportAdvisor.AddMissingNavigationWarnings(pages, dryContent, warnings);
            var dryResult = new ImportResult
            {
                ThemePath = GetThemeDir(options),
                SitePath = GetSiteDir(options),
                PagesFound = pages.Count,
                TemplatesGenerated = pages.Count + 2,
                PartialsGenerated = CountEstimatedPartials(layout),
                ComponentsGenerated = dryComponents.Count,
                RecordsExtracted = CountRecords(dryContent),
                AssetsCopied = pages.Sum(p => p.AssetPaths.Count),
                Warnings = warnings,
                Diagnostics = diagnostics,
                ReportPages = BuildReportPages(pages, routeMap),
                ReportComponents = BuildReportComponents(dryComponents),
                ReportSeedFiles = options.GenerateSeed ? BuildReportSeedFiles(options, dryContent, pages, dryComponents) : []
            };
            ImportReportWriter.Write(options, dryResult, diagnostics);
            return dryResult;
        }

        var themeDir = GetThemeDir(options);
        if (Directory.Exists(themeDir) && options.Force)
            Directory.Delete(themeDir, recursive: true);

        if (options.PreserveHtml)
            PreserveOriginalHtml(options, pages);

        var assetResult = AssetImporter.Import(options, pages);

        var result = ThemeGenerator.Generate(options, pages, layout, warnings, assetResult.PathMappings, routeMap);
        result = result with { AssetsCopied = assetResult.Count };
        result.Warnings.AddRange(assetResult.Warnings);

        if (options.ExtractContent)
        {
            var components = ComponentExtractor.Extract(pages);
            var content = ContentExtractor.Extract(pages);
            NavigationImportAdvisor.AddMissingNavigationWarnings(pages, content, result.Warnings);

            if (!options.DryRun)
                WriteComponentTemplates(options, components);

            ContentDraftWriter.Write(options, content);

            result = result with
            {
                ComponentsGenerated = components.Count,
                RecordsExtracted = CountRecords(content),
                ReportComponents = BuildReportComponents(components),
                ReportSeedFiles = options.GenerateSeed ? BuildReportSeedFiles(options, content, pages, components) : []
            };

            if (options.GenerateSeed)
            {
                var seedOptions = options with { Overwrite = options.Overwrite || options.Force };
                var seedGenerated = SeedGenerator.Generate(seedOptions, content, components, pages);
                result = result with { SeedGenerated = seedGenerated };
            }
        }

        var siteYamlCreated = SiteConfigGenerator.Generate(options, routeMap);
        var templatesSynced = SyncTemplates(options, options.Force);
        result = result with
        {
            SiteYamlCreated = siteYamlCreated,
            TemplatesSynced = templatesSynced,
            SitePath = GetSiteDir(options),
            Diagnostics = diagnostics,
            ReportPages = BuildReportPages(pages, routeMap)
        };

        AssetImporter.TransferAssetsToStatic(options);

        var hardcodedReport = TemplateResidueAnalyzer.Analyze(themeDir, null, routeMap);
        result = result with { HardcodedContentReport = hardcodedReport };
        if (options.StrictMode != null && hardcodedReport != null && options.StrictMode != "warn")
            ThrowIfStrictResidue(hardcodedReport);

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

    private static bool SyncTemplates(HtmlDemoImportOptions options, bool force)
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

    private static int CountRecords(ExtractedContent content)
        => content.Pages.Count + content.Navigation.Count + content.Posts.Count + content.Companies.Count +
           content.Services.Count + content.Faqs.Count + content.Sections.Count;

    private static List<ImportReportPage> BuildReportPages(List<DiscoveredPage> pages, RouteMapConfig? routeMap)
        => pages.Select(p => new ImportReportPage(
            p.RelativePath,
            RouteForPage(p, routeMap),
            p.Type.ToString(),
            TemplateForPage(p, routeMap),
            "generated")).ToList();

    private static List<ImportReportComponent> BuildReportComponents(List<DiscoveredComponent> components)
        => components.Select(c => new ImportReportComponent(
            c.Name,
            string.Join(", ", c.UsedBy.Select(p => p.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase)),
            string.IsNullOrWhiteSpace(c.NormalizedTemplate) ? "skipped" : "generated")).ToList();

    private static List<ImportReportSeedFile> BuildReportSeedFiles(
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
            _ => "page"
        };
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

    private static void ThrowIfStrictResidue(HardcodedContentReport hardcodedReport)
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
            $"Strict 模式: 硬编码内容残留: {summary}");
    }

    private static void ThrowIfErrorDiagnostics(List<ImportDiagnostic> diagnostics)
    {
        var errors = diagnostics
            .Where(d => d.Severity == ImportDiagnosticSeverity.Error)
            .ToList();
        if (errors.Count == 0) return;

        var summary = string.Join(", ", errors.Select(d => d.Code).Distinct());
        throw new ImportException(ImportErrorKind.UserInput, $"导入诊断失败: {summary}");
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

    private static void PreserveOriginalHtml(HtmlDemoImportOptions options, List<DiscoveredPage> pages)
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

        Console.WriteLine($"  原始 HTML 已保留: {preserveDir}");
    }

    private static void WriteComponentTemplates(HtmlDemoImportOptions options,
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
            var themeDir = GetThemeDir(options);
            if (Directory.Exists(themeDir) && !options.Force)
                throw new ImportException(ImportErrorKind.UserInput, $"主题已存在: {options.ThemeName}。使用 --force 覆盖。");
        }

        ScanDangerousFiles(options.InputPath);
    }

    private static void ScanDangerousFiles(string inputPath)
    {
        foreach (var pattern in DangerousInputPatterns)
        {
            if (pattern.Contains('*', StringComparison.Ordinal))
            {
                var matches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    throw new ImportException(ImportErrorKind.UserInput, $"输入目录包含敏感文件 ({pattern}): {Path.GetRelativePath(inputPath, matches[0])}");
            }
            else
            {
                var fullPath = Path.Combine(inputPath, pattern);
                if (File.Exists(fullPath) || Directory.Exists(fullPath))
                    throw new ImportException(ImportErrorKind.UserInput, $"输入目录包含敏感项: {pattern}");

                var fileMatches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
                if (fileMatches.Length > 0)
                    throw new ImportException(ImportErrorKind.UserInput, $"输入目录包含敏感文件 ({pattern}): {Path.GetRelativePath(inputPath, fileMatches[0])}");

                var dirMatches = Directory.GetDirectories(inputPath, pattern, SearchOption.AllDirectories);
                if (dirMatches.Length > 0)
                    throw new ImportException(ImportErrorKind.UserInput, $"输入目录包含敏感目录 ({pattern}): {Path.GetRelativePath(inputPath, dirMatches[0])}");
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
