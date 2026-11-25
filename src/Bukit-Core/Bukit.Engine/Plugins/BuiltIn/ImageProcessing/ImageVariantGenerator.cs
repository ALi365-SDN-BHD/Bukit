using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
using SkiaSharp;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantFreshness;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantOwnership;

namespace Bukit.Engine.Plugins.BuiltIn.ImageProcessing;

/// <summary>
/// Produces resized and WebP variants with SkiaSharp, using atomic writes and
/// freshness records. No external image tool is ever invoked.
/// </summary>
internal static class ImageVariantGenerator
{
    internal const int MaxVariantFailures = 5;
    internal const string SkiaSharpResizeIdentity = "skiasharp-orientation-v3";
    internal static readonly int[] DefaultSizes = [480, 768, 1200];

    internal static IReadOnlyList<int> NormalizeSizes(IReadOnlyList<int>? sizes) =>
        [.. (sizes ?? DefaultSizes).Where(size => size > 0).Distinct().Order()];

    internal static bool TryGetImageWidth(string path, ILogger logger, out int width)
        => TryGetImageSize(path, logger, out width, out _);

    internal static bool TryGetImageSize(string path, ILogger logger, out int width, out int height)
    {
        try
        {
            return TryReadImageSize(path, out width, out height);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            width = 0;
            height = 0;
            logger.Warn($"event=image_processing.skip file={Path.GetFileName(path)} reason=invalid_source");
            return false;
        }
    }

    /// <summary>
    /// Reads the display width and height (after EXIF orientation) from the image header only.
    /// Shared by variant planning, the HTML transform and the incremental fingerprint so all agree.
    /// </summary>
    internal static bool TryReadImageSize(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        using var codec = SKCodec.Create(path);
        if (codec is null) return false;
        // Resizing keeps only the first frame, so animated WebP stays as uploaded.
        if (codec.EncodedFormat == SKEncodedImageFormat.Webp && codec.FrameCount > 1) return false;
        var rotated = ImageOptimizer.GetEncodedOrigin(path, codec) >= SKEncodedOrigin.LeftTop;
        (width, height) = rotated ? (codec.Info.Height, codec.Info.Width) : (codec.Info.Width, codec.Info.Height);
        return width > 0 && height > 0;
    }

    internal static async Task GenerateResizedVariantAsync(
        BuildContext context,
        string imageFile,
        FileInfo sourceInfo,
        string sourceSha256,
        int size,
        int quality,
        HashSet<PluginOutputTrackingInfo> priorPluginOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs,
        ICollection<string> failures,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var ext = Path.GetExtension(imageFile);
        var sizedFile = ImageVariantPlan.VariantFileName(imageFile, size, ext);
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
            existingFreshness.Matches(sourceInfo, sourceSha256, quality, size, ext, SkiaSharpResizeIdentity))
        {
            AddTrackedOutput(context, generatedOutputs, sizedFile);
            AddTrackedOutput(context, generatedOutputs, freshnessFile);
            return;
        }

        // A filename only excludes recursive input discovery. Existing bytes or
        // sidecars without a valid Bukit ownership record remain user-owned.
        if ((variantExists || sidecarExists) && !hasOwnedFreshness)
        {
            context.Logger.Warn($"event=image_resize.skip file={Path.GetFileName(sizedFile)} reason=unowned_existing_output");
            if (!variantExists)
            {
                // Pages already list this variant in srcset. A user-owned sidecar
                // blocks generation, so the URL would 404; report it instead of
                // passing silently. The sidecar itself is left untouched.
                failures.Add($"{GetRelativeIdentity(context.OutputDir, sizedFile)} (blocked by unowned {Path.GetFileName(freshnessFile)}; remove it)");
            }

            return;
        }

        // Once freshness no longer matches, the prior managed output is
        // invalid. Remove it before rebuilding so a failed resize cannot
        // leave stale bytes addressable or re-project them into srcset.
        if (hasOwnedFreshness)
        {
            TryDelete(sizedFile);
            TryDelete(freshnessFile);
        }

