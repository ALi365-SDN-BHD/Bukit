using Bukit.Notion.Security;
using Bukit.Plugin.Abstractions.Protocol;
using Bukit.Plugin.Abstractions.Results;

namespace Bukit.Plugin.Notion;

public sealed record NotionValidateSeedOptions(
    string ProjectRoot,
    string SeedDirectory);

public sealed record NotionValidateDatabaseMapOptions(
    string ProjectRoot,
    string DatabaseMapPath);

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

    private static PluginDiagnostic Error(string code, string message, string? path = null)
        => new(code, "error", message, path);
}
