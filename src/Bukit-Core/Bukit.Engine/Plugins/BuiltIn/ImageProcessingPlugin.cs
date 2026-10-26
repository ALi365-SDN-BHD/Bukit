using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Shared;
using Bukit.Engine.Plugins.BuiltIn.ImageProcessing;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantFreshness;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantGenerator;
using static Bukit.Engine.Plugins.BuiltIn.ImageProcessing.ImageVariantOwnership;

namespace Bukit.Engine.Plugins.BuiltIn;

/// <summary>
/// Generates resized and WebP image variants after the build and projects their
/// srcsets. Variant generation, freshness records, ownership and HTML rewriting
/// live in the ImageProcessing folder.
/// </summary>
internal sealed class ImageProcessingPlugin : IBukitPlugin, IAfterBuildAsyncPlugin, IHtmlTransformPlugin
{
    private readonly AppConfig _config;
    private readonly ImageVariantGenerator _generator = new();

    internal ImageProcessingPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "image-processing";
    public string Version => "1.0.0";

    public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
    {
        context.BuildContext.TryGet(BuildDataKeys.MediaDownloadDir, out var mediaDownloadDir);
        return new ResponsiveImageHtmlTransform(
            _config,
            mediaDownloadDir,
            context.BuildContext.BaseUrl,
            context.BuildContext.Logger);
    }

    public async Task AfterBuildAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        var config = _config.Theme.Images;
        if (config is not { Enabled: true })
        {
            return;
        }
        if (config.Formats?.Any(format => string.Equals(format, "avif", StringComparison.OrdinalIgnoreCase)) == true)
        {
            context.Logger.Warn("event=image_avif.skip reason=no_approved_decoder");
        }