        var temporarySizedFile = Path.Combine(
            Path.GetDirectoryName(sizedFile)!,
            $".{Path.GetFileNameWithoutExtension(sizedFile)}.bukit-{Guid.NewGuid():N}{ext}");
        var temporaryFreshnessFile = freshnessFile + $".bukit-{Guid.NewGuid():N}.tmp";
        try
        {
            await ResizeWithSkiaSharpAsync(
                imageFile,
                temporarySizedFile,
                size,
                quality,
                cancellationToken);
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
                Tool: SkiaSharpResizeIdentity));
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Logger.Warn($"event=image_resize.error file={Path.GetFileName(imageFile)} reason={ex.Message}");
            failures.Add(GetRelativeIdentity(context.OutputDir, sizedFile));
        }
        finally
        {
            TryDelete(temporarySizedFile);
            TryDelete(temporaryFreshnessFile);
        }
    }

    internal static Task ResizeWithSkiaSharpAsync(
        string source,
        string destination,
        int width,
        int quality,
        CancellationToken cancellationToken)
    {
        var format = Path.GetExtension(destination).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg
        };
        // Resizing a WebP source keeps it WebP; animated WebP never reaches this point.
        return ImageOptimizer.EncodeAsync(source, destination, width, quality, format, cancellationToken,
            allowStaticWebpSource: format == SKEncodedImageFormat.Webp);
    }

    internal static async Task<Dictionary<string, string>> GenerateWebpVariantsAsync(
        BuildContext context,
        IReadOnlyList<string> imageFiles,
        IReadOnlyDictionary<string, int> sourceWidths,
        IReadOnlyList<int> sizes,
        int quality,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs,
        Dictionary<string, string> sourceHashes,
        int failureLimit,
        ICollection<string> failures,
        CancellationToken cancellationToken)
    {
        var srcsets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var baseUrl = BuildPathUtils.NormalizeBaseUrl(context.BaseUrl).TrimEnd('/');

        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (failures.Count >= failureLimit)
            {
                break;
            }

            if (!sourceWidths.TryGetValue(imageFile, out var sourceWidth))
            {
                continue;
            }

            var sourceInfo = new FileInfo(imageFile);
            var sourceSha256 = await GetOrComputeSourceSha256Async(imageFile, sourceHashes, cancellationToken);
            var candidates = new List<string>();
            foreach (var width in ImageVariantPlan.CandidateWidths(sizes, sourceWidth))
            {
                if (failures.Count >= failureLimit)
                {
                    break;
                }

                var webpFile = ImageVariantPlan.VariantFileName(imageFile, width, ".webp");
                if (!await GenerateOwnedWebpVariantAsync(
                    context,
                    imageFile,
                    sourceInfo,
                    sourceSha256,
                    webpFile,
                    width,
                    quality,
                    priorOutputs,
                    generatedOutputs,
                    failures,
                    cancellationToken))
                {
                    continue;
                }

                var webpRelative = GetRelativeIdentity(context.OutputDir, webpFile);
                candidates.Add($"{baseUrl}/{webpRelative} {width}w");
            }

            if (candidates.Count > 0)
            {
                var sourceRelative = GetRelativeIdentity(context.OutputDir, imageFile);
                srcsets[$"{baseUrl}/{sourceRelative}"] = string.Join(", ", candidates);
            }
        }

        return srcsets;
    }

    private static async Task<bool> GenerateOwnedWebpVariantAsync(
        BuildContext context,
        string sourceFile,
        FileInfo sourceInfo,
        string sourceSha256,
        string webpFile,
        int width,
        int quality,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs,
        ICollection<string> failures,
        CancellationToken cancellationToken)
    {
        var freshnessFile = webpFile + FreshnessSuffix;
        var variantExists = File.Exists(webpFile);
        var sidecarExists = File.Exists(freshnessFile);
        var hasPriorOwnership = HasPriorOwnership(context.OutputDir, priorOutputs, webpFile, freshnessFile);
        var hasValidFreshness = TryReadFreshness(
            freshnessFile,
            context.OutputDir,
            sourceFile,
            webpFile,
            width,
            out var existingFreshness);
        var hasOwnedFreshness = hasPriorOwnership && hasValidFreshness;

        if (variantExists && hasOwnedFreshness && existingFreshness.Matches(
            sourceInfo,
            sourceSha256,
            quality,
            width,
            ".webp",
            ImageOptimizer.WebpEncoderIdentity))
        {
            AddTrackedOutput(context, generatedOutputs, webpFile);
            AddTrackedOutput(context, generatedOutputs, freshnessFile);
            return true;
        }

        if ((variantExists || sidecarExists) && !hasOwnedFreshness)
        {
            context.Logger.Warn($"event=image_webp.skip file={Path.GetFileName(webpFile)} reason=unowned_existing_output");
            return false;
        }

        if (hasOwnedFreshness)
        {
            TryDelete(webpFile);
            TryDelete(freshnessFile);
        }

        var temporaryWebp = Path.Combine(
            Path.GetDirectoryName(webpFile)!,
            $".{Path.GetFileNameWithoutExtension(webpFile)}.bukit-{Guid.NewGuid():N}.webp");
        var temporaryFreshness = freshnessFile + $".bukit-{Guid.NewGuid():N}.tmp";
        try
        {
            await ImageOptimizer.EncodeWebpAsync(sourceFile, temporaryWebp, width, quality, cancellationToken);
            var variantInfo = new FileInfo(temporaryWebp);
            var variantSha256 = await ComputeSha256Async(temporaryWebp, cancellationToken);
            WriteFreshness(temporaryFreshness, new VariantFreshness(
                SchemaVersion: FreshnessSchemaVersion,
                Owner: FreshnessOwner,
                SourcePath: GetRelativeIdentity(context.OutputDir, sourceFile),
                VariantPath: GetRelativeIdentity(context.OutputDir, webpFile),
                SourceSize: sourceInfo.Length,
                SourceMtime: sourceInfo.LastWriteTimeUtc.Ticks,
                SourceSha256: sourceSha256,
                VariantLength: variantInfo.Length,
                VariantSha256: variantSha256,
                Quality: quality,
                Size: width,
                Format: ".webp",
                Tool: ImageOptimizer.WebpEncoderIdentity));
            File.Move(temporaryWebp, webpFile, overwrite: false);
            try
            {
                File.Move(temporaryFreshness, freshnessFile, overwrite: false);
            }
            catch
            {
                TryDelete(webpFile);
                throw;
            }

            AddTrackedOutput(context, generatedOutputs, webpFile);
            AddTrackedOutput(context, generatedOutputs, freshnessFile);
            context.Logger.Info($"event=image_webp.ok file={Path.GetFileName(webpFile)}");
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            context.Logger.Warn($"event=image_webp.error file={Path.GetFileName(webpFile)} reason={ex.GetType().Name}");
            failures.Add(GetRelativeIdentity(context.OutputDir, webpFile));
            return false;
        }
        finally
        {
            TryDelete(temporaryWebp);
            TryDelete(temporaryFreshness);
        }
    }
}
