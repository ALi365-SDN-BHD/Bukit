using System.Text.Json;
using Bukit.Importing;
using Bukit.Plugin.Abstractions.Protocol;

namespace Bukit.Plugin.Import;

public static class ImportPluginOptionsMapper
{
    public static ImportCommandOptions Map(PluginInvokeRequest request)
    {
        var invocation = MapInvocation(request);
        if (invocation.Kind == ImportPluginInvocationKind.Import && invocation.ImportOptions is not null)
            return invocation.ImportOptions;

        throw new ImportPluginOptionsException(
            "plugin.import.unsupportedCommand",
            $"Unknown import subcommand: {string.Join(" ", GetCommandPath(request))}");
    }

    public static ImportPluginMappedInvocation MapInvocation(PluginInvokeRequest request)
    {
        var rootDir = ImportPluginPathGuard.NormalizeRoot(request.Context.RootDir);
        var workingDir = ImportPluginPathGuard.NormalizeWorkingDir(rootDir, request.Context.WorkingDir);
        var path = GetCommandPath(request);

        if (path.SequenceEqual(["import", "html-demo"], StringComparer.Ordinal))
        {
            return new ImportPluginMappedInvocation(
                ImportPluginInvocationKind.Import,
                ImportOptions: MapHtmlDemo(request, rootDir, workingDir));
        }

        if (path.SequenceEqual(["import", "seed"], StringComparer.Ordinal))
        {
            return new ImportPluginMappedInvocation(
                ImportPluginInvocationKind.Import,
                ImportOptions: MapSeed(request, rootDir, workingDir));
        }

        if (path.SequenceEqual(["notion", "push"], StringComparer.Ordinal))
        {
            return new ImportPluginMappedInvocation(
                ImportPluginInvocationKind.NotionPush,
                NotionPushOptions: MapNotionPush(request, rootDir, workingDir));
        }

        if (path.SequenceEqual(["notion", "validate-schema"], StringComparer.Ordinal))
        {
            return new ImportPluginMappedInvocation(
                ImportPluginInvocationKind.NotionValidateSchema,
                SchemaValidationOptions: MapNotionValidateSchema(request, rootDir, workingDir));
        }

        throw new ImportPluginOptionsException(
            "plugin.import.unsupportedCommand",
            $"未知的 import 子命令: {string.Join(" ", path)}");
    }

    private static ImportCommandOptions MapHtmlDemo(PluginInvokeRequest request, string rootDir, string workingDir)
    {
        var demoDir = request.Command.Arguments.Count > 0
            ? ImportPluginPathGuard.ResolveRequiredPath(rootDir, workingDir, request.Command.Arguments[0], "demo-dir")
            : null;
        var configPath = ImportPluginPathGuard.ResolveOptionalPath(
            rootDir,
            workingDir,
            ReadString(request, "--config") ?? request.Context.ConfigPath,
            "--config");
        var sitePath = ResolveWritablePath(rootDir, rootDir, ReadString(request, "--site-path"), "--site-path");
        var notionSiteDir = sitePath
            ?? Path.Combine(rootDir, "sites", ReadString(request, "--theme") ?? Path.GetFileName(workingDir));
        var createMissing = ReadBool(request, "--create-missing-notion-databases");
        var generatedMap = ResolveWritablePath(
            rootDir,
            notionSiteDir,
            ReadString(request, "--notion-generated-database-map"),
            "--notion-generated-database-map");
        var databaseMap = ImportPluginPathGuard.PreserveSafeRelativeOrRootedPath(
            rootDir,
            ReadString(request, "--notion-database-map"),
            "--notion-database-map");
        // With --create-missing-notion-databases and no generated map, the workflow
        // writes ids back into this very file, so it must be inside the write scope.
        if (createMissing && generatedMap is null && databaseMap is not null)
        {
            var mapWritePath = Path.IsPathRooted(databaseMap)
                ? databaseMap
                : Path.Combine(notionSiteDir, databaseMap);
            if (!ImportPluginCommandSpecs.IsWritablePath(rootDir, mapWritePath))
            {
                throw new ImportPluginOptionsException(
                    "plugin.import.pathDenied",
                    "--notion-database-map is rewritten when --create-missing-notion-databases is used; " +
                    "it must stay inside the plugin's declared write scope.");
            }
        }
        var routeMapPath = ImportPluginPathGuard.ResolveOptionalPath(rootDir, demoDir ?? workingDir, ReadString(request, "--route-map"), "--route-map");
        var tokenEnv = ReadString(request, "--notion-token-env") ?? "NOTION_TOKEN";
        var pushNotion = ReadBool(request, "--push-notion");
        if (pushNotion)
            EnsureEnvironmentGranted(request, tokenEnv);

        var dryRun = ReadBool(request, "--dry-run");
        var use = ReadBool(request, "--use");
        // --use writes the theme name into the config; a read-only config is fine otherwise.
        if (use && !dryRun && configPath is not null &&
            !ImportPluginCommandSpecs.IsWritableConfigPath(rootDir, configPath))
        {
            throw new ImportPluginOptionsException(
                "plugin.import.pathDenied",
                "--use rewrites the theme name in --config; the config must be a writable file inside the plugin's declared write scope.");
        }

        return new ImportCommandOptions
        {
            Subcommand = "html-demo",
            RootDir = rootDir,
            WorkingDir = workingDir,
            ConfigPath = configPath,
            Site = ReadString(request, "--site"),
            DemoDir = demoDir,
            ThemeName = ReadString(request, "--theme"),
            Force = ReadBool(request, "--force"),
            Use = use,
            Verify = ReadBool(request, "--verify"),
            ExtractContent = !ReadBool(request, "--no-extract-content"),
            GenerateSeed = !ReadBool(request, "--no-seed"),
            ContentSource = ReadString(request, "--content-source") ?? "notion",
            BuildSource = ReadString(request, "--build-source") ?? "markdown",
            SitePath = sitePath,
            Language = ReadString(request, "--language") ?? "",
            DryRun = dryRun,
            StrictMode = ResolveStrictMode(ReadString(request, "--strict")),
            Overwrite = ReadBool(request, "--overwrite"),
            PreserveHtml = !ReadBool(request, "--no-preserve-html"),
            GenerateReport = !ReadBool(request, "--no-report"),
            BaseUrl = ReadString(request, "--base-url"),
            RouteMapPath = routeMapPath,
            PushNotion = pushNotion,
            NotionDatabaseId = ReadString(request, "--notion-database-id"),
            NotionDatabaseMap = databaseMap,
            CreateMissingNotionDatabases = createMissing,
            NotionParentPageId = ReadString(request, "--notion-parent-page-id"),
            NotionGeneratedDatabaseMap = generatedMap,
            NotionTokenEnv = tokenEnv,
            NotionReport = ResolveWritablePath(
                rootDir,
                notionSiteDir,
                ReadString(request, "--notion-report"),
                "--notion-report"),
            ValidateNotionSchema = !ReadBool(request, "--no-validate-notion-schema")
        };
    }

