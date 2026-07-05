using Bukit.Cli.Shared;
using Bukit.Cli.Shared.Cli.Binding;
using Bukit.Importing;

namespace Bukit.Labs.Cli.Commands;

public static class ImportCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var sub = command.GetArgument(0) ?? "";
        return sub switch
        {
            "html-demo" => await HtmlDemoAsync(command),
            "seed" => await SeedAsync(command),
            _ => Unknown(sub)
        };
    }

    private static Task<int> SeedAsync(CliBoundCommand command)
    {
        var inputArg = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(inputArg))
        {
            Console.Error.WriteLine("缺少必填参数: <seed-dir>");
            return Task.FromResult(2);
        }

        var inputDir = Path.GetFullPath(inputArg);
        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"seed 目录不存在: {inputDir}");
            return Task.FromResult(2);
        }

        var output = command.GetString("--output");
        if (string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("缺少必填选项: --output <content-dir>");
            return Task.FromResult(2);
        }

        var outputDir = Path.GetFullPath(output);
        try
        {
            var result = ImportSeedService.Import(inputDir, outputDir, command.GetBool("--force"));
            Console.WriteLine($"seed import 完成: records={result.RecordsRead} written={result.FilesWritten} output={result.OutputDir}");
            return Task.FromResult(0);
        }
        catch (ImportException ex) when (ex.Kind == ImportErrorKind.UserInput)
        {
            Console.Error.WriteLine(ex.Message);
            return Task.FromResult(2);
        }
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
        var buildSource = command.GetString("--build-source") ?? "markdown";
        var sitePath = command.GetString("--site-path");
        var language = command.GetString("--language") ?? "zh";
        var dryRun = command.GetBool("--dry-run");
        var strictVal = command.GetString("--strict");
        var strictMode = strictVal != null
            ? (string.Equals(strictVal, "warn", StringComparison.OrdinalIgnoreCase) ? "warn" : "fail")
            : null;
        var overwrite = command.GetBool("--overwrite");
        var preserveHtml = !command.GetBool("--no-preserve-html");
        var generateReport = !command.GetBool("--no-report");
        var baseUrl = command.GetString("--base-url");
        var routeMapPath = command.GetString("--route-map");
        var pushNotion = command.GetBool("--push-notion");
        var notionDatabaseId = command.GetString("--notion-database-id");
        var notionDatabaseMap = command.GetString("--notion-database-map");
        var createMissingNotionDatabases = command.GetBool("--create-missing-notion-databases");
        var notionParentPageId = command.GetString("--notion-parent-page-id");
        var notionGeneratedDatabaseMap = command.GetString("--notion-generated-database-map");
        var notionTokenEnv = command.GetString("--notion-token-env") ?? "NOTION_TOKEN";
        var notionReport = command.GetString("--notion-report");
        var validateNotionSchema = !command.GetBool("--no-validate-notion-schema");

        var resolved = ConfigPathResolver.Resolve(
            command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        if (!contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("json", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"不支持的内容源类型: {contentSource}");
            return 2;
        }
        if (!buildSource.Equals("markdown", StringComparison.OrdinalIgnoreCase) &&
            !buildSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"不支持的构建内容源类型: {buildSource}");
            return 2;
        }
        if (buildSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--build-source notion requires --content-source notion.");
            return 2;
        }

        if (!string.IsNullOrWhiteSpace(sitePath) && !Path.IsPathRooted(sitePath))
            sitePath = Path.GetFullPath(Path.Combine(rootDir, sitePath));

        if (!string.IsNullOrWhiteSpace(routeMapPath) && !Path.IsPathRooted(routeMapPath))
            routeMapPath = Path.GetFullPath(Path.Combine(demoDir, routeMapPath));

        if (pushNotion)
        {
            if (dryRun)
            {
                Console.Error.WriteLine("--push-notion 不能与 --dry-run 同时使用。先生成草稿后再执行实际推送。");
                return 2;
            }
            if (!generateSeed)
            {
                Console.Error.WriteLine("--push-notion 需要 seed 数据。请不要同时使用 --no-seed。");
                return 2;
            }
            if (createMissingNotionDatabases && string.IsNullOrWhiteSpace(notionParentPageId))
            {
                Console.Error.WriteLine("--create-missing-notion-databases 需要 --notion-parent-page-id <id>。");
                return 2;
            }
        }

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
            StrictMode = strictMode,
            Overwrite = overwrite,
            PreserveHtml = preserveHtml,
            GenerateReport = generateReport,
            BaseUrl = baseUrl,
            BuildSource = buildSource.ToLowerInvariant(),
            RouteMapPath = routeMapPath,
            NotionDatabaseId = notionDatabaseId,
            NotionDatabaseMap = notionDatabaseMap,
            NotionTokenEnv = notionTokenEnv
        };

        ImportResult result;
        try
        {
            result = HtmlDemoImporter.Import(options);
        }
        catch (ImportException ex) when (ex.Kind == ImportErrorKind.UserInput)
        {
            Console.Error.WriteLine($"导入失败: {ex.Message}");
            return 2;
        }
        catch (ImportException ex)
        {
            Console.Error.WriteLine($"导入失败: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"导入失败: {ex.Message}");
            return 1;
        }

        if (use && !dryRun)
        {
            var resolved2 = ConfigPathResolver.Resolve(
                command.GetString("--config"), command.GetString("--site"));
            var useResult = await ImportThemeSelectionService.SetThemeAsync(themeName,
                resolved2.FullConfigPath, resolved2.RootDir,
                brand: null, primaryColor: null, accentColor: null);
            if (useResult != 0) return useResult;
            Console.WriteLine("  主题已设置");
        }

        if (pushNotion)
        {
            var pushResult = await ImportNotionPushWorkflow.PushGeneratedSeedAsync(
                new ImportGeneratedNotionPushOptions
                {
                    ImportResult = result,
                    RootDir = rootDir,
                    ThemeName = themeName,
                    ContentSource = contentSource,
                    DatabaseId = notionDatabaseId,
                    DatabaseMap = notionDatabaseMap,
                    CreateMissingDatabases = createMissingNotionDatabases,
                    ParentPageId = notionParentPageId,
                    GeneratedDatabaseMap = notionGeneratedDatabaseMap,
                    TokenEnv = notionTokenEnv,
                    ReportPath = notionReport,
                    ValidateSchema = validateNotionSchema,
                    DryRun = dryRun,
                    GenerateSeed = generateSeed
                });
            if (pushResult != 0) return pushResult;
        }

        if (verify)
        {
            var verifyResult = await ImportVerifyWorkflow.VerifyAsync(result, rootDir, themeName);
            if (verifyResult != 0) return verifyResult;
        }

        return 0;
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"未知的 import 子命令: {sub}");
        Console.Error.WriteLine("可用: html-demo");
        return 2;
    }
}
