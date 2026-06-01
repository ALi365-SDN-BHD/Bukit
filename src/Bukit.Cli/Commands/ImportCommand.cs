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
        var records = ImportSeedRecordReader.ReadDirectory(inputDir);
        var written = ImportSeedContentWriter.WriteMarkdown(outputDir, records, command.GetBool("--force"));
        Console.WriteLine($"seed import 完成: records={records.Count} written={written} output={outputDir}");
        return Task.FromResult(0);
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
        var language = command.GetString("--language") ?? "zh";
        var dryRun = command.GetBool("--dry-run");
        var strict = command.GetBool("--strict");
        var overwrite = command.GetBool("--overwrite");
        var preserveHtml = !command.GetBool("--no-preserve-html");
        var generateReport = !command.GetBool("--no-report");
        var baseUrl = command.GetString("--base-url");
        var routeMapPath = command.GetString("--route-map");
        var pushNotion = command.GetBool("--push-notion");
        var notionDatabaseId = command.GetString("--notion-database-id");
        var notionTokenEnv = command.GetString("--notion-token-env") ?? "NOTION_TOKEN";
        var notionReport = command.GetString("--notion-report");
        var validateNotionSchema = !command.GetBool("--no-validate-notion-schema");

        var noMarkdownDraftExplicit = command.Options.ContainsKey("--no-markdown-draft");
        var noMarkdownDraft = command.GetBool("--no-markdown-draft");

        var resolved = ConfigPathResolver.Resolve(
            command.GetString("--config"), command.GetString("--site"));
        var rootDir = resolved.RootDir;
        if (!contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("json", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("yaml", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("markdown", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"不支持的内容源类型: {contentSource}");
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
            if (string.IsNullOrWhiteSpace(notionDatabaseId))
            {
                Console.Error.WriteLine("缺少必填选项: --notion-database-id <id>");
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
            Strict = strict,
            Overwrite = overwrite,
            PreserveHtml = preserveHtml,
            GenerateReport = generateReport,
            BaseUrl = baseUrl,
            NoMarkdownDraft = noMarkdownDraft,
            RouteMapPath = routeMapPath,
            NotionDatabaseId = notionDatabaseId,
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
            var useResult = await ThemeCommand.SetThemeAsync(themeName,
                resolved2.FullConfigPath, resolved2.RootDir,
                brand: null, primaryColor: null, accentColor: null);
            if (useResult != 0) return useResult;
            Console.WriteLine("  主题已设置");
        }

        if (pushNotion)
        {
            var pushResult = await PushGeneratedSeedToNotionAsync(
                result,
                rootDir,
                themeName,
                contentSource,
                notionDatabaseId!,
                notionTokenEnv,
                notionReport,
                validateNotionSchema);
            if (pushResult != 0) return pushResult;
        }

        if (verify)
        {
            var verifyResult = await VerifyImportAsync(result, rootDir, themeName);
            if (verifyResult != 0) return verifyResult;
        }

        return 0;
    }

    private static async Task<int> PushGeneratedSeedToNotionAsync(
        ImportResult result,
        string rootDir,
        string themeName,
        string contentSource,
        string databaseId,
        string tokenEnv,
        string? reportPath,
        bool validateSchema)
    {
        var siteDir = string.IsNullOrWhiteSpace(result.SitePath)
            ? Path.Combine(rootDir, "sites", themeName)
            : result.SitePath;

        if (validateSchema)
        {
            Console.WriteLine("校验 Notion schema...");
            var token = Environment.GetEnvironmentVariable(tokenEnv);
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.Error.WriteLine($"{tokenEnv} is required for schema validation.");
                return 2;
            }
            using var http = NotionCommand.CreateHttpClient();
            var validationReport = await NotionSchemaValidator.ValidateAsync(
                http, databaseId, token, null);

            if (!validationReport.Success)
            {
                Console.Error.WriteLine("Notion schema validation failed:");
                foreach (var f in validationReport.FieldResults.Where(r => r.Result != "OK"))
                    Console.Error.WriteLine($"  {f.Name}: {f.Result} - {f.Message}");
                return 2;
            }
            Console.WriteLine("  Schema validation passed.");
        }

        var seedDir = contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(siteDir, "notion-seed")
            : Path.Combine(siteDir, "data");

        var options = new Dictionary<string, string?>
        {
            ["--input"] = seedDir,
            ["--database-id"] = databaseId,
            ["--token-env"] = tokenEnv,
            ["--mode"] = "upsert",
            ["--unique-field"] = "Slug"
        };
        if (!string.IsNullOrWhiteSpace(reportPath))
            options["--report"] = Path.IsPathRooted(reportPath)
                ? reportPath
                : Path.Combine(siteDir, reportPath);

        return await NotionCommand.RunAsync(new CliBoundCommand(options, ["push"]));
    }

    private static async Task<int> VerifyImportAsync(ImportResult result, string rootDir, string themeName)
    {
        var siteDir = string.IsNullOrWhiteSpace(result.SitePath)
            ? Path.Combine(rootDir, "sites", themeName)
            : result.SitePath;
        var siteConfig = Path.Combine(siteDir, "site.yaml");

        var doctorResult = await DoctorCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>
        {
            ["--config"] = siteConfig
        }, []));
        if (doctorResult != 0) return doctorResult;

        return await BuildCommand.RunAsync(new CliBoundCommand(new Dictionary<string, string?>
        {
            ["--config"] = siteConfig
        }, []));
    }

    private static int Unknown(string sub)
    {
        Console.Error.WriteLine($"未知的 import 子命令: {sub}");
        Console.Error.WriteLine("可用: html-demo");
        return 2;
    }
}
