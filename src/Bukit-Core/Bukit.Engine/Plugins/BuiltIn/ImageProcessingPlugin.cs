using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bukit.Config;
using Bukit.Content.Media;
using Bukit.Shared;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed partial class ImageProcessingPlugin : IBukitPlugin, IAfterBuildAsyncPlugin
{
    private readonly AppConfig _config;
    private readonly ImageContentValidator _imageValidator = new();

    internal ImageProcessingPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "image-processing";
    public string Version => "1.0.0";

    public async Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        var config = _config.Theme.Images;
        if (config is not { Enabled: true })
        {
            return;
        }

        var assetsDir = Path.Combine(context.OutputDir, "assets");
        if (!Directory.Exists(assetsDir))
        {
            return;
        }

        var priorPluginOutputs = GetPriorPluginOutputs(context);
        CleanupOrphanedOwnedVariants(
            context.OutputDir,
            assetsDir,
            priorPluginOutputs,
            cancellationToken);

        var exts = new[] { ".jpg", ".jpeg", ".png" };
        var sizes = config.Sizes ?? new[] { 480, 768, 1200 };
        var imageFiles = SafeFileEnumerator.EnumerateFiles(assetsDir, "*.*")
            .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !IsGeneratedSizedImage(f))
            .ToList();

        if (imageFiles.Count == 0)
        {
            return;
        }

        var quality = config.Quality > 0 ? config.Quality : 80;
        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CleanupStaleVariants(
                context.OutputDir,
                imageFile,
                sizes,
                priorPluginOutputs);
        }

        var tool = await FindResizeToolAsync(context.Logger, cancellationToken);
        if (tool is null)
        {
            context.Logger.Warn("event=image_processing.skip reason=no_tool message=Install ImageMagick (magick) for image resizing.");
            return;
        }

        var generatedOutputs = new HashSet<PluginOutputTrackingInfo>();

        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceInfo = new FileInfo(imageFile);
            var sourceSha256 = await ComputeSha256Async(imageFile, cancellationToken);

            foreach (var size in sizes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var baseName = Path.GetFileNameWithoutExtension(imageFile);
                var ext = Path.GetExtension(imageFile);
                var sizedFile = Path.Combine(Path.GetDirectoryName(imageFile)!, $"{baseName}-{size}w{ext}");
                var freshnessFile = sizedFile + FreshnessSuffix;
                var variantExists = File.Exists(sizedFile);
                var sidecarExists = File.Exists(freshnessFile);
                var hasPriorOwnership = HasPriorOwnership(
                    context.OutputDir,
                    priorPluginOutputs,
                    sizedFile,
                    freshnessFile);
                var hasValidFreshness = TryReadFreshness(
                    freshnessFile,
                    context.OutputDir,
                    imageFile,
                    sizedFile,
                    size,
                    out var existingFreshness);
                var hasOwnedFreshness = hasPriorOwnership && hasValidFreshness;

                // Skip if variant exists and freshness matches current inputs
                if (variantExists && hasOwnedFreshness &&
                    existingFreshness.Matches(sourceInfo, sourceSha256, quality, size, ext, tool))
                {
                    AddTrackedOutput(context, generatedOutputs, sizedFile);
                    AddTrackedOutput(context, generatedOutputs, freshnessFile);
                    continue;
                }

                // A filename only excludes recursive input discovery. Existing bytes or
                // sidecars without a valid Bukit ownership record remain user-owned.
                if ((variantExists || sidecarExists) && !hasOwnedFreshness)
                {
                    context.Logger.Warn($"event=image_resize.skip file={Path.GetFileName(sizedFile)} reason=unowned_existing_output");
                    continue;
                }

                // Once freshness no longer matches, the prior managed output is
                // invalid. Remove it before rebuilding so a failed resize cannot
                // leave stale bytes addressable or re-project them into srcset.
                if (hasOwnedFreshness)
                {
                    TryDelete(sizedFile);
                    TryDelete(freshnessFile);
                }

                try
                {
                    var temporarySizedFile = Path.Combine(
                        Path.GetDirectoryName(sizedFile)!,
                        $".{Path.GetFileNameWithoutExtension(sizedFile)}.bukit-{Guid.NewGuid():N}{ext}");
                    var temporaryFreshnessFile = freshnessFile + $".bukit-{Guid.NewGuid():N}.tmp";
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = tool,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    startInfo.ArgumentList.Add(imageFile);
                    startInfo.ArgumentList.Add("-resize");
                    startInfo.ArgumentList.Add($"{size}x");
                    startInfo.ArgumentList.Add("-quality");
                    startInfo.ArgumentList.Add(quality.ToString());
                    startInfo.ArgumentList.Add(temporarySizedFile);
                    try
                    {
                        var result = await ExternalToolProcessRunner.RunAsync(
                            startInfo,
                            TimeSpan.FromSeconds(10),
                            cancellationToken);
                        if (result.ExitCode == 0 && File.Exists(temporarySizedFile) &&
                            await _imageValidator.ValidateAsync(
                                temporarySizedFile,
                                MimeTypeForExtension(ext),
                                cancellationToken))
                        {
                            var variantInfo = new FileInfo(temporarySizedFile);
                            var variantSha256 = await ComputeSha256Async(temporarySizedFile, cancellationToken);
                            WriteFreshness(temporaryFreshnessFile, new VariantFreshness(
                                SchemaVersion: FreshnessSchemaVersion,
                                Owner: FreshnessOwner,
                                SourcePath: GetRelativeIdentity(context.OutputDir, imageFile),
                                VariantPath: GetRelativeIdentity(context.OutputDir, sizedFile),
                                SourceSize: sourceInfo.Length,
                                SourceMtime: sourceInfo.LastWriteTimeUtc.Ticks,
                                SourceSha256: sourceSha256,
                                VariantLength: variantInfo.Length,
                                VariantSha256: variantSha256,
                                Quality: quality,
                                Size: size,
                                Format: ext.ToLowerInvariant(),
                                Tool: tool));
                            File.Move(temporarySizedFile, sizedFile, overwrite: false);
                            try
                            {
                                File.Move(temporaryFreshnessFile, freshnessFile, overwrite: false);
                            }
                            catch
                            {
                                TryDelete(sizedFile);
                                throw;
                            }

                            AddTrackedOutput(context, generatedOutputs, sizedFile);
                            AddTrackedOutput(context, generatedOutputs, freshnessFile);
                            context.Logger.Info($"event=image_resize.ok file={Path.GetFileName(sizedFile)}");
                        }
                        else
                        {
                            context.Logger.Warn($"event=image_resize.error file={Path.GetFileName(imageFile)} reason={result.StandardError}");
                        }
                    }
                    finally
                    {
                        TryDelete(temporarySizedFile);
                        TryDelete(temporaryFreshnessFile);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    context.Logger.Warn($"event=image_resize.error file={Path.GetFileName(imageFile)} reason={ex.Message}");
                }
            }
        }

        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var imageFile in imageFiles)
        {
            var relPath = Path.GetRelativePath(assetsDir, imageFile);
            var baseName = Path.GetFileNameWithoutExtension(imageFile);
            var ext = Path.GetExtension(imageFile);

            var srcsetParts = new List<string>();
            var existingSizes = new List<int>();
            foreach (var size in sizes)
            {
                var sizedFile = Path.Combine(
                    Path.GetDirectoryName(imageFile)!,
                    $"{baseName}-{size}w{ext}");
                if (!File.Exists(sizedFile))
                {
                    continue;
                }

                if (!IsTrackedOutput(context, generatedOutputs, sizedFile))
                {
                    continue;
                }

                var sizedRel = Path.Combine(Path.GetDirectoryName(relPath) ?? "", $"{baseName}-{size}w{ext}")
                    .Replace("\\", "/", StringComparison.Ordinal);
                srcsetParts.Add($"/assets/{sizedRel} {size}w");
                existingSizes.Add(size);
            }

            if (srcsetParts.Count == 0)
            {
                continue;
            }

            var rel = relPath.Replace("\\", "/", StringComparison.Ordinal);
            data[rel] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["srcset"] = string.Join(", ", srcsetParts),
                ["sizes"] = existingSizes.ToArray(),
                ["url"] = $"/assets/{rel}"
            };
        }

        if (data.Count > 0)
        {
            context.Data["__image_srcsets"] = data;
        }

        if (generatedOutputs.Count > 0)
        {
            context.Data["__plugin_outputs"] = generatedOutputs;
        }
    }

    private const string FreshnessSuffix = ".bukit-freshness.json";
    private const int FreshnessSchemaVersion = 1;
    private const string FreshnessOwner = "bukit:image-processing";

    private static string MimeTypeForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        _ => "application/octet-stream"
    };

    private static void AddTrackedOutput(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> outputs,
        string sizedFile)
    {
        var relPath = Path.GetRelativePath(context.OutputDir, sizedFile)
            .Replace("\\", "/", StringComparison.Ordinal);
        outputs.Add(new PluginOutputTrackingInfo("image-processing", "after-build", relPath));
    }

    private static bool IsTrackedOutput(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> outputs,
        string path)
    {
        var relPath = Path.GetRelativePath(context.OutputDir, path)
            .Replace("\\", "/", StringComparison.Ordinal);
        return outputs.Contains(new PluginOutputTrackingInfo("image-processing", "after-build", relPath));
    }

    private static HashSet<PluginOutputTrackingInfo> GetPriorPluginOutputs(BuildContext context)
    {
        if (context.Data.TryGetValue(BuildContextDataKeys.PriorPluginOutputs, out var value) &&
            value is HashSet<PluginOutputTrackingInfo> outputs)
        {
            return outputs;
        }

        return new HashSet<PluginOutputTrackingInfo>();
    }

    private static bool HasPriorOwnership(
        string outputDir,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        string variantFile,
        string sidecarFile)
    {
        var variantPath = GetRelativeIdentity(outputDir, variantFile);
        var sidecarPath = GetRelativeIdentity(outputDir, sidecarFile);
        return priorOutputs.Contains(new PluginOutputTrackingInfo("image-processing", "after-build", variantPath)) &&
               priorOutputs.Contains(new PluginOutputTrackingInfo("image-processing", "after-build", sidecarPath));
    }

    private sealed record VariantFreshness(
        int SchemaVersion,
        string Owner,
        string SourcePath,
        string VariantPath,
        long SourceSize,
        long SourceMtime,
        string SourceSha256,
        long VariantLength,
        string VariantSha256,
        int Quality,
        int Size,
        string Format,
        string Tool)
    {
        public bool Matches(
            FileInfo source,
            string sourceSha256,
            int quality,
            int size,
            string format,
            string tool)
            => SourceSize == source.Length
               && SourceMtime == source.LastWriteTimeUtc.Ticks
               && string.Equals(SourceSha256, sourceSha256, StringComparison.Ordinal)
               && Quality == quality
               && Size == size
               && string.Equals(Format, format.ToLowerInvariant(), StringComparison.Ordinal)
               && string.Equals(Tool, tool, StringComparison.Ordinal);
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(VariantFreshness))]
    private sealed partial class VariantFreshnessJsonContext : JsonSerializerContext;

    private static bool TryReadFreshness(
        string path,
        string outputDir,
        string sourceFile,
        string variantFile,
        int expectedSize,
        out VariantFreshness freshness)
    {
        freshness = null!;
        try
        {
            if (!TryReadFreshnessRecord(path, out var candidate) ||
                !string.Equals(candidate.SourcePath, GetRelativeIdentity(outputDir, sourceFile), StringComparison.Ordinal) ||
                !string.Equals(candidate.VariantPath, GetRelativeIdentity(outputDir, variantFile), StringComparison.Ordinal) ||
                candidate.Size != expectedSize ||
                !string.Equals(candidate.Format, Path.GetExtension(variantFile).ToLowerInvariant(), StringComparison.Ordinal) ||
                !MatchesVariantIdentity(candidate, variantFile))
            {
                return false;
            }

            freshness = candidate;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryReadFreshnessRecord(string path, out VariantFreshness freshness)
    {
        freshness = null!;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("schemaVersion", out var schemaVersionEl) ||
                !schemaVersionEl.TryGetInt32(out var schemaVersion) ||
                schemaVersion != FreshnessSchemaVersion ||
                !root.TryGetProperty("owner", out var ownerEl) ||
                ownerEl.ValueKind != JsonValueKind.String ||
                !string.Equals(ownerEl.GetString(), FreshnessOwner, StringComparison.Ordinal) ||
                !root.TryGetProperty("sourcePath", out var sourcePathEl) ||
                sourcePathEl.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("variantPath", out var variantPathEl) ||
                variantPathEl.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("sourceSize", out var sourceSizeEl) ||
                !sourceSizeEl.TryGetInt64(out var sourceSize) ||
                sourceSize < 0 ||
                !root.TryGetProperty("sourceMtime", out var sourceMtimeEl) ||
                !sourceMtimeEl.TryGetInt64(out var sourceMtime) ||
                !root.TryGetProperty("sourceSha256", out var sourceSha256El) ||
                sourceSha256El.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("variantLength", out var variantLengthEl) ||
                !variantLengthEl.TryGetInt64(out var variantLength) ||
                variantLength < 0 ||
                !root.TryGetProperty("variantSha256", out var variantSha256El) ||
                variantSha256El.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("quality", out var qualityEl) ||
                !qualityEl.TryGetInt32(out var quality) ||
                quality <= 0 ||
                !root.TryGetProperty("size", out var sizeEl) ||
                !sizeEl.TryGetInt32(out var size) ||
                size <= 0 ||
                !root.TryGetProperty("format", out var formatEl) ||
                formatEl.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("tool", out var toolEl) ||
                toolEl.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var sourcePath = sourcePathEl.GetString() ?? string.Empty;
            var variantPath = variantPathEl.GetString() ?? string.Empty;
            var sourceSha256 = sourceSha256El.GetString() ?? string.Empty;
            var variantSha256 = variantSha256El.GetString() ?? string.Empty;
            var format = formatEl.GetString() ?? string.Empty;
            var tool = toolEl.GetString() ?? string.Empty;
            if (!IsNormalizedRelativeIdentity(sourcePath) ||
                !IsNormalizedRelativeIdentity(variantPath) ||
                !IsSha256(sourceSha256) ||
                !IsSha256(variantSha256) ||
                string.IsNullOrWhiteSpace(format) ||
                string.IsNullOrWhiteSpace(tool))
            {
                return false;
            }

            freshness = new VariantFreshness(
                schemaVersion,
                ownerEl.GetString()!,
                sourcePath,
                variantPath,
                sourceSize,
                sourceMtime,
                sourceSha256,
                variantLength,
                variantSha256,
                quality,
                size,
                format,
                tool);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool MatchesVariantIdentity(VariantFreshness freshness, string variantFile)
    {
        try
        {
            var info = new FileInfo(variantFile);
            return info.Exists &&
                   info.Length == freshness.VariantLength &&
                   string.Equals(ComputeSha256(variantFile), freshness.VariantSha256, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void WriteFreshness(string path, VariantFreshness freshness)
    {
        var json = JsonSerializer.Serialize(freshness, VariantFreshnessJsonContext.Default.VariantFreshness);
        File.WriteAllText(path, json);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string GetRelativeIdentity(string outputDir, string path)
    {
        var identity = Path.GetRelativePath(Path.GetFullPath(outputDir), Path.GetFullPath(path))
            .Replace("\\", "/", StringComparison.Ordinal);
        if (!IsNormalizedRelativeIdentity(identity))
        {
            throw new ArgumentException("Managed image identity must remain inside the output directory.", nameof(path));
        }

        return identity;
    }

    private static bool IsNormalizedRelativeIdentity(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) ||
            Path.IsPathRooted(identity) ||
            identity.Contains('\\'))
        {
            return false;
        }

        return identity.Split('/', StringSplitOptions.None)
            .All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static bool TryResolveRelativeIdentity(
        string outputDir,
        string identity,
        out string path)
    {
        path = string.Empty;
        if (!IsNormalizedRelativeIdentity(identity))
        {
            return false;
        }

        try
        {
            var candidate = Path.GetFullPath(Path.Combine(
                outputDir,
                identity.Replace('/', Path.DirectorySeparatorChar)));
            if (!string.Equals(GetRelativeIdentity(outputDir, candidate), identity, StringComparison.Ordinal))
            {
                return false;
            }

            path = candidate;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsWithinDirectory(string directory, string path)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(path));
        return !Path.IsPathRooted(relative) &&
               !string.Equals(relative, "..", StringComparison.Ordinal) &&
               !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static async Task<string?> FindResizeToolAsync(
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var name in new[] { "magick", "convert" })
        {
            try
            {
                var result = await ExternalToolProcessRunner.RunAsync(new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }, TimeSpan.FromSeconds(3), cancellationToken);
                if (result.ExitCode == 0)
                {
                    return name;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.Warn($"event=image_processing.tool.probe.failed tool={name} reason={ex.Message}");
            }
        }

        return null;
    }

    private static bool IsGeneratedSizedImage(string path)
    {
        // Exclude any managed variant regardless of the currently configured sizes,
        // including historical sizes from prior builds
        var stem = Path.GetFileNameWithoutExtension(path);
        var suffixStart = stem.LastIndexOf('-');
        if (suffixStart <= 0 || suffixStart == stem.Length - 2)
        {
            return false;
        }

        var suffix = stem[(suffixStart + 1)..];
        return suffix.EndsWith('w')
               && suffix.Length > 1
               && int.TryParse(suffix.AsSpan(0, suffix.Length - 1), out _);
    }

    private static void CleanupOrphanedOwnedVariants(
        string outputDir,
        string assetsDir,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        CancellationToken cancellationToken)
    {
        foreach (var sidecarFile in SafeFileEnumerator.EnumerateFiles(assetsDir, $"*{FreshnessSuffix}"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var variantFile = sidecarFile[..^FreshnessSuffix.Length];
            if (!HasPriorOwnership(outputDir, priorOutputs, variantFile, sidecarFile) ||
                !TryReadFreshnessRecord(sidecarFile, out var freshness) ||
                !string.Equals(freshness.VariantPath, GetRelativeIdentity(outputDir, variantFile), StringComparison.Ordinal) ||
                !IsExpectedVariantPath(variantFile, freshness.Size, freshness.Format) ||
                !MatchesVariantIdentity(freshness, variantFile) ||
                !TryResolveRelativeIdentity(outputDir, freshness.SourcePath, out var sourceFile) ||
                !IsWithinDirectory(assetsDir, sourceFile) ||
                File.Exists(sourceFile))
            {
                continue;
            }

            TryDelete(variantFile);
            TryDelete(sidecarFile);
        }
    }

    private static bool IsExpectedVariantPath(string variantFile, int size, string format)
    {
        var extension = Path.GetExtension(variantFile);
        var stem = Path.GetFileNameWithoutExtension(variantFile);
        return string.Equals(extension.ToLowerInvariant(), format, StringComparison.Ordinal) &&
               stem.Length > $"-{size}w".Length &&
               stem.EndsWith($"-{size}w", StringComparison.Ordinal);
    }

    private static void CleanupStaleVariants(
        string outputDir,
        string sourceFile,
        IReadOnlyList<int> currentSizes,
        HashSet<PluginOutputTrackingInfo> priorOutputs)
    {
        var dir = Path.GetDirectoryName(sourceFile)!;
        var baseName = Path.GetFileNameWithoutExtension(sourceFile);
        var ext = Path.GetExtension(sourceFile);
        var currentSizeSet = new HashSet<int>(currentSizes);

        foreach (var existingFile in SafeFileEnumerator.EnumerateFiles(dir, $"{baseName}-*w{ext}"))
        {
            var stem = Path.GetFileNameWithoutExtension(existingFile);
            var suffix = stem.Substring(baseName.Length);
            // Parse -NNNw pattern
            if (suffix.Length > 2 && suffix[^1] == 'w'
                && int.TryParse(suffix.AsSpan(1, suffix.Length - 2), out var parsedSize)
                && !currentSizeSet.Contains(parsedSize)
                && HasPriorOwnership(
                    outputDir,
                    priorOutputs,
                    existingFile,
                    existingFile + FreshnessSuffix)
                && TryReadFreshness(
                    existingFile + FreshnessSuffix,
                    outputDir,
                    sourceFile,
                    existingFile,
                    parsedSize,
                    out _))
            {
                TryDelete(existingFile);
                TryDelete(existingFile + FreshnessSuffix);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
