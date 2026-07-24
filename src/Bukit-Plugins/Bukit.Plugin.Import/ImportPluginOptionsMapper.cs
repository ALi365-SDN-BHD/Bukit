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
        var sitePath = ImportPluginPathGuard.ResolveOptionalPath(rootDir, rootDir, ReadString(request, "--site-path"), "--site-path");
        var routeMapPath = ImportPluginPathGuard.ResolveOptionalPath(rootDir, demoDir ?? workingDir, ReadString(request, "--route-map"), "--route-map");
        var tokenEnv = ReadString(request, "--notion-token-env") ?? "NOTION_TOKEN";
        var pushNotion = ReadBool(request, "--push-notion");
        if (pushNotion)
            EnsureEnvironmentGranted(request, tokenEnv);

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
            Use = ReadBool(request, "--use"),
            Verify = ReadBool(request, "--verify"),
            ExtractContent = !ReadBool(request, "--no-extract-content"),
            GenerateSeed = !ReadBool(request, "--no-seed"),
            ContentSource = ReadString(request, "--content-source") ?? "notion",
            BuildSource = ReadString(request, "--build-source") ?? "markdown",
            SitePath = sitePath,
            Language = ReadString(request, "--language") ?? "zh",
            DryRun = ReadBool(request, "--dry-run"),
            StrictMode = ResolveStrictMode(ReadString(request, "--strict")),
            Overwrite = ReadBool(request, "--overwrite"),
            PreserveHtml = !ReadBool(request, "--no-preserve-html"),
            GenerateReport = !ReadBool(request, "--no-report"),
            BaseUrl = ReadString(request, "--base-url"),
            RouteMapPath = routeMapPath,
            PushNotion = pushNotion,
            NotionDatabaseId = ReadString(request, "--notion-database-id"),
            NotionDatabaseMap = ImportPluginPathGuard.PreserveSafeRelativeOrRootedPath(
                rootDir,
                ReadString(request, "--notion-database-map"),
                "--notion-database-map"),
            CreateMissingNotionDatabases = ReadBool(request, "--create-missing-notion-databases"),
            NotionParentPageId = ReadString(request, "--notion-parent-page-id"),
            NotionGeneratedDatabaseMap = ImportPluginPathGuard.PreserveSafeRelativeOrRootedPath(
                rootDir,
                ReadString(request, "--notion-generated-database-map"),
                "--notion-generated-database-map"),
            NotionTokenEnv = tokenEnv,
            NotionReport = ImportPluginPathGuard.PreserveSafeRelativeOrRootedPath(
                rootDir,
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
            OutputDir = ImportPluginPathGuard.ResolveOptionalPath(rootDir, workingDir, ReadString(request, "--output"), "--output"),
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

        return new ImportNotionSeedPushOptions
        {
            InputDir = ImportPluginPathGuard.ResolveOptionalPath(rootDir, workingDir, ReadString(request, "--input"), "--input") ?? "",
            DatabaseId = ReadString(request, "--database-id"),
            DatabaseMapPath = ImportPluginPathGuard.ResolveOptionalPath(rootDir, workingDir, ReadString(request, "--database-map"), "--database-map"),
            CreateMissingDatabases = ReadBool(request, "--create-missing-databases"),
            ParentPageId = ReadString(request, "--parent-page-id"),
            GeneratedDatabaseMapPath = ImportPluginPathGuard.ResolveOptionalPath(
                rootDir,
                workingDir,
                ReadString(request, "--generated-database-map"),
                "--generated-database-map"),
            TokenEnv = tokenEnv,
            Mode = ReadString(request, "--mode") ?? "create",
            UniqueField = ReadString(request, "--unique-field") ?? "Slug",
            UpdateContent = ReadString(request, "--update-content") ?? "",
            DryRun = dryRun,
            ReportPath = ImportPluginPathGuard.ResolveOptionalPath(rootDir, workingDir, ReadString(request, "--report"), "--report"),
            ValidateSchema = !ReadBool(request, "--no-validate-schema")
        };
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
            ReportPath = ImportPluginPathGuard.ResolveOptionalPath(rootDir, workingDir, ReadString(request, "--report"), "--report")
        };
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
