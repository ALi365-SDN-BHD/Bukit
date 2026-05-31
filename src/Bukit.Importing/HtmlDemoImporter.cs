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

        if (options.Strict)
            RunStrictValidation(pages, warnings);

        if (options.DryRun)
        {
            var dryResult = new ImportResult
            {
                ThemePath = Path.Combine(options.RootDir, "themes", options.ThemeName),
                PagesFound = pages.Count,
                TemplatesGenerated = pages.Count + 2,
                PartialsGenerated = CountEstimatedPartials(layout),
                ComponentsGenerated = 0,
                RecordsExtracted = pages.Count,
                AssetsCopied = pages.Sum(p => p.AssetPaths.Count),
                Warnings = warnings
            };
            var diagnostics = ImportSafetyScanner.Scan(options, pages);
            ImportReportWriter.Write(options, dryResult, diagnostics);
            return dryResult;
        }

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

            result = result with
            {
                ComponentsGenerated = components.Count,
                RecordsExtracted = content.Pages.Count + content.Posts.Count +
                    content.Companies.Count + content.Faqs.Count + content.Sections.Count
            };

            if (options.GenerateSeed)
            {
                var seedGenerated = SeedGenerator.Generate(options, content, components, pages);
                result = result with { SeedGenerated = seedGenerated };
            }
        }

        var diagnostics2 = ImportSafetyScanner.Scan(options, pages);

        var siteYamlCreated = SiteConfigGenerator.Generate(options);
        result = result with { SiteYamlCreated = siteYamlCreated };

        AssetImporter.TransferAssetsToStatic(options.RootDir, options.ThemeName);

        ImportReportWriter.Write(options, result, diagnostics2);

        return result;
    }

    private static int CountEstimatedPartials(LayoutExtractor.LayoutInfo layout)
    {
        var count = 0;
        if (!string.IsNullOrWhiteSpace(layout.Header)) count++;
        if (!string.IsNullOrWhiteSpace(layout.Nav)) count++;
        if (!string.IsNullOrWhiteSpace(layout.Footer)) count++;
        return count;
    }

    private static void RunStrictValidation(List<DiscoveredPage> pages, List<string> warnings)
    {
        var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.Slug) && page.Type != PageType.Home)
                throw new InvalidOperationException($"Strict 模式: 页面缺少 slug: {page.RelativePath}");

            if (!string.IsNullOrWhiteSpace(page.Slug) && !slugs.Add(page.Slug))
                throw new InvalidOperationException($"Strict 模式: 重复 slug: {page.Slug} ({page.RelativePath})");
        }
    }

    private static void PreserveOriginalHtml(HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var siteDir = options.SitePath ?? Path.Combine(options.RootDir, "sites", options.ThemeName);
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
            throw new InvalidOperationException($"输入目录不存在: {options.InputPath}");

        var indexPath = Path.Combine(options.InputPath, "index.html");
        if (!File.Exists(indexPath))
            throw new InvalidOperationException($"输入目录中缺少 index.html: {options.InputPath}");

        ValidateThemeName(options.ThemeName);

        if (!options.DryRun)
        {
            var themeDir = Path.Combine(options.RootDir, "themes", options.ThemeName);
            if (Directory.Exists(themeDir) && !options.Force)
                throw new InvalidOperationException($"主题已存在: {options.ThemeName}。使用 --force 覆盖。");
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
                    throw new InvalidOperationException($"输入目录包含敏感文件 (扩展名 {ext}): {Path.GetRelativePath(inputPath, matches[0])}");
            }
            else
            {
                var fileMatches = Directory.GetFiles(inputPath, pattern, SearchOption.AllDirectories);
                if (fileMatches.Length > 0)
                    throw new InvalidOperationException($"输入目录包含敏感项: {Path.GetRelativePath(inputPath, fileMatches[0])}");

                var dirMatches = Directory.GetDirectories(inputPath, pattern, SearchOption.AllDirectories);
                if (dirMatches.Length > 0)
                    throw new InvalidOperationException($"输入目录包含敏感项: {Path.GetRelativePath(inputPath, dirMatches[0])}");
            }
        }
    }

    private static void ValidateThemeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("主题名不能为空");

        if (name is "." or "..")
            throw new InvalidOperationException($"无效的主题名: {name}");

        if (Path.IsPathRooted(name))
            throw new InvalidOperationException($"无效的主题名（绝对路径）: {name}");

        if (name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
            throw new InvalidOperationException($"无效的主题名（包含路径分隔符）: {name}");
    }
}
