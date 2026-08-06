using System.Buffers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Bukit.Engine.Output;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

internal static class BuildManifestTracker
{
    internal static void PrepareAssetPlanOutputs(
        IReadOnlyList<AssetOutputItem> items,
        string outputDir,
        StringComparer pathComparer,
        BuildManifest manifest,
        bool incrementalEnabled,
        CancellationToken cancellationToken,
        ConcurrentDictionary<string, BuildManifestEntry>? manifestEntries = null,
        IOutputPathPolicy? pathPolicy = null)
    {
        if (!incrementalEnabled)
        {
            return;
        }

        var currentExactDestinations = items
            .Select(item => item.Destination)
            .ToHashSet(StringComparer.Ordinal);
        var currentIdentityDestinations = items
            .Select(item => item.Destination)
            .ToHashSet(pathComparer);
        var currentRenderDestinations = items
            .Where(item => item.Category == AssetOutputCategory.Render)
            .Select(item => item.Destination)
            .ToHashSet(pathComparer);
        var currentNonRenderDestinations = items
            .Where(item => item.Category != AssetOutputCategory.Render)
            .Select(item => item.Destination)
            .ToHashSet(pathComparer);
        var blockingStalePaths = manifest.Static.Keys
            .Concat(manifest.Assets.Keys)
            .Concat(manifest.Media.Keys)
            .Distinct(StringComparer.Ordinal)
            .Where(stale => !currentExactDestinations.Contains(stale))
            .Where(stale => currentIdentityDestinations.Contains(stale) ||
                            currentExactDestinations.Any(current => StructurallyConflicts(stale, current, pathComparer)))
            .OrderByDescending(path => path.Length)
            .ThenBy(path => path, pathComparer)
            .ToArray();
        var blockingStaleRenderEntries = manifest.Entries
            .Select(entry => new
            {
                entry.Key,
                OutputPath = BuildPathUtils.NormalizeRelPath(
                    string.IsNullOrWhiteSpace(entry.Value.OutputPath) ? entry.Key : entry.Value.OutputPath)
            })
            .Where(entry => !currentRenderDestinations.Contains(entry.OutputPath))
            .Where(entry => currentNonRenderDestinations.Contains(entry.OutputPath) ||
                            currentNonRenderDestinations.Any(current => StructurallyConflicts(entry.OutputPath, current, pathComparer)))
            .ToArray();
        var pathsToDelete = blockingStalePaths
            .Concat(blockingStaleRenderEntries.Select(entry => entry.OutputPath))
            .Distinct(pathComparer)
            .OrderByDescending(path => path.Length)
            .ThenBy(path => path, pathComparer)
            .ToArray();

        var outputFileSystem = new SafeOutputFileSystem(outputDir, pathPolicy);
        foreach (var stale in pathsToDelete)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = outputFileSystem.GetSafeFullPath(stale);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                DeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(fullPath), outputDir);
            }
        }

        foreach (var staleEntry in blockingStaleRenderEntries)
        {
            manifest.Entries.Remove(staleEntry.Key);
            manifestEntries?.TryRemove(staleEntry.Key, out _);
        }
    }

    internal static void TrackAssetPlanOutputs(
        IReadOnlyList<AssetOutputItem> items,
        string outputDir,
        StringComparer pathComparer,
        BuildManifest manifest,
        bool incrementalEnabled,
        ILogger logger,
        string? fingerprintMode = null,
        IOutputPathPolicy? pathPolicy = null,
        CancellationToken cancellationToken = default)
    {
        var currentStatic = CreateTrackedOutputs(items, outputDir, AssetOutputCategory.Static, fingerprintMode, cancellationToken);
        var currentAssets = CreateTrackedOutputs(
            items,
            outputDir,
            AssetOutputCategory.Assets,
            fingerprintMode,
            cancellationToken,
            includeTokens: true);
        var currentMedia = CreateTrackedOutputs(items, outputDir, AssetOutputCategory.Media, fingerprintMode, cancellationToken);
        var currentDestinations = items
            .Select(item => item.Destination)
            .ToHashSet(pathComparer);

        DeleteStaleTrackedFiles(
            outputDir, manifest.Static, currentStatic, incrementalEnabled, logger, "static", pathPolicy, currentDestinations);
        DeleteStaleTrackedFiles(
            outputDir, manifest.Assets, currentAssets, incrementalEnabled, logger, "asset", pathPolicy, currentDestinations);
        DeleteStaleTrackedFiles(
            outputDir, manifest.Media, currentMedia, incrementalEnabled, logger, "media", pathPolicy, currentDestinations);

        manifest.Static = currentStatic;
        manifest.Assets = currentAssets;
        manifest.Media = currentMedia;
    }

    internal static void SyncMediaOutputs(
        string mediaDownloadDir,
        string outputDir,
        BuildManifest manifest,
        bool incrementalEnabled,
        ILogger logger,
        string? fingerprintMode = null,
        IOutputPathPolicy? pathPolicy = null,
        Bukit.Shared.IO.ISafeSourceFileOpener? opener = null,
        CancellationToken cancellationToken = default)
    {
        var mediaOutputDir = Path.Combine(outputDir, "assets", "uploads");
        var copyOptions = new DirectoryCopyOptions
        {
            HashMode = fingerprintMode ?? "size-time",
            IgnoreDotPrefixedFiles = true,
            FollowSymlinks = false
        };
        var currentMedia = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in DirectoryCopy.EnumerateFilesForSync(mediaDownloadDir, copyOptions, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = BuildPathUtils.NormalizeRelPath(item.RelativePath);
            var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", "uploads", relativePath));
            var destinationFile = Path.Combine(
                mediaOutputDir,
                item.RelativePath);
            DirectoryCopy.SyncPlannedFile(
                item.SourcePath,
                destinationFile,
                copyOptions.HashMode,
                outputDir,
                item.PhysicalSourceRoot,
                copyOptions,
                pathPolicy,
                opener);
            currentMedia[outputPath] = ComputeFileFingerprint(destinationFile, fingerprintMode, cancellationToken);
        }

        DeleteStaleTrackedFiles(outputDir, manifest.Media, currentMedia, incrementalEnabled, logger, "media", pathPolicy);
        manifest.Media = currentMedia;
    }

    internal static void TrackPluginOutputs(BuildContext pluginContext, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, string? fingerprintMode = null, IOutputPathPolicy? pathPolicy = null, CancellationToken cancellationToken = default)
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
                        Hash = ComputeFileFingerprint(fullPath, fingerprintMode, cancellationToken)
                    };
                }
            }
        }

        DeleteStaleTrackedFiles(outputDir, manifest.PluginOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), currentOutputs.ToDictionary(x => x.Key, x => x.Value.Hash, StringComparer.Ordinal), incrementalEnabled, logger, "plugin", pathPolicy);
        manifest.PluginOutputs = currentOutputs;
    }

    internal static void TrackStaticOutputs(string? parentStaticDir, string? staticDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, bool renderHtmlStaticFiles, string? fingerprintMode = null, IOutputPathPolicy? pathPolicy = null, CancellationToken cancellationToken = default)
    {
        var currentStatic = new Dictionary<string, string>(StringComparer.Ordinal);
        AddStaticSourceOutputs(parentStaticDir, currentStatic, renderHtmlStaticFiles: false, fingerprintMode, cancellationToken);
        AddStaticSourceOutputs(staticDir, currentStatic, renderHtmlStaticFiles, fingerprintMode, cancellationToken);

        DeleteStaleTrackedFiles(outputDir, manifest.Static, currentStatic, incrementalEnabled, logger, "static", pathPolicy);
        manifest.Static = currentStatic;
    }

    internal static void TrackAssetOutputs(string? parentAssetsDir, string? assetsDir, string outputDir, BuildManifest manifest, bool incrementalEnabled, ILogger logger, string? fingerprintMode = null, IOutputPathPolicy? pathPolicy = null, CancellationToken cancellationToken = default)
    {
        var currentAssets = new Dictionary<string, string>(StringComparer.Ordinal);
        AddAssetSourceOutputs(parentAssetsDir, currentAssets, fingerprintMode, cancellationToken);
        AddAssetSourceOutputs(assetsDir, currentAssets, fingerprintMode, cancellationToken);

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

    private static void AddStaticSourceOutputs(string? sourceDir, Dictionary<string, string> outputs, bool renderHtmlStaticFiles, string? fingerprintMode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in SafeFileEnumerator.EnumerateFiles(sourceDir))
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
            outputs[relativePath] = ComputeFileFingerprint(file, fingerprintMode, cancellationToken);
        }
    }

    private static void AddAssetSourceOutputs(string? sourceDir, Dictionary<string, string> outputs, string? fingerprintMode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceDir) || !Directory.Exists(sourceDir))
        {
            return;
        }

        foreach (var file in SafeFileEnumerator.EnumerateFiles(sourceDir))
        {
            if (IsSymlink(file))
            {
                continue;
            }

            var relativePath = BuildPathUtils.NormalizeRelPath(Path.GetRelativePath(sourceDir, file));
            var outputPath = BuildPathUtils.NormalizeRelPath(Path.Combine("assets", relativePath));
            outputs[outputPath] = ComputeFileFingerprint(file, fingerprintMode, cancellationToken);
        }
    }

    private static Dictionary<string, string> CreateTrackedOutputs(
        IReadOnlyList<AssetOutputItem> items,
        string outputDir,
        AssetOutputCategory category,
        string? fingerprintMode,
        CancellationToken cancellationToken,
        bool includeTokens = false)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in items.Where(item =>
                     item.Category == category || includeTokens && item.Category == AssetOutputCategory.Tokens))
        {
            var outputPath = Path.Combine(
                outputDir,
                item.Destination.Replace('/', Path.DirectorySeparatorChar));
            outputs[item.Destination] = ComputeFileFingerprint(outputPath, fingerprintMode, cancellationToken);
        }

        return outputs;
    }

    private static void DeleteStaleTrackedFiles(
        string outputDir,
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current,
        bool incrementalEnabled,
        ILogger logger,
        string kind,
        IOutputPathPolicy? pathPolicy = null,
        IReadOnlySet<string>? currentDestinations = null)
    {
        if (!incrementalEnabled)
        {
            return;
        }

        var outputFileSystem = new SafeOutputFileSystem(outputDir, pathPolicy);
        foreach (var stale in previous.Keys
                     .Where(key => !current.ContainsKey(key) && currentDestinations?.Contains(key) != true)
                     .ToList())
        {
            try
            {
                var fullPath = outputFileSystem.GetSafeFullPath(stale);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    DeleteEmptyDirectoriesUpToRoot(Path.GetDirectoryName(fullPath), outputDir);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                logger.Warn($"Failed to delete stale {kind} output '{stale}': {ex.Message}");
            }
        }
    }

    private static string ComputeFileFingerprint(string file, string? fingerprintMode = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var mode = (fingerprintMode ?? "size-time").Trim().ToLowerInvariant();

        if (mode == "sha256")
        {
            using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.SequentialScan);
            return ComputeSha256(stream, cancellationToken);
        }

        var info = new FileInfo(file);
        return $"{info.Length}:{info.LastWriteTimeUtc.Ticks}";
    }

    internal static string ComputeSha256(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = stream.Read(buffer.AsSpan());
                if (read == 0)
                {
                    break;
                }

                incrementalHash.AppendData(buffer.AsSpan(0, read));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Convert.ToHexString(incrementalHash.GetHashAndReset()).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
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

    private static bool StructurallyConflicts(string left, string right, StringComparer comparer)
        => IsDescendant(left, right, comparer) || IsDescendant(right, left, comparer);

    private static bool IsDescendant(string candidate, string ancestor, StringComparer comparer)
        => candidate.Length > ancestor.Length &&
           candidate[ancestor.Length] == '/' &&
           comparer.Equals(candidate[..ancestor.Length], ancestor);

    private static void DeleteEmptyDirectoriesUpToRoot(string? directory, string outputDir)
    {
        var root = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullDirectory, root, PlatformPathHelper.PathComparison))
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
