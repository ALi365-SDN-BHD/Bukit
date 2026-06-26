using Bukit.Notion.Security;
using Bukit.Notion.Push;
using Bukit.Notion;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Notion;

public sealed record NotionValidateSeedOptions(
    string ProjectRoot,
    string SeedDirectory);

public sealed record NotionValidateDatabaseMapOptions(
    string ProjectRoot,
    string DatabaseMapPath);

public sealed record NotionPushMapperOptions(
    NotionPushOptions PushOptions);

public sealed record NotionValidateSeedMapperResult(
    bool Success,
    NotionValidateSeedOptions? Options,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed record NotionValidateDatabaseMapMapperResult(
    bool Success,
    NotionValidateDatabaseMapOptions? Options,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public sealed record NotionPushMapperResult(
    bool Success,
    NotionPushMapperOptions? Options,
    IReadOnlyList<PluginDiagnostic>? Diagnostics = null)
{
    public IReadOnlyList<PluginDiagnostic> Diagnostics { get; init; } = Diagnostics ?? [];
}

public static class NotionOptionsMapper
{
    public static NotionValidateSeedMapperResult MapValidateSeedOptions(PluginInvokeRequest request)
    {
        var diagnostics = new List<PluginDiagnostic>();
        if (!request.Command.Path.SequenceEqual(["notion", "validate-seed"], StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "plugin.notion.unsupportedCommand",
                "Notion plugin only supports the notion validate-seed command for this path."));
        }

        string? seedDirectory = request.Command.Arguments.Count > 0
            ? request.Command.Arguments[0]
            : null;
        if (string.IsNullOrWhiteSpace(seedDirectory))
        {
            diagnostics.Add(Error("notion.seedDirMissing", "Missing required argument: <seed-dir>."));
        }

        if (diagnostics.Count > 0)
        {
            return new NotionValidateSeedMapperResult(false, null, diagnostics);
        }

        string root = request.Context.RootDir;
        return new NotionValidateSeedMapperResult(
            true,
            new NotionValidateSeedOptions(
                ProjectRoot: root,
                SeedDirectory: NotionPathGuard.ResolvePath(root, seedDirectory!)),
            []);
    }

    public static NotionValidateDatabaseMapMapperResult MapValidateDatabaseMapOptions(PluginInvokeRequest request)
    {
        var diagnostics = new List<PluginDiagnostic>();
        if (!request.Command.Path.SequenceEqual(["notion", "validate-database-map"], StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "plugin.notion.unsupportedCommand",
                "Notion plugin only supports the notion validate-database-map command for this path."));
        }

        string? databaseMapPath = request.Command.Arguments.Count > 0
            ? request.Command.Arguments[0]
            : null;
        if (string.IsNullOrWhiteSpace(databaseMapPath))
        {
            diagnostics.Add(Error("notion.databaseMapMissingPath", "Missing required argument: <database-map>."));
        }

        if (diagnostics.Count > 0)
        {
            return new NotionValidateDatabaseMapMapperResult(false, null, diagnostics);
        }

        string root = request.Context.RootDir;
        return new NotionValidateDatabaseMapMapperResult(
            true,
            new NotionValidateDatabaseMapOptions(
                ProjectRoot: root,
                DatabaseMapPath: NotionPathGuard.ResolvePath(root, databaseMapPath!)),
            []);
    }

    public static NotionPushMapperResult MapPushOptions(PluginInvokeRequest request)
    {
        var diagnostics = new List<PluginDiagnostic>();
        if (!request.Command.Path.SequenceEqual(["notion", "push"], StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "plugin.notion.unsupportedCommand",
                "Notion plugin only supports the notion push command for this path."));
        }

        string? seedDirectory = ReadRequiredStringOption(request, "--seed", "notion.pushMissingSeed", diagnostics);
        string? databaseMapPath = ReadRequiredStringOption(request, "--database-map", "notion.pushMissingDatabaseMap", diagnostics);
        string? modeValue = ReadRequiredStringOption(request, "--mode", "notion.pushMissingMode", diagnostics);
        bool dryRun = ReadDryRun(request, diagnostics);
        bool confirmReplace = ReadBooleanFlag(request, "--confirm-replace", diagnostics);
        string tokenEnvironmentVariable = NotionPluginConstants.TokenEnvironmentVariable;

        if (request.Command.Options.TryGetValue("--token-env", out var tokenEnvElement))
        {
            if (tokenEnvElement.ValueKind != System.Text.Json.JsonValueKind.String
                || string.IsNullOrWhiteSpace(tokenEnvElement.GetString()))
            {
                diagnostics.Add(Error("notion.tokenEnvInvalid", "--token-env must be a non-empty JSON string."));
            }
            else if (!NotionPluginConstants.IsAllowedTokenEnvironmentVariable(tokenEnvElement.GetString()!))
            {
                diagnostics.Add(Error("notion.tokenEnvNotAllowed", "--token-env must name an allowlisted environment variable."));
            }
            else
            {
                tokenEnvironmentVariable = tokenEnvElement.GetString()!;
            }
        }

        if (!TryParseMode(modeValue, out NotionPushMode mode))
        {
            diagnostics.Add(Error("notion.pushInvalidMode", "--mode must be create, upsert, or replace."));
        }

        string root = request.Context.RootDir;
        string reportPath = ReadOptionalStringOption(request, "--report", diagnostics)
            ?? Path.Combine(root, ".bukit", "reports", "plugin-output", "notion", "notion-push-report.json");
        string resolvedReportPath = NotionPathGuard.ResolvePath(root, reportPath);
        if (!IsAllowedReportPath(root, resolvedReportPath))
        {
            diagnostics.Add(Error(
                "notion.reportPathOutsideAllowedOutput",
                "Report path must stay under .bukit/reports/plugin-output/notion or .bukit/tmp/notion.",
                resolvedReportPath));
        }

        if (diagnostics.Count > 0)
        {
            return new NotionPushMapperResult(false, null, diagnostics);
        }

        return new NotionPushMapperResult(
            true,
            new NotionPushMapperOptions(new NotionPushOptions(
                ProjectRoot: root,
                SeedDirectory: NotionPathGuard.ResolvePath(root, seedDirectory!),
                DatabaseMapPath: NotionPathGuard.ResolvePath(root, databaseMapPath!),
                Mode: mode,
                DryRun: dryRun,
                ReportPath: resolvedReportPath,
                TokenEnvironmentVariable: tokenEnvironmentVariable,
                ConfirmReplace: confirmReplace)),
            []);
    }

    private static PluginDiagnostic Error(string code, string message, string? path = null)
        => new(code, "error", message, path);

    private static string? ReadRequiredStringOption(
        PluginInvokeRequest request,
        string optionName,
        string missingCode,
        List<PluginDiagnostic> diagnostics)
    {
        string? value = ReadOptionalStringOption(request, optionName, diagnostics);
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Error(missingCode, $"Missing required option: {optionName}."));
        }

        return value;
    }

    private static string? ReadOptionalStringOption(
        PluginInvokeRequest request,
        string optionName,
        List<PluginDiagnostic> diagnostics)
    {
        if (!request.Command.Options.TryGetValue(optionName, out var element))
        {
            return null;
        }

        if (element.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            diagnostics.Add(Error("notion.pushInvalidOption", $"{optionName} must be a non-empty JSON string."));
            return null;
        }

        return element.GetString();
    }

    private static bool ReadDryRun(PluginInvokeRequest request, List<PluginDiagnostic> diagnostics)
        => ReadBooleanFlag(request, "--dry-run", diagnostics);

    private static bool ReadBooleanFlag(PluginInvokeRequest request, string optionName, List<PluginDiagnostic> diagnostics)
    {
        if (!request.Command.Options.TryGetValue(optionName, out var element))
        {
            return false;
        }

        if (element.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
        {
            return element.GetBoolean();
        }

        diagnostics.Add(Error("notion.pushInvalidOption", $"{optionName} must be a JSON boolean."));
        return false;
    }

    private static bool TryParseMode(string? value, out NotionPushMode mode)
    {
        mode = NotionPushMode.Create;
        return value switch
        {
            "create" => true,
            "upsert" => SetMode(NotionPushMode.Upsert, out mode),
            "replace" => SetMode(NotionPushMode.Replace, out mode),
            _ => false
        };

        static bool SetMode(NotionPushMode value, out NotionPushMode mode)
        {
            mode = value;
            return true;
        }
    }

    private static bool IsAllowedReportPath(string root, string reportPath)
        => NotionPathGuard.IsWithinAnyRoot(
            reportPath,
            Path.Combine(root, ".bukit", "reports", "plugin-output", "notion"),
            Path.Combine(root, ".bukit", "tmp", "notion"));
}
