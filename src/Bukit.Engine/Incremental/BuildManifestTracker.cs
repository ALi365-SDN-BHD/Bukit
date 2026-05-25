using System.Collections.Concurrent;
using Bukit.Engine.Output;
using Bukit.Engine.Plugins;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

internal static class BuildManifestTracker
{
    internal static void SyncMediaOutputs(string mediaDownloadDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger)
    {
        var mediaOutputDir = Path.Combine(outputDir, "assets", "uploads");
        DirectoryCopy.SyncFilesRecursive(mediaDownloadDir, mediaOutputDir, ignoreDotPrefixedFiles: true);

        var currentMedia = Directory.EnumerateFiles(mediaDownloadDir, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetFileName(file).StartsWith('.'))
            .Select(file =>
            {
                var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(mediaDownloadDir, file));
                var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", "uploads", relativePath));
                return new KeyValuePair<string, string>(outputPath, ComputeFileFingerprint(file));
            })
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        DeleteStaleTrackedFiles(outputDir, manifest.Media, currentMedia, incrementalEnabled, logger, "media");
        manifest.Media = currentMedia;
    }

    internal static void TrackPluginOutputs(BuildContext pluginContext, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger)
    {
        var currentOutputs = new Dictionary<string, PluginOutputManifestEntry>(StringComparer.Ordinal);
        if (pluginContext.Data.TryGetValue("__plugin_outputs", out var outputsObj) && outputsObj is HashSet<PluginOutputTrackingInfo> outputs)
        {
            foreach (var output in outputs)
            {
                var fullPath = FileWriter.GetSafeFullPath(outputDir, output.Path);
                if (File.Exists(fullPath))
                {
                    currentOutputs[BuildPathUtils.NormalizeRelPath(output.Path)] = new PluginOutputManifestEntry
                    {
                        Plugin = output.Plugin,
                        Hook = output.Hook,
                        Path = BuildPathUtils.NormalizeRelPath(output.Path),
                        Hash = ComputeFileFingerprint(fullPath)
                    };
                }
            }
        }

        DeleteStaleTrackedFiles(outputDir, manifest.PluginOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), currentOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), incrementalEnabled, logger, "plugin");
        manifest.PluginOutputs = currentOutputs;
    }

    internal static void TrackStaticOutputs(string? parentStaticDir, string? staticDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, bool renderHtmlStaticFiles)
    {
        var currentStatic = new Dictionary<string, string>(StringComparer.Ordinal);
        AddStaticSourceOutputs(parentStaticDir, currentStatic, renderHtmlStaticFiles: false);
        AddStaticSourceOutputs(staticDir, currentStatic, renderHtmlStaticFiles);

        DeleteStaleTrackedFiles(outputDir, manifest.Static, currentStatic, incrementalEnabled, logger, "static");
        manifest.Static = currentStatic;
    }

    internal static void TrackAssetOutputs(string? parentAssetsDir, string assetsDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger)
    {
        var currentAssets = new Dictionary<string, string>(StringComparer.Ordinal);
        AddAssetSourceOutputs(parentAssetsDir, currentAssets);
        AddAssetSourceOutputs(assetsDir, currentAssets);

        DeleteStaleTrackedFiles(outputDir, manifest.Assets, currentAssets, incrementalEnabled, logger, "asset");
        manifest.Assets = currentAssets;
    }

    internal static void DeleteStaleManifestOutputs(string outputDir, BuildManifest manifest, ConcurrentDictionary<string, byte> currentKeys, ILogger logger)
    {
        var removed = manifest.Entries
            .Where(kv => !currentKeys.ContainsKey(kv.Key))
            .ToList();

        foreach (var kv in removed)
        {
            var relativePath = string.IsNullOrWhiteSpace(kv.Value.OutputPath) ? kv.Key : kv.Value.OutputPath;
            try
            {
                var fullPath = FileWriter.GetSafeFullPath(outputDir, relativePath);
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

    private static void AddStaticSourceOutputs(string? sourceDir, Dictionary<string, string> outputs, bool renderHtmlStaticFiles)
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

            var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(sourceDir, file));
            outputs[relativePath] = ComputeFileFingerprint(file);
        }
    }

    private static void AddAssetSourceOutputs(string? sourceDir, Dictionary<string, string> outputs)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(sourceDir, file));
            var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", relativePath));
            outputs[outputPath] = ComputeFileFingerprint(file);
        }
    }

    private static void DeleteStaleTrackedFiles(
        string outputDir,
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current,
        bool incrementalEnabled,
        ILogger logger,
        string kind)
    {
        if (!incrementalEnabled)
        {
            return;
        }

        var outputFileSystem = new SafeOutputFileSystem(outputDir);
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

    private static string ComputeFileFingerprint(string file)
    {
        var info = new FileInfo(file);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
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
