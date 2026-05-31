using System.Text;
using Bukit.Cli.Cli.Binding;
using Bukit.Importing;

namespace Bukit.Cli.Commands;

public static class ImportCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0) ?? "";
        return sub switch
        {
            "html-demo" => await HtmlDemoAsync(command),
            _ => Unknown(sub)
        };
    }

    private static async Task<int> HtmlDemoAsync(CliBoundCommand command)
    {
        var demoDirArg = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(demoDirArg))
        {
            Console.Error.WriteLine("缺少必填参数: <demo-dir>");
            return 2;
        }
        var demoDir = Path.GetFullPath(demoDirArg);
        if (!Directory.Exists(demoDir))
        {
            Console.Error.WriteLine($"demo 目录不存在: {demoDir}");
            return 2;
        }

        var themeName = command.GetString("--theme");
        if (string.IsNullOrWhiteSpace(themeName))
        {
            Console.Error.WriteLine("缺少必填选项: --theme <名称>");
            return 2;
        }
        if (!CloneModels.IsSafeThemeName(themeName))
        {
            Console.Error.WriteLine($"无效的主题名: {themeName}");
            return 2;
        }

        var force = command.GetBool("--force");
        var use = command.GetBool("--use");
        var verify = command.GetBool("--verify");
        var extractContent = !command.GetBool("--no-extract-content");
        var generateSeed = !command.GetBool("--no-seed");
        var contentSource = command.GetString("--content-source") ?? "notion";
        var sitePath = command.GetString("--site-path");
        var language = command.GetString("--language") ?? "en";
        var dryRun = command.GetBool("--dry-run");
        var strict = command.GetBool("--strict");
        var overwrite = command.GetBool("--overwrite");
        var preserveHtml = command.GetBool("--preserve-html");
        var generateReport = command.GetBool("--report");
        var baseUrl = command.GetString("--base-url");

        var resolved = ConfigPathResolver.Resolve(
            command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;

        var themeDir = Path.Combine(rootDir, "themes", themeName);
        if (!dryRun && Directory.Exists(themeDir) && !force)
        {
            Console.Error.WriteLine($"主题已存在: {themeName}。使用 --force 覆盖。");
            return 2;
        }

        var options = new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = themeName,
            RootDir = rootDir,
            Force = force,
            Use = use,
            Verify = verify,
            ExtractContent = extractContent,
            GenerateSeed = generateSeed,
            ContentSource = contentSource,
            SitePath = sitePath,
            Language = language,
            DryRun = dryRun,
            Strict = strict,
            Overwrite = overwrite,
            PreserveHtml = preserveHtml,
            GenerateReport = generateReport,
            BaseUrl = baseUrl
        };

        ImportResult result;
        try
        {
            result = HtmlDemoImporter.Import(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"导入失败: {ex.Message}");
            return 1;
        }

        if (!dryRun)
        {
            var synced = SyncTemplates(rootDir, themeName, force);
            Console.WriteLine($"  bukit.templates.yaml: {(synced ? "已创建" : "跳过")}");
        }

        if (use && !dryRun)
        {
            var resolved2 = ConfigPathResolver.Resolve(
                command.GetString("--config"), command.GetString("--site"));
            var useResult = await ThemeCommand.SetThemeAsync(themeName,
                resolved2.FullConfigPath, resolved2.RootDir,
                brand: null, primaryColor: null, accentColor: null);
            if (useResult != 0) return useResult;
            Console.WriteLine("  主题已设置");
        }

        if (verify)
        {
            var verifyResult = await CloneVerifier.VerifyCloneAsync(
                command, rootDir, failOnVisualDiff: false, visualThreshold: 0.03);
            if (verifyResult != 0) return verifyResult;
        }

        return 0;
    }

    private static bool SyncTemplates(string rootDir, string themeName, bool force)
    {
        var layoutsDir = Path.Combine(rootDir, "themes", themeName, "layouts");
        if (!Directory.Exists(layoutsDir))
            return false;

        var manifestPath = Path.Combine(layoutsDir, "bukit.templates.yaml");
        if (File.Exists(manifestPath) && !force)
            return false;

        var htmlFiles = Directory.GetFiles(layoutsDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("templates:");

        foreach (var file in htmlFiles)
        {
            var relative = Path.GetRelativePath(layoutsDir, file).Replace('\\', '/');
            var text = File.ReadAllText(file);

            var needsPageContent = text.Contains("p.content", StringComparison.Ordinal) ||
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

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"未知的 import 子命令: {sub}");
        Console.Error.WriteLine("可用: html-demo");
        return 2;
    }
}
