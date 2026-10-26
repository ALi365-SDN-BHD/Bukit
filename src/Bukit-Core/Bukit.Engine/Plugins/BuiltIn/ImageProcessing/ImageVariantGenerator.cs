using System.Diagnostics;
using Bukit.Content.Media;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
using SkiaSharp;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantFreshness;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantOwnership;

namespace Bukit.Engine.Plugins.BuiltIn.ImageProcessing;

/// <summary>
/// Produces resized (external tool or SkiaSharp) and WebP variants with
/// atomic writes and freshness records.
/// </summary>
internal sealed class ImageVariantGenerator
{
    private readonly ImageContentValidator _imageValidator = new();

    internal static readonly int[] DefaultSizes = [480, 768, 1200];

    internal static IReadOnlyList<int> NormalizeSizes(IReadOnlyList<int>? sizes) =>
        [.. (sizes ?? DefaultSizes).Where(size => size > 0).Distinct().Order()];

    internal static bool TryGetImageWidth(string path, ILogger logger, out int width)
    {
        width = 0;
        try
        {
            using var codec = SKCodec.Create(path);
            if (codec is null) return false;
            var origin = ImageOptimizer.GetEncodedOrigin(path, codec);
            width = origin >= SKEncodedOrigin.LeftTop ? codec.Info.Height : codec.Info.Width;
            return width > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
        {
            logger.Warn($"event=image_processing.skip file={Path.GetFileName(path)} reason=invalid_source");
            return false;
        }
    }

    internal async Task GenerateResizedVariantAsync(
        BuildContext context,
        string imageFile,
        FileInfo sourceInfo,
        string sourceSha256,
        int size,
        int quality,
        string? tool,
        string toolIdentity,
        HashSet<PluginOutputTrackingInfo> priorPluginOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs,
        CancellationToken cancellationToken)
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
            existingFreshness.Matches(sourceInfo, sourceSha256, quality, size, ext, toolIdentity))
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

        try
        {
            var temporarySizedFile = Path.Combine(
                Path.GetDirectoryName(sizedFile)!,
                $".{Path.GetFileNameWithoutExtension(sizedFile)}.bukit-{Guid.NewGuid():N}{ext}");
            var temporaryFreshnessFile = freshnessFile + $".bukit-{Guid.NewGuid():N}.tmp";
            try
            {
                (int ExitCode, string StandardError) result;
                if (tool is null)
                {
                    await ResizeWithSkiaSharpAsync(
                        imageFile,
                        temporarySizedFile,
                        size,
                        quality,
                        cancellationToken);
                    result = (0, string.Empty);
                }
                else
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = tool,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    startInfo.ArgumentList.Add(imageFile);
                    startInfo.ArgumentList.Add("-auto-orient");
                    startInfo.ArgumentList.Add("-resize");
                    startInfo.ArgumentList.Add($"{size}x");
                    startInfo.ArgumentList.Add("-quality");
                    startInfo.ArgumentList.Add(quality.ToString());
                    startInfo.ArgumentList.Add(temporarySizedFile);
                    var externalResult = await ExternalToolProcessRunner.RunAsync(
                        startInfo,
                        TimeSpan.FromSeconds(10),
                        context.Logger,
                        cancellationToken);
                    result = (externalResult.ExitCode, externalResult.StandardError);
                }
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
                        Tool: toolIdentity));
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

    internal static Task ResizeWithSkiaSharpAsync(
        string source,
        string destination,
        int width,
        int quality,
        CancellationToken cancellationToken)
    {
        var format = Path.GetExtension(destination).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
        return ImageOptimizer.EncodeAsync(source, destination, width, quality, format, cancellationToken);
    }

    internal async Task<Dictionary<string, string>> GenerateWebpVariantsAsync(
        BuildContext context,
        IReadOnlyList<string> imageFiles,
        IReadOnlyDictionary<string, int> sourceWidths,
        IReadOnlyList<int> sizes,
        int quality,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs,
        CancellationToken cancellationToken)
    {
        var srcsets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var baseUrl = BuildPathUtils.NormalizeBaseUrl(context.BaseUrl).TrimEnd('/');

        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!sourceWidths.TryGetValue(imageFile, out var sourceWidth))
            {
                continue;
            }

            var sourceInfo = new FileInfo(imageFile);
            var sourceSha256 = await ComputeSha256Async(imageFile, cancellationToken);
            var baseName = Path.GetFileNameWithoutExtension(imageFile);
            var candidates = new List<string>();
            foreach (var width in sizes.Where(size => size < sourceWidth).Append(sourceWidth).Distinct().Order())
            {
                var webpFile = Path.Combine(Path.GetDirectoryName(imageFile)!, $"{baseName}-{width}w.webp");
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

    private async Task<bool> GenerateOwnedWebpVariantAsync(
        BuildContext context,
        string sourceFile,
        FileInfo sourceInfo,
        string sourceSha256,
        string webpFile,
        int width,
        int quality,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs,
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
            return false;
        }
        finally
        {
            TryDelete(temporaryWebp);
            TryDelete(temporaryFreshness);
        }
    }

    internal static async Task<string?> FindResizeToolAsync(
        ILogger logger,
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
                }, TimeSpan.FromSeconds(3), logger, cancellationToken);
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
                logger.Warn($"event=image_processing.tool.probe.failed tool={name} reason={ex.Message}");
            }
        }

        return null;
    }

    internal static string MimeTypeForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        _ => "application/octet-stream"
    };
}