    private static ImportCommandOptions MapSeed(PluginInvokeRequest request, string rootDir, string workingDir)
        => new()
        {
            Subcommand = "seed",
            RootDir = rootDir,
            WorkingDir = workingDir,
            ConfigPath = ImportPluginPathGuard.ResolveOptionalPath(
                rootDir,
                workingDir,
                ReadString(request, "--config") ?? request.Context.ConfigPath,
                "--config"),
            Site = ReadString(request, "--site"),
            SeedDir = request.Command.Arguments.Count > 0
                ? ImportPluginPathGuard.ResolveRequiredPath(rootDir, workingDir, request.Command.Arguments[0], "seed-dir")
                : null,
            OutputDir = ResolveWritablePath(rootDir, workingDir, ReadString(request, "--output"), "--output"),
            Force = ReadBool(request, "--force")
        };

    private static ImportNotionSeedPushOptions MapNotionPush(
        PluginInvokeRequest request,
        string rootDir,
        string workingDir)
    {
        var dryRun = ReadBool(request, "--dry-run");
        var tokenEnv = ReadString(request, "--token-env") ?? "NOTION_TOKEN";
        if (!dryRun || request.Command.Options.ContainsKey("--token-env"))
            EnsureEnvironmentGranted(request, tokenEnv);

        var createMissing = ReadBool(request, "--create-missing-databases");
        var generatedMapValue = ReadString(request, "--generated-database-map");
        var reportValue = ReadString(request, "--report");
        var inputDir = ImportPluginPathGuard.ResolveOptionalPath(
            rootDir,
            workingDir,
            ReadString(request, "--input"),
            "--input") ?? "";
        var databaseMap = ImportPluginPathGuard.ResolveOptionalPath(
            rootDir,
            workingDir,
            ReadString(request, "--database-map"),
            "--database-map");
        if (databaseMap is not null &&
            createMissing &&
            string.IsNullOrWhiteSpace(generatedMapValue) &&
            !ImportPluginCommandSpecs.IsWritablePath(rootDir, databaseMap))
        {
            throw new ImportPluginOptionsException(
                "plugin.import.pathDenied",
                "--database-map is rewritten when --create-missing-databases is used; it must stay inside the plugin's declared write scope.");
        }

        EnsureDefaultOutputsAreWritable(
            rootDir, inputDir, dryRun, createMissing, reportValue, generatedMapValue, databaseMap);

        return new ImportNotionSeedPushOptions
        {
            InputDir = inputDir,
            DatabaseId = ReadString(request, "--database-id"),
            DatabaseMapPath = databaseMap,
            CreateMissingDatabases = createMissing,
            ParentPageId = ReadString(request, "--parent-page-id"),
            GeneratedDatabaseMapPath = ResolveWritablePath(
                rootDir,
                workingDir,
                generatedMapValue,
                "--generated-database-map"),
            TokenEnv = tokenEnv,
            Mode = ReadString(request, "--mode") ?? "create",
            UniqueField = ReadString(request, "--unique-field") ?? "Slug",
            UpdateContent = ReadString(request, "--update-content") ?? "",
            DryRun = dryRun,
            ReportPath = ResolveWritablePath(rootDir, workingDir, reportValue, "--report"),
            ValidateSchema = !ReadBool(request, "--no-validate-schema")
        };
    }

