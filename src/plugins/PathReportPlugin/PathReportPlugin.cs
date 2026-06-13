using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Engine.Abstractions.Plugins.Protocol;

namespace Bukit.Plugins.PathReportPlugin;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var plugin = new PathReportPlugin();
        await plugin.RunAsync();
        return 0;
    }
}

internal sealed class PathReportPlugin : ProcessPluginHost
{
    protected override string PluginName => "path-report";
    protected override string PluginVersion => "0.2.0";
    protected override IReadOnlyList<string> SupportedHooks => new[] { "after-build" };

    protected override async Task AfterBuildAsync(AfterBuildRequestPayload payload, IReadOnlyDictionary<string, object>? pluginOptions, CancellationToken ct)
    {
        var distDir = payload.OutputDir;
        var reportDir = Path.Combine(distDir, "_debug");
        Directory.CreateDirectory(reportDir);

        var files = new PathReportFiles(
            Dist: ListFiles(distDir)
        );

        var report = new PathReport(
            DistDir: distDir,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Files: files
        );

        var reportPath = Path.Combine(reportDir, "paths-report.json");
        var json = JsonSerializer.Serialize(report, PathReportJsonContext.Default.PathReport);
        await File.WriteAllTextAsync(reportPath, json, ct);
    }

    private static IReadOnlyList<string> ListFiles(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return Array.Empty<string>();
        }

        try
        {
            return Directory
                .EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(dir, path).Replace('\\', '/'))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

public sealed record PathReport(
    string DistDir,
    DateTimeOffset GeneratedAtUtc,
    PathReportFiles Files);

public sealed record PathReportFiles(
    IReadOnlyList<string> Dist);

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PathReport))]
[JsonSerializable(typeof(PathReportFiles))]
internal sealed partial class PathReportJsonContext : JsonSerializerContext
{
}
