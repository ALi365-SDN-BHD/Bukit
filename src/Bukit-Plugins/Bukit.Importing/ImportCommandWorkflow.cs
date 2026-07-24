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
                $"Unknown import subcommand: {options.Subcommand}",
                "Available: html-demo, seed")
        };
    }

    private static ImportCommandResult Seed(ImportCommandOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SeedDir))
            return ErrorResult(2, "Missing required argument: <seed-dir>");

        var inputDir = ImportPathResolver.ResolveInputFromWorkingDir(options.WorkingDir, options.SeedDir);
        if (!Directory.Exists(inputDir))
            return ErrorResult(2, $"Seed directory does not exist: {inputDir}");

        if (string.IsNullOrWhiteSpace(options.OutputDir))
            return ErrorResult(2, "Missing required option: --output <content-dir>");

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
                        $"seed import complete: records={result.RecordsRead} written={result.FilesWritten} output={result.OutputDir}")
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
            return ErrorResult(2, "Missing required argument: <demo-dir>");

        var demoDir = ImportPathResolver.ResolveInputFromWorkingDir(options.WorkingDir, options.DemoDir);
        if (!Directory.Exists(demoDir))
            return ErrorResult(2, $"Demo directory does not exist: {demoDir}");

        var themeName = options.ThemeName;
        if (string.IsNullOrWhiteSpace(themeName))
            return ErrorResult(2, "Missing required option: --theme <name>");
        if (!IsSafeThemeName(themeName))
            return ErrorResult(2, $"Invalid theme name: {themeName}");

        var (rootDir, fullConfigPath) = ImportPathResolver.ResolveRoot(options.ConfigPath, options.Site, options.RootDir, options.WorkingDir);
        var contentSource = options.ContentSource;
        var buildSource = options.BuildSource;
        if (!contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("json", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("yaml", StringComparison.OrdinalIgnoreCase))
            return ErrorResult(2, $"Unsupported content source type: {contentSource}");

        if (!buildSource.Equals("markdown", StringComparison.OrdinalIgnoreCase) &&
            !buildSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
            return ErrorResult(2, $"Unsupported build source type: {buildSource}");

        if (buildSource.Equals("notion", StringComparison.OrdinalIgnoreCase) &&
            !contentSource.Equals("notion", StringComparison.OrdinalIgnoreCase))
            return ErrorResult(2, "--build-source notion requires --content-source notion.");

        var sitePath = ImportPathResolver.ResolveSitePath(rootDir, options.SitePath);
        var routeMapPath = ImportPathResolver.ResolveRouteMapPath(demoDir, options.RouteMapPath);

        if (options.PushNotion)
        {
            if (options.DryRun)
                return ErrorResult(2, "--push-notion cannot be used with --dry-run. Generate first, then push.");
            if (!options.GenerateSeed)
                return ErrorResult(2, "--push-notion requires seed data. Do not use --no-seed.");
            if (options.CreateMissingNotionDatabases && string.IsNullOrWhiteSpace(options.NotionParentPageId))
                return ErrorResult(2, "--create-missing-notion-databases requires --notion-parent-page-id <id>.");
        }

        var themeDir = Path.Combine(rootDir, "themes", themeName);
        if (!options.DryRun && Directory.Exists(themeDir) && !options.Force)
            return ErrorResult(2, $"Theme already exists: {themeName}. Use --force to overwrite.");

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
            messages.Add(new ImportCommandMessage("error", $"Import failed: {importException.Message}"));
            return new ImportCommandResult
            {
                ExitCode = importException.Kind == ImportErrorKind.UserInput ? 2 : 1,
                Messages = messages
            };
        }
        if (importCapture.Exception is not null)
        {
            messages.Add(new ImportCommandMessage("error", $"Import failed: {importCapture.Exception.Message}"));
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
            messages.Add(new ImportCommandMessage("info", "  Theme set"));
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
