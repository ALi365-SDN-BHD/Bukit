using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Engine.Plugins.Protocol;

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

// DESKTOP-REMOVED: Original inline [BukitPlugin] implementation (includes WechatMaterialUploader support).
#if false
using System.Text.Json;
using Bukit.Config;
using Bukit.Engine.Plugins;
using Bukit.Shared;

namespace Bukit.Plugins.PathReportPlugin;

[BukitPlugin]
public sealed class PathReportPlugin : IBukitPlugin, IAfterBuildPlugin, IOrderedPlugin, IDisposable
{
    private readonly WechatMaterialUploader _uploader;
    private readonly bool _ownsUploader;

    public string Name => "path-report";
    public string Version => "0.1.0";
    public int Order => int.MaxValue;

    public PathReportPlugin()
        : this(new WechatMaterialUploader(), ownsUploader: true)
    {
    }

    public PathReportPlugin(WechatMaterialUploader uploader)
        : this(uploader, ownsUploader: false)
    {
    }

    private PathReportPlugin(WechatMaterialUploader uploader, bool ownsUploader)
    {
        _uploader = uploader;
        _ownsUploader = ownsUploader;
    }

    public void Dispose()
    {
        if (_ownsUploader) _uploader.Dispose();
    }

    public void AfterBuild(BuildContext context)
    {
        var rootDir = context.RootDir;
        var distDir = context.OutputDir;
        var cacheDir = Path.Combine(rootDir, ".cache");
        var themeName = context.Config.Theme.Name?.Trim();
        var themeRoot = string.IsNullOrWhiteSpace(themeName) ? rootDir : Path.Combine(rootDir, "themes", themeName);
        var assetsDir = ResolveAssetsDir(rootDir, context.Config.Theme, themeRoot, themeName);

        var report = new PathReport(
            RootDir: rootDir,
            CacheDir: cacheDir,
            DistDir: distDir,
            ThemeRoot: themeRoot,
            LayoutsDir: context.LayoutsDir,
            AssetsDir: assetsDir,
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Files: new PathReportFiles(
                Cache: ListFiles(cacheDir, context.Logger),
                Dist: ListFiles(distDir, context.Logger),
                Theme: ListFiles(themeRoot, context.Logger),
                Assets: ListFiles(assetsDir, context.Logger)
            ),
            WechatMaterialUpload: null
        );

        var reportDir = Path.Combine(distDir, "_debug");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "paths-report.json");

        var json = JsonSerializer.Serialize(report, PathReportJsonContext.Default.PathReport);
        File.WriteAllText(reportPath, json);
        context.Logger.Info($"path-report wrote: {reportPath}");
    }
}
#endif
