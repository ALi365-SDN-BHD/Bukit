namespace Bukit.Importing;

public static class ImportCommandWorkflow
{
    public static async Task<ImportCommandResult> RunAsync(ImportCommandOptions options)
    {
        return options.Subcommand switch
        {
            "html-demo" => await HtmlDemoAsync(options),
            "seed" => Seed(options),
            _ => ErrorResult(2,
                $"未知的 import 子命令: {options.Subcommand}",
                "可用: html-demo")
        };
    }

    private static ImportCommandResult Seed(ImportCommandOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SeedDir))
            return ErrorResult(2, "缺少必填参数: <seed-dir>");

        var inputDir = ImportPathResolver.ResolveInputFromWorkingDir(options.WorkingDir, options.SeedDir);
        if (!Directory.Exists(inputDir))
            return ErrorResult(2, $"seed 目录不存在: {inputDir}");

        if (string.IsNullOrWhiteSpace(options.OutputDir))
            return ErrorResult(2, "缺少必填选项: --output <content-dir>");

        var outputDir = ImportPathResolver.ResolveInputFromWorkingDir(options.WorkingDir, options.OutputDir);
        try
        {
            var result = ImportSeedService.Import(inputDir, outputDir, options.Force);
            return new ImportCommandResult
            {
                ExitCode = 0,
                SeedResult = result,
                Messages =
                [
                    new ImportCommandMessage("info",
                        $"seed import 完成: records={result.RecordsRead} written={result.FilesWritten} output={result.OutputDir}")
                ],
                Artifacts = BuildSeedArtifacts(result)
            };
        }
        catch (ImportException ex) when (ex.Kind == ImportErrorKind.UserInput)
        {
            return ErrorResult(2, ex.Message);
        }
    }

    private static async Task<ImportCommandResult> HtmlDemoAsync(ImportCommandOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.DemoDir))
            return ErrorResult(2, "缺少必填参数: <demo-dir>");

        var demoDir = ImportPathResolver.ResolveInputFromWorkingDir(options.WorkingDir, options.DemoDir);
        if (!Directory.Exists(demoDir))
            return ErrorResult(2, $"demo 目录不存在: {demoDir}");

        var themeName = options.ThemeName;
        if (string.IsNullOrWhiteSpace(themeName))
            return ErrorResult(2, "缺少必填选项: --theme <名称>");
        if (!IsSafeThemeName(themeName))
            return ErrorResult(2, $"无效的主题名: {themeName}");

        var (rootDir, fullConfigPath) = ResolveRoot(options);
        var contentSource = options.ContentSource;
        var buildSource = options.BuildSource;
        if (!contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("json", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("yaml", StringComparison.OrdinalIgnoreCase))
            return ErrorResult(2, $"不支持的内容源类型: {contentSource}");

        if (!buildSource.Equals("markdown", StringComparison.OrdinalIgnoreCase) &&
            !buildSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
            return ErrorResult(2, $"不支持的构建内容源类型: {buildSource}");

        if (buildSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
            return ErrorResult(2, "--build-source notion requires --content-source notion.");

        var sitePath = ImportPathResolver.ResolveSitePath(rootDir, options.SitePath);
        var routeMapPath = ImportPathResolver.ResolveRouteMapPath(demoDir, options.RouteMapPath);

        if (options.PushNotion)
        {
            if (options.DryRun)
                return ErrorResult(2, "--push-notion 不能与 --dry-run 同时使用。先生成草稿后再执行实际推送。");
            if (!options.GenerateSeed)
                return ErrorResult(2, "--push-notion 需要 seed 数据。请不要同时使用 --no-seed。");
            if (options.CreateMissingNotionDatabases && string.IsNullOrWhiteSpace(options.NotionParentPageId))
                return ErrorResult(2, "--create-missing-notion-databases 需要 --notion-parent-page-id <id>。");
        }

        var themeDir = Path.Combine(rootDir, "themes", themeName);
        if (!options.DryRun && Directory.Exists(themeDir) && !options.Force)
            return ErrorResult(2, $"主题已存在: {themeName}。使用 --force 覆盖。");

        var importOptions = new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = themeName,
            RootDir = rootDir,
            Force = options.Force,
            Use = options.Use,
            Verify = options.Verify,
            ExtractContent = options.ExtractContent,
            GenerateSeed = options.GenerateSeed,
            ContentSource = contentSource,
            SitePath = sitePath,
            Language = options.Language,
            DryRun = options.DryRun,
            StrictMode = options.StrictMode,
            Overwrite = options.Overwrite,
            PreserveHtml = options.PreserveHtml,
            GenerateReport = options.GenerateReport,
            BaseUrl = options.BaseUrl,
            BuildSource = buildSource.ToLowerInvariant(),
            RouteMapPath = routeMapPath,
            NotionDatabaseId = options.NotionDatabaseId,
            NotionDatabaseMap = options.NotionDatabaseMap,
            NotionTokenEnv = options.NotionTokenEnv
        };

        var messages = new List<ImportCommandMessage>();
        var importCapture = await CaptureConsoleAsync(() => Task.FromResult(HtmlDemoImporter.Import(importOptions)));
        messages.AddRange(importCapture.Messages);
        if (importCapture.Exception is ImportException importException)
        {
            messages.Add(new ImportCommandMessage("error", $"导入失败: {importException.Message}"));
            return new ImportCommandResult
            {
                ExitCode = importException.Kind == ImportErrorKind.UserInput ? 2 : 1,
                Messages = messages
            };
        }
        if (importCapture.Exception is not null)
        {
            messages.Add(new ImportCommandMessage("error", $"导入失败: {importCapture.Exception.Message}"));
            return new ImportCommandResult
            {
                ExitCode = 1,
                Messages = messages
            };
        }

        var result = importCapture.Result!;

        if (options.Use && !options.DryRun)
        {
            var useCapture = await CaptureConsoleAsync(() =>
                ImportThemeSelectionService.SetThemeAsync(
                    themeName,
                    fullConfigPath,
                    rootDir,
                    brand: null,
                    primaryColor: null,
                    accentColor: null));
            messages.AddRange(useCapture.Messages);
            if (useCapture.Exception is not null)
                return ExceptionResult(1, messages, useCapture.Exception);
            if (useCapture.Result != 0)
                return new ImportCommandResult
                {
                    ExitCode = useCapture.Result,
                    HtmlDemoResult = result,
                    Messages = messages,
                    Diagnostics = BuildCommandDiagnostics(result)
                };
            messages.Add(new ImportCommandMessage("info", "  主题已设置"));
        }

        if (options.PushNotion)
        {
            var pushCapture = await CaptureConsoleAsync(() =>
                ImportNotionPushWorkflow.PushGeneratedSeedAsync(
                    new ImportGeneratedNotionPushOptions
                    {
                        ImportResult = result,
                        RootDir = rootDir,
                        ThemeName = themeName,
                        ContentSource = contentSource,
                        DatabaseId = options.NotionDatabaseId,
                        DatabaseMap = options.NotionDatabaseMap,
                        CreateMissingDatabases = options.CreateMissingNotionDatabases,
                        ParentPageId = options.NotionParentPageId,
                        GeneratedDatabaseMap = options.NotionGeneratedDatabaseMap,
                        TokenEnv = options.NotionTokenEnv,
                        ReportPath = options.NotionReport,
                        ValidateSchema = options.ValidateNotionSchema,
                        DryRun = options.DryRun,
                        GenerateSeed = options.GenerateSeed
                    }));
            messages.AddRange(pushCapture.Messages);
            if (pushCapture.Exception is not null)
                return ExceptionResult(1, messages, pushCapture.Exception);
            if (pushCapture.Result != 0)
                return new ImportCommandResult
                {
                    ExitCode = pushCapture.Result,
                    HtmlDemoResult = result,
                    Messages = messages,
                    Diagnostics = BuildCommandDiagnostics(result)
                };
        }

        if (options.Verify)
        {
            var verifyCapture = await CaptureConsoleAsync(() => ImportVerifyWorkflow.VerifyAsync(result, rootDir, themeName));
            messages.AddRange(verifyCapture.Messages);
            if (verifyCapture.Exception is not null)
                return ExceptionResult(1, messages, verifyCapture.Exception);
            if (verifyCapture.Result != 0)
                return new ImportCommandResult
                {
                    ExitCode = verifyCapture.Result,
                    HtmlDemoResult = result,
                    Messages = messages,
                    Diagnostics = BuildCommandDiagnostics(result)
                };
        }

        return new ImportCommandResult
        {
            ExitCode = 0,
            HtmlDemoResult = result,
            Messages = messages,
            Diagnostics = BuildCommandDiagnostics(result),
            Artifacts = BuildHtmlDemoArtifacts(result)
        };
    }

    private static (string RootDir, string FullConfigPath) ResolveRoot(ImportCommandOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            var fullConfigPath = Path.GetFullPath(Path.IsPathRooted(options.ConfigPath)
                ? options.ConfigPath
                : Path.Combine(options.WorkingDir, options.ConfigPath));
            var rootDir = Path.GetDirectoryName(fullConfigPath) ?? options.WorkingDir;
            var configFileName = Path.GetFileName(fullConfigPath);
            var siteDir = Directory.GetParent(rootDir);
            if (configFileName.Equals("site.yaml", StringComparison.OrdinalIgnoreCase) &&
                siteDir?.Name.Equals("sites", StringComparison.OrdinalIgnoreCase) == true &&
                siteDir.Parent is not null)
            {
                rootDir = siteDir.Parent.FullName;
            }
            return (rootDir, fullConfigPath);
        }

        if (!string.IsNullOrWhiteSpace(options.Site))
        {
            var rootDir = options.RootDir;
            var fileName = NormalizeSiteFileName(options.Site);
            var fullConfigPath = Path.GetFullPath(Path.Combine(rootDir, "sites", fileName));
            var safeRoot = Path.GetFullPath(Path.Combine(rootDir, "sites")) + Path.DirectorySeparatorChar;
            if (!fullConfigPath.StartsWith(safeRoot, PlatformPathComparison))
                throw new ImportException(ImportErrorKind.UserInput, $"--site value '{options.Site}' resolves to a path outside the sites directory.");
            return (rootDir, fullConfigPath);
        }

        var defaultFullConfigPath = Path.GetFullPath(Path.Combine(options.RootDir, "site.yaml"));
        var defaultRootDir = Path.GetDirectoryName(defaultFullConfigPath) ?? options.RootDir;
        return (defaultRootDir, defaultFullConfigPath);
    }

    private static StringComparison PlatformPathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string NormalizeSiteFileName(string site)
    {
        var trimmed = site.Trim();
        if (trimmed.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return trimmed + ".yaml";
    }

    private static bool IsSafeThemeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (name is "." or "..")
            return false;

        return !Path.IsPathRooted(name) &&
               name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) < 0;
    }

    private static ImportCommandResult ErrorResult(int exitCode, params string[] messages)
        => new()
        {
            ExitCode = exitCode,
            Messages = messages.Select(message => new ImportCommandMessage("error", message)).ToList()
        };

    private static ImportCommandResult ExceptionResult(
        int exitCode,
        IReadOnlyList<ImportCommandMessage> existingMessages,
        Exception exception)
    {
        var messages = existingMessages.ToList();
        messages.Add(new ImportCommandMessage("error", exception.Message));
        return new ImportCommandResult
        {
            ExitCode = exitCode,
            Messages = messages
        };
    }

    private static IReadOnlyList<ImportCommandArtifact> BuildSeedArtifacts(ImportSeedResult result)
    {
        var artifacts = new List<ImportCommandArtifact>
        {
            new("directory", result.OutputDir, "seed markdown output")
        };
        artifacts.AddRange(result.WrittenFiles.Select(path => new ImportCommandArtifact("content", path, "seed markdown file")));
        return artifacts;
    }

    private static IReadOnlyList<ImportCommandArtifact> BuildHtmlDemoArtifacts(ImportResult result)
    {
        var artifacts = new List<ImportCommandArtifact>
        {
            new("theme", result.ThemePath, "generated theme")
        };
        if (!string.IsNullOrWhiteSpace(result.SitePath))
        {
            artifacts.Add(new ImportCommandArtifact("site", result.SitePath, "generated site"));
            var reportPath = Path.Combine(result.SitePath, "import-report.md");
            if (File.Exists(reportPath))
                artifacts.Add(new ImportCommandArtifact("report", reportPath, "import report"));
        }

        return artifacts;
    }

    private static IReadOnlyList<ImportCommandDiagnostic> BuildCommandDiagnostics(ImportResult result)
        => result.Diagnostics
            .Select(diagnostic => new ImportCommandDiagnostic(
                diagnostic.Code,
                ToCommandSeverity(diagnostic.Severity),
                diagnostic.Message,
                diagnostic.FilePath))
            .ToArray();

    private static string ToCommandSeverity(ImportDiagnosticSeverity severity)
        => severity switch
        {
            ImportDiagnosticSeverity.Error => "error",
            ImportDiagnosticSeverity.Warning => "warning",
            _ => "info"
        };

    private static async Task<CapturedConsole<T>> CaptureConsoleAsync<T>(Func<Task<T>> action)
    {
        var capture = await ImportConsoleCapture.CaptureAsync(action);
        var messages = new List<ImportCommandMessage>();
        messages.AddRange(capture.StdOutLines.Select(line => new ImportCommandMessage("info", line)));
        messages.AddRange(capture.StdErrLines.Select(line => new ImportCommandMessage("error", line)));
        return new CapturedConsole<T>(capture.Result, capture.Exception, messages);
    }

    private sealed record CapturedConsole<T>(
        T? Result,
        Exception? Exception,
        IReadOnlyList<ImportCommandMessage> Messages);
}