    /// <summary>
    /// Paths the workflow derives itself (default report, default generated map,
    /// push lock) also have to stay inside the declared write scope.
    /// </summary>
    private static void EnsureDefaultOutputsAreWritable(
        string rootDir,
        string inputDir,
        bool dryRun,
        bool createMissing,
        string? reportValue,
        string? generatedMapValue,
        string? databaseMap)
    {
        if (string.IsNullOrWhiteSpace(inputDir))
            return;

        var defaults = new List<string>();
        if (!dryRun)
            defaults.Add(Path.Combine(inputDir, ".notion-push.lock"));
        if (string.IsNullOrWhiteSpace(reportValue))
            defaults.Add(Path.Combine(inputDir, dryRun ? "notion-push-plan.json" : "notion-push-report.json"));
        if (createMissing &&
            string.IsNullOrWhiteSpace(generatedMapValue) &&
            string.IsNullOrWhiteSpace(databaseMap))
        {
            defaults.Add(Path.Combine(inputDir, "notion-database-map.generated.yaml"));
        }

        foreach (var path in defaults)
        {
            if (!ImportPluginCommandSpecs.IsWritablePath(rootDir, path))
            {
                throw new ImportPluginOptionsException(
                    "plugin.import.pathDenied",
                    $"Default output '{path}' is outside the plugin's declared write scope: " +
                    $"{string.Join(", ", ImportPluginCommandSpecs.WriteRoots)}. " +
                    "Move the seed directory under one of those roots or pass an explicit output path.");
            }
        }
    }

    private static ImportNotionSchemaValidationOptions MapNotionValidateSchema(
        PluginInvokeRequest request,
        string rootDir,
        string workingDir)
    {
        var tokenEnv = ReadString(request, "--token-env") ?? "NOTION_TOKEN";
        EnsureEnvironmentGranted(request, tokenEnv);

        return new ImportNotionSchemaValidationOptions
        {
            DatabaseId = ReadString(request, "--database-id"),
            TokenEnv = tokenEnv,
            ReportPath = ResolveWritablePath(rootDir, workingDir, ReadString(request, "--report"), "--report")
        };
    }

    private static string? ResolveWritablePath(string rootDir, string baseDir, string? value, string name)
    {
        var resolved = ImportPluginPathGuard.ResolveOptionalPath(rootDir, baseDir, value, name);
        if (resolved is null)
            return null;

        if (!ImportPluginCommandSpecs.IsWritablePath(rootDir, resolved))
            throw new ImportPluginOptionsException(
                "plugin.import.pathDenied",
                $"{name} must stay inside the plugin's declared write scope: {string.Join(", ", ImportPluginCommandSpecs.WriteRoots)}.");

        return resolved;
    }

    private static void EnsureEnvironmentGranted(PluginInvokeRequest request, string tokenEnv)
    {
        if (string.IsNullOrWhiteSpace(tokenEnv))
            throw new ImportPluginOptionsException("plugin.import.envDenied", "--notion-token-env must not be empty.");

        if (!request.Permissions.Environment.Read.Contains(tokenEnv, StringComparer.Ordinal))
        {
            throw new ImportPluginOptionsException(
                "plugin.import.envDenied",
                $"Environment variable '{tokenEnv}' is not granted. Add it to permissions.environment.read.");
        }
    }

    private static string? ResolveStrictMode(string? value)
        => value is null
            ? null
            : string.Equals(value, "warn", StringComparison.OrdinalIgnoreCase) ? "warn" : "fail";

    private static IReadOnlyList<string> GetCommandPath(PluginInvokeRequest request)
        => request.Command.Path.Count > 0
            ? request.Command.Path
            : [request.Command.Name];

    private static string? ReadString(PluginInvokeRequest request, string name)
    {
        if (!request.Command.Options.TryGetValue(name, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            _ => value.GetRawText()
        };
    }

    private static bool ReadBool(PluginInvokeRequest request, string name)
    {
        if (!request.Command.Options.TryGetValue(name, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
            JsonValueKind.Number => value.TryGetInt32(out var parsed) && parsed != 0,
            _ => false
        };
    }
}

public enum ImportPluginInvocationKind
{
    Import,
    NotionPush,
    NotionValidateSchema
}

public sealed record ImportPluginMappedInvocation(
    ImportPluginInvocationKind Kind,
    ImportCommandOptions? ImportOptions = null,
    ImportNotionSeedPushOptions? NotionPushOptions = null,
    ImportNotionSchemaValidationOptions? SchemaValidationOptions = null);