        var priorPluginOutputs = GetPriorPluginOutputs(context);
        var imageFiles = FindSourceImages(context, priorPluginOutputs, cancellationToken);
        var sizes = NormalizeSizes(config.Sizes);
        var webpEnabled = config.Formats?.Any(format =>
            string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase)) == true;
        var generatedOutputs = new HashSet<PluginOutputTrackingInfo>();

        if (imageFiles.Count == 0)
        {
            if (!webpEnabled)
            {
                PreservePriorWebpOutputs(context, priorPluginOutputs, generatedOutputs);
            }
            SetTrackedOutputs(context, generatedOutputs);
            return;
        }

        var (resizePlans, sourceWidths) = PlanResizes(
            context,
            imageFiles,
            sizes,
            webpEnabled,
            priorPluginOutputs,
            cancellationToken);

        var quality = config.Quality > 0 ? config.Quality : 80;
        string? tool = null;
        if (resizePlans.Count > 0)
        {
            tool = await FindResizeToolAsync(context.Logger, cancellationToken);
            if (tool is null)
            {
                context.Logger.Info("event=image_processing.fallback tool=skiasharp");
            }
        }
        var toolIdentity = tool is null ? "skiasharp-orientation-v2" : $"{tool}-auto-orient-v1";

        await GenerateResizedVariantsAsync(
            context,
            resizePlans,
            quality,
            tool,
            toolIdentity,
            priorPluginOutputs,
            generatedOutputs,
            cancellationToken);

        if (webpEnabled)
        {
            var webpSrcsets = await _generator.GenerateWebpVariantsAsync(
                context,
                imageFiles,
                sourceWidths,
                sizes,
                quality,
                priorPluginOutputs,
                generatedOutputs,
                cancellationToken);
            RewriteCurrentHtmlWithWebp(context, webpSrcsets, cancellationToken);
        }
        else
        {
            PreservePriorWebpOutputs(context, priorPluginOutputs, generatedOutputs);
        }

        var data = BuildSrcsetData(context, imageFiles, resizePlans, generatedOutputs);
        if (data.Count > 0)
        {
            context.Set(BuildDataKeys.ImageSrcsets, data);
        }

        SetTrackedOutputs(context, generatedOutputs);
    }

    private static (Dictionary<string, IReadOnlyList<int>> ResizePlans, Dictionary<string, int> SourceWidths) PlanResizes(
        BuildContext context,
        IReadOnlyList<string> imageFiles,
        IReadOnlyList<int> sizes,
        bool webpEnabled,
        HashSet<PluginOutputTrackingInfo> priorPluginOutputs,
        CancellationToken cancellationToken)
    {
        var resizePlans = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        var sourceWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasWidth = TryGetImageWidth(imageFile, context.Logger, out var width);
            var applicableSizes = hasWidth ? [.. sizes.Where(size => size < width)] : Array.Empty<int>();
            CleanupStaleVariants(
                context.OutputDir,
                imageFile,
                applicableSizes,
                priorPluginOutputs);
            if (hasWidth)
            {
                sourceWidths[imageFile] = width;
                if (webpEnabled)
                {
                    CleanupStaleVariants(
                        context.OutputDir,
                        imageFile,
                        [.. applicableSizes.Append(width).Distinct()],
                        priorPluginOutputs,
                        ".webp");
                }
            }
            if (applicableSizes.Length > 0)
            {
                resizePlans[imageFile] = applicableSizes;
            }
        }

        return (resizePlans, sourceWidths);
    }

    private async Task GenerateResizedVariantsAsync(
        BuildContext context,
        IReadOnlyDictionary<string, IReadOnlyList<int>> resizePlans,
        int quality,
        string? tool,
        string toolIdentity,
        HashSet<PluginOutputTrackingInfo> priorPluginOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs,
        CancellationToken cancellationToken)
    {
        foreach (var (imageFile, applicableSizes) in resizePlans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceInfo = new FileInfo(imageFile);
            var sourceSha256 = await ComputeSha256Async(imageFile, cancellationToken);

            foreach (var size in applicableSizes)
            {
                await _generator.GenerateResizedVariantAsync(
                    context,
                    imageFile,
                    sourceInfo,
                    sourceSha256,
                    size,
                    quality,
                    tool,
                    toolIdentity,
                    priorPluginOutputs,
                    generatedOutputs,
                    cancellationToken);
            }
        }
    }

    private static Dictionary<string, object> BuildSrcsetData(
        BuildContext context,
        IReadOnlyList<string> imageFiles,
        IReadOnlyDictionary<string, IReadOnlyList<int>> resizePlans,
        HashSet<PluginOutputTrackingInfo> generatedOutputs)
    {
        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var imageFile in imageFiles)
        {
            var relPath = Path.GetRelativePath(context.OutputDir, imageFile);
            var baseName = Path.GetFileNameWithoutExtension(imageFile);
            var ext = Path.GetExtension(imageFile);

            var srcsetParts = new List<string>();
            var existingSizes = new List<int>();
            foreach (var size in resizePlans.GetValueOrDefault(imageFile) ?? [])
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
                srcsetParts.Add($"/{sizedRel} {size}w");
                existingSizes.Add(size);
            }

            if (srcsetParts.Count == 0)
            {
                continue;
            }

            var rel = relPath.Replace("\\", "/", StringComparison.Ordinal);
            data[rel.StartsWith("assets/", StringComparison.Ordinal) ? rel[7..] : rel] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["srcset"] = string.Join(", ", srcsetParts),
                ["sizes"] = existingSizes.ToArray(),
                ["url"] = $"/{rel}"
            };
        }

        return data;
    }

    private List<string> FindSourceImages(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> priorPluginOutputs,
        CancellationToken cancellationToken)
    {
        var assetsDir = Path.Combine(context.OutputDir, "assets");
        var mediaDir = Path.GetFullPath(Path.Combine(context.OutputDir,
            ResponsiveImageHtmlTransform.NormalizeMediaUrlBase(_config.Content.Media.UrlBase).TrimStart('/')));
        if (!IsWithinDirectory(context.OutputDir, mediaDir))
        {
            throw new IOException("Image media output directory escapes the output root.");
        }
        var imageDirectories = new[] { assetsDir, mediaDir }.Distinct(StringComparer.Ordinal)
            .Where(Directory.Exists).ToArray();
        foreach (var directory in imageDirectories)
        {
            CleanupOrphanedOwnedVariants(context.OutputDir, directory, priorPluginOutputs, cancellationToken);
        }

        var exts = new[] { ".jpg", ".jpeg", ".png" };
        return [.. imageDirectories.SelectMany(directory => SafeFileEnumerator.EnumerateFiles(directory, "*.*"))
            .Distinct(StringComparer.Ordinal)
            .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !IsOwnedGeneratedVariant(context.OutputDir, f, priorPluginOutputs))];
    }

    private void RewriteCurrentHtmlWithWebp(
        BuildContext context,
        IReadOnlyDictionary<string, string> webpSrcsets,
        CancellationToken cancellationToken)
    {
        if (!context.TryGet(BuildDataKeys.CurrentHtmlOutputs, out var htmlOutputs))
        {
            return;
        }

        foreach (var outputPath in htmlOutputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Path.GetExtension(outputPath).Equals(".html", StringComparison.OrdinalIgnoreCase) ||
                !TryResolveRelativeIdentity(context.OutputDir, outputPath.Replace('\\', '/'), out var htmlFile) ||
                !File.Exists(htmlFile))
            {
                continue;
            }

            var html = File.ReadAllText(htmlFile);
            var rewritten = ResponsiveImageHtmlTransform.RewriteWebpPictures(
                html,
                webpSrcsets,
                context.BaseUrl,
                _config.Site.Url);
            if (string.Equals(html, rewritten, StringComparison.Ordinal))
            {
                continue;
            }

            var temporary = htmlFile + $".bukit-{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(temporary, rewritten);
                File.Move(temporary, htmlFile, overwrite: true);
            }
            finally
            {
                TryDelete(temporary);
            }
        }
    }
}
