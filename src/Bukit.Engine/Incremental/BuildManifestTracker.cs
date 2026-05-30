using System.Collections.Concurrent;
using System.Security.Cryptography;
using Bukit.Engine.Output;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

internal static class BuildManifestTracker
{
    internal static void SyncMediaOutputs(string mediaDownloadDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, string? fingerprintMode = null, IOutputPathPolicy? pathPolicy = null)
    {
        var mediaOutputDir = Path.Combine(outputDir, "assets", "uploads");
        DirectoryCopy.SyncFilesRecursive(mediaDownloadDir, mediaOutputDir, ignoreDotPrefixedFiles: true, outputRoot: outputDir, pathPolicy: pathPolicy);

        var currentMedia = Directory.EnumerateFiles(mediaDownloadDir, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).StartsWith('.') && !IsSymlink(file))
            .Select(file =>
            {
                var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(mediaDownloadDir, file));
                var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", "uploads", relativePath));
                return new KeyValuePair<string, string>(outputPath, ComputeFileFingerprint(file, fingerprintMode));
            })
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        DeleteStaleTrackedFiles(outputDir, manifest.Media, currentMedia, incrementalEnabled, logger, "media", pathPolicy);
        manifest.Media = currentMedia;
    }

    internal static void TrackPluginOutputs(BuildContext pluginContext, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, string? fingerprintMode = null, IOutputPathPolicy? pathPolicy = null)
    {
        var currentOutputs = new Dictionary<string, PluginOutputManifestEntry>(StringComparer.Ordinal);
        if (pluginContext.Data.TryGetValue("__plugin_outputs", out var outputsObj) && outputsObj is HashSet<PluginOutputTrackingInfo> outputs)
        {
            foreach (var output in outputs)
            {
                var fullPath = FileWriter.GetSafeFullPath(outputDir, output.Path, pathPolicy);
                if (File.Exists(fullPath))
                {
                    currentOutputs[BuildPathUtils.NormalizeRelPath(output.Path)] = new PluginOutputManifestEntry
                    {
                        Plugin = output.Plugin,
                        Hook = output.Hook,
                        Path = BuildPathUtils.NormalizeRelPath(output.Path),
                        Hash = ComputeFileFingerprint(fullPath, fingerprintMode)
                    };
                }
            }
        }

        DeleteStaleTrackedFiles(outputDir, manifest.PluginOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), currentOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), incrementalEnabled, logger, "plugin", pathPolicy);
        manifest.PluginOutputs = currentOutputs;
    }

    internal static void TrackStaticOutputs(string? parentStaticDir, string? staticDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, bool renderHtmlStaticFiles, string? fingerprintMode = null, IOutputPathPolicy? pathPolicy = null)
    {
        var currentStatic = new Dictionary<string, string>(StringComparer.Ordinal);
        AddStaticSourceOutputs(parentStaticDir, currentStatic, renderHtmlStaticFiles: false, fingerprintMode);
        AddStaticSourceOutputs(staticDir, currentStatic, renderHtmlStaticFiles, fingerprintMode);

        DeleteStaleTrackedFiles(outputDir, manifest.Static, currentStatic, incrementalEnabled, logger, "static", pathPolicy);
        manifest.Static = currentStatic;
    }

    internal static void TrackAssetOutputs(string? parentAssetsDir, string? assetsDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, string? fingerprintMode = null, IOutputPathPolicy? pathPolicy = null)
    {
        var currentAssets = new Dictionary<string, string>(StringComparer.Ordinal);
        AddAssetSourceOutputs(parentAssetsDir, currentAssets, fingerprintMode);
        AddAssetSourceOutputs(assetsDir, currentAssets, fingerprintMode);

        DeleteStaleTrackedFiles(outputDir, manifest.Assets, currentAssets, incrementalEnabled, logger, "asset", pathPolicy);
        manifest.Assets = currentAssets;
    }

    internal static void DeleteStaleManifestOutputs(string outputDir, BuildManifest manifest, ConcurrentDictionary<string, byte> currentKeys, ILogger logger, IOutputPathPolicy? pathPolicy = null)
    {
        var removed = manifest.Entries
            .Where(kv => !currentKeys.ContainsKey(kv.Key))
            .ToList();

        foreach (var kv in removed)
        {
            var relativePath = string.IsNullOrWhiteSpace(kv.Value.OutputPath) ? kv.Key : kv.Value.OutputPath;
            try
            {
                var fullPath = FileWriter.GetSafeFullPath(outputDir, relativePath, pathPolicy);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    DeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(fullPath), outputDir);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.Warn($"Failed to delete stale output '{relativePath}': {ex.Message}");
            }

            manifest.Entries.Remove(kv.Key);
        }
    }

    private static void AddStaticSourceOutputs(string? sourceDir, Dictionary<string, string> outputs, bool renderHtmlStaticFiles, string? fingerprintMode = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (renderHtmlStaticFiles && string.Equals(Path.GetExtension(file), ".html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsSymlink(file))
            {
                continue;
            }

            var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(sourceDir, file));
            outputs[relativePath] = ComputeFileFingerprint(file, fingerprintMode);
        }
    }

    private static void AddAssetSourceOutputs(string? sourceDir, Dictionary<string, string> outputs, string? fingerprintMode = null)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            if (IsSymlink(file))
            {
                continue;
            }

            var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(sourceDir, file));
            var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", relativePath));
            outputs[outputPath] = ComputeFileFingerprint(file, fingerprintMode);
        }
    }

    private static void DeleteStaleTrackedFiles(
        string outputDir,
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current,
        bool incrementalEnabled,
        ILogger logger,
        string kind,
        IOutputPathPolicy? pathPolicy = null)
    {
        if (!incrementalEnabled)
        {
            return;
        }

        var outputFileSystem = new SafeOutputFileSystem(outputDir, pathPolicy);
        foreach (var stale in previous.Keys.Where(key => !current.ContainsKey(key)).ToList())
        {
            try
            {
                var fullPath = outputFileSystem.GetSafeFullPath(stale);
                if (File.Exists(fullPath))
                {
                    outputFileSystem.DeleteFileAsync(stale, CancellationToken.None).GetAwaiter().GetResult();
                    DeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(fullPath), outputDir);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.Warn($"Failed to delete stale {kind} output '{stale}': {ex.Message}");
            }
        }
    }

    private static string ComputeFileFingerprint(string file, string? fingerprintMode = null)
    {
        var mode = (fingerprintMode ?? "size-time").Trim().ToLowerInvariant();

        if (mode == "sha256")
        {
            var bytes = File.ReadAllBytes(file);
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        var info = new FileInfo(file);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    private static bool IsSymlink(string path)
    {
        try
        {
            var attr = File.GetAttributes(path);
            return (attr & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteEmptyDirectoriesUpToRoot(string? directory, string outputDir)
    {
        var root = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullDirectory, root, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!Directory.Exists(fullDirectory) || Directory.EnumerateFileSystemEntries(fullDirectory).Any())
            {
                break;
            }

            Directory.Delete(fullDirectory);
            directory = Path.GetDirectoryName(fullDirectory);
        }
    }
}
