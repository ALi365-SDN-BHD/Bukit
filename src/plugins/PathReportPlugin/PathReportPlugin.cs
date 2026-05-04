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
        if (_ownsUploader)
        {
            _uploader.Dispose();
        }
    }

    public void AfterBuild(BuildContext context)
    {
        var rootDir = context.RootDir;
        var distDir = context.OutputDir;
        var cacheDir = Path.Combine(rootDir, ".cache");
        var themeName = context.Config.Theme.Name?.Trim();
        var themeRoot = string.IsNullOrWhiteSpace(themeName) ? rootDir : Path.Combine(rootDir, "themes", themeName);
        var assetsDir = ResolveAssetsDir(rootDir, context.Config.Theme, themeRoot, themeName);

        var pluginOptions = GetPluginOptions(context, Name);
        var uploadOptions = ReadMap(pluginOptions, "wechatMaterialUpload");
        var uploadEnabled = ReadBool(uploadOptions, "enabled") ?? false;
        WechatMaterialUploadResult? uploadResult = null;
        if (uploadEnabled)
        {
            uploadResult = UploadWechatMaterial(context, uploadOptions);
        }

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
            WechatMaterialUpload: uploadResult
        );

        var reportDir = Path.Combine(distDir, "_debug");
        Directory.CreateDirectory(reportDir);
        var reportPath = Path.Combine(reportDir, "paths-report.json");

        var json = JsonSerializer.Serialize(report, PathReportJsonContext.Default.PathReport);
        File.WriteAllText(reportPath, json);
        context.Logger.Info($"path-report wrote: {reportPath}");
    }

    private WechatMaterialUploadResult UploadWechatMaterial(BuildContext context, IReadOnlyDictionary<string, object> options)
    {
        var distDir = context.OutputDir;

        var relPath = ReadString(options, "file") ?? "assets/imgs/default.png";
        var relPathClean = relPath.Replace('\\', '/').TrimStart('/');
        var fullPath = Path.GetFullPath(Path.Combine(distDir, relPathClean.Replace('/', Path.DirectorySeparatorChar)));
        var safeDist = Path.GetFullPath(distDir) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(safeDist, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"path-report wechatMaterialUpload file escapes output directory: {relPath}");
        }

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"path-report wechatMaterialUpload file not found: {fullPath}");
        }

        var bytes = File.ReadAllBytes(fullPath);
        if (bytes.Length == 0)
        {
            throw new InvalidOperationException($"path-report wechatMaterialUpload file is empty: {fullPath}");
        }

        var type = (ReadString(options, "type") ?? "image").Trim();

        var wechat = ReadMap(options, "wechat");
        var appIdEnv = ReadString(wechat, "appIdEnv") ?? ReadString(options, "appIdEnv");
        var appSecretEnv = ReadString(wechat, "appSecretEnv") ?? ReadString(options, "appSecretEnv");
        if (string.IsNullOrWhiteSpace(appIdEnv) || string.IsNullOrWhiteSpace(appSecretEnv))
        {
            throw new InvalidOperationException("path-report wechatMaterialUpload requires appIdEnv and appSecretEnv.");
        }

        var appId = Environment.GetEnvironmentVariable(appIdEnv.Trim());
        var secret = Environment.GetEnvironmentVariable(appSecretEnv.Trim());
        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException($"path-report wechatMaterialUpload env missing: {appIdEnv}/{appSecretEnv}");
        }

        var fileName = Path.GetFileName(fullPath);
        var contentType = ResolveContentType(fileName);

        var resp = _uploader.UploadPermanentMaterialAsync(appId.Trim(), secret.Trim(), type, bytes, fileName, contentType, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        context.Logger.Info($"path-report wechatMaterialUpload ok type={type} media_id={resp.MediaId} url={resp.Url}");
        return new WechatMaterialUploadResult(relPathClean, type, resp.MediaId, resp.Url, resp.Raw);
    }

    private static string ResolveAssetsDir(string rootDir, ThemeConfig theme, string themeRoot, string? themeName)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            return MakeAbsolute(rootDir, theme.Assets);
        }

        if (string.Equals(theme.Assets, "assets", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(themeRoot, "assets");
        }

        return MakeAbsolute(rootDir, theme.Assets);
    }

    private static string MakeAbsolute(string rootDir, string relOrAbs)
    {
        if (string.IsNullOrWhiteSpace(relOrAbs))
        {
            return rootDir;
        }

        return Path.IsPathFullyQualified(relOrAbs)
            ? relOrAbs
            : Path.GetFullPath(Path.Combine(rootDir, relOrAbs));
    }

    private static IReadOnlyList<string> ListFiles(string dir, ILogger logger)
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
        catch (Exception ex)
        {
            logger.Warn($"path-report failed to list files for '{dir}': {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static IReadOnlyDictionary<string, object> GetPluginOptions(BuildContext context, string name)
    {
        if (context.Config.Site.Plugins is null)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        if (!context.Config.Site.Plugins.TryGetValue(name, out var cfg) || cfg.Options is null)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        return cfg.Options;
    }

    private static IReadOnlyDictionary<string, object> ReadMap(IReadOnlyDictionary<string, object>? map, string key)
    {
        if (map is null || string.IsNullOrWhiteSpace(key))
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        if (!map.TryGetValue(key, out var v) || v is null)
        {
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        if (v is IReadOnlyDictionary<string, object> ro)
        {
            return ro;
        }

        if (v is Dictionary<string, object> d)
        {
            return d;
        }

        return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadString(IReadOnlyDictionary<string, object>? map, string key)
    {
        if (map is null || string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (!map.TryGetValue(key, out var v) || v is null)
        {
            return null;
        }

        var s = v.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static bool? ReadBool(IReadOnlyDictionary<string, object>? map, string key)
    {
        var s = ReadString(map, key);
        if (s is null)
        {
            return null;
        }

        if (bool.TryParse(s, out var b))
        {
            return b;
        }

        return s == "1" ? true : s == "0" ? false : null;
    }

    private static string ResolveContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).Trim().ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream"
        };
    }
}

public sealed record PathReport(
    string RootDir,
    string CacheDir,
    string DistDir,
    string ThemeRoot,
    string LayoutsDir,
    string AssetsDir,
    DateTimeOffset GeneratedAtUtc,
    PathReportFiles Files,
    WechatMaterialUploadResult? WechatMaterialUpload);

public sealed record PathReportFiles(
    IReadOnlyList<string> Cache,
    IReadOnlyList<string> Dist,
    IReadOnlyList<string> Theme,
    IReadOnlyList<string> Assets);

public sealed record WechatMaterialUploadResult(
    string FilePath,
    string Type,
    string MediaId,
    string? Url,
    string Raw);
