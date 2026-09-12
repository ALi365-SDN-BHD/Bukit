using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Bukit.Config;
using Bukit.Content.Media;
using Bukit.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed partial class ImageProcessingPlugin : IBukitPlugin, IAfterBuildAsyncPlugin, IHtmlTransformPlugin
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

    public IHtmlTransform CreateHtmlTransform(HtmlTransformPluginContext context)
    {
        context.BuildContext.Data.TryGetValue(BuildContextDataKeys.MediaDownloadDir, out var mediaDownloadDir);
        return new ResponsiveImageHtmlTransform(
            _config,
            mediaDownloadDir as string,
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

        var resizePlans = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        var sourceWidths = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasWidth = TryGetImageWidth(imageFile, context.Logger, out var width);
            var applicableSizes = hasWidth ? sizes.Where(size => size < width).ToArray() : Array.Empty<int>();
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

        var quality = config.Quality > 0 ? config.Quality : 80;
        string? tool = null;
        if (resizePlans.Count > 0)
        {
            tool = await FindResizeToolAsync(context.Logger, cancellationToken);
            if (tool is null)
            {
                context.Logger.Info("event=image_processing.fallback tool=imagesharp");
            }
        }
        var toolIdentity = tool ?? "imagesharp";

        foreach (var (imageFile, applicableSizes) in resizePlans)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceInfo = new FileInfo(imageFile);
            var sourceSha256 = await ComputeSha256Async(imageFile, cancellationToken);

            foreach (var size in applicableSizes)
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
                    (int ExitCode, string StandardError) result;
                    if (tool is null)
                    {
                        await ResizeWithImageSharpAsync(
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
                        startInfo.ArgumentList.Add("-resize");
                        startInfo.ArgumentList.Add($"{size}x");
                        startInfo.ArgumentList.Add("-quality");
                        startInfo.ArgumentList.Add(quality.ToString());
                        startInfo.ArgumentList.Add(temporarySizedFile);
                        var externalResult = await ExternalToolProcessRunner.RunAsync(
                            startInfo,
                            TimeSpan.FromSeconds(10),
                            cancellationToken);
                        result = (externalResult.ExitCode, externalResult.StandardError);
                    }
                    try
                    {
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
        }

        if (webpEnabled)
        {
            var webpSrcsets = await GenerateWebpVariantsAsync(
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

        var data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var imageFile in imageFiles)
        {
            var relPath = Path.GetRelativePath(context.OutputDir, imageFile);
            var baseName = Path.GetFileNameWithoutExtension(imageFile);
            var ext = Path.GetExtension(imageFile);

            var srcsetParts = new List<string>();
            var existingSizes = new List<int>();
            foreach (var size in resizePlans.GetValueOrDefault(imageFile) ?? Array.Empty<int>())
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

        if (data.Count > 0)
        {
            context.Data["__image_srcsets"] = data;
        }

        SetTrackedOutputs(context, generatedOutputs);
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

    private const string FreshnessSuffix = ".bukit-freshness.json";
    private const int FreshnessSchemaVersion = 1;
    private const string FreshnessOwner = "bukit:image-processing";

    private static readonly int[] DefaultSizes = [480, 768, 1200];

    private static IReadOnlyList<int> NormalizeSizes(IReadOnlyList<int>? sizes) =>
        [.. (sizes ?? DefaultSizes).Where(size => size > 0).Distinct().Order()];

    private static bool TryGetImageWidth(string path, ILogger logger, out int width)
    {
        width = 0;
        try
        {
            width = ImageMetadataReader.TryReadImageMetadata(path)?.Width ?? 0;
            return width > 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.Warn($"event=image_processing.skip file={Path.GetFileName(path)} reason=invalid_source");
            return false;
        }
    }

    private static async Task ResizeWithImageSharpAsync(
        string source,
        string destination,
        int width,
        int quality,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(source, cancellationToken);
        image.Mutate(operation => operation.Resize(width, 0));
        if (Path.GetExtension(destination).Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(destination).Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            await image.SaveAsJpegAsync(destination, new JpegEncoder { Quality = quality }, cancellationToken);
            return;
        }

        await image.SaveAsPngAsync(destination, cancellationToken);
    }

    private async Task<Dictionary<string, string>> GenerateWebpVariantsAsync(
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

    private void RewriteCurrentHtmlWithWebp(
        BuildContext context,
        IReadOnlyDictionary<string, string> webpSrcsets,
        CancellationToken cancellationToken)
    {
        if (!context.Data.TryGetValue(BuildContextDataKeys.CurrentHtmlOutputs, out var value) ||
            value is not IReadOnlyList<string> htmlOutputs)
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

    private sealed partial class ResponsiveImageHtmlTransform(
        AppConfig config,
        string? mediaDownloadDir,
        string baseUrl,
        ILogger logger) : IHtmlTransform
    {
        [GeneratedRegex(
            """<!--[\s\S]*?-->|<(?:script|style|template)\b(?:[^>"']|"[^"]*"|'[^']*')*>[\s\S]*?</(?:script|style|template)\s*>|<picture\b(?:[^>"']|"[^"]*"|'[^']*')*>[\s\S]*?</picture\s*>|<img(?=[\s/>])(?:[^>"']|"[^"]*"|'[^']*')*>""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex HtmlElementRegex();
        [GeneratedRegex(
            """(?<name>[^\s=/>]+)(?:\s*=\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<unquoted>[^\s>]+)))?""",
            RegexOptions.CultureInvariant)]
        private static partial Regex AttributeRegex();
        private readonly IReadOnlyList<int> _sizes = NormalizeSizes(config.Theme.Images?.Sizes);
        private readonly string _baseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
        private readonly string _mediaUrlBase = NormalizeMediaUrlBase(config.Content.Media.UrlBase);
        private readonly Uri? _siteUri = Uri.TryCreate(config.Site.Url, UriKind.Absolute, out var siteUri) ? siteUri : null;
        private readonly bool _webpEnabled = config.Theme.Images?.Formats?.Any(format =>
            string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase)) == true;

        private const string GeneratedSrcsetMarker = "data-bukit-generated-srcset";

        public string Name => "responsive-images";

        public string Transform(HtmlTransformContext context, string html)
        {
            if (config.Theme.Images is not { Enabled: true } ||
                string.IsNullOrWhiteSpace(mediaDownloadDir) ||
                !Directory.Exists(mediaDownloadDir))
            {
                return html;
            }

            return HtmlElementRegex().Replace(html, match =>
                match.Value.StartsWith("<img", StringComparison.OrdinalIgnoreCase)
                    ? RewriteImageTag(match.Value)
                    : match.Value);
        }

        internal static string RewriteWebpPictures(
            string html,
            IReadOnlyDictionary<string, string> webpSrcsets,
            string baseUrl,
            string? siteUrl)
        {
            var normalizedBaseUrl = BuildPathUtils.NormalizeBaseUrl(baseUrl).TrimEnd('/');
            var siteUri = Uri.TryCreate(siteUrl, UriKind.Absolute, out var parsedSiteUri) ? parsedSiteUri : null;
            return HtmlElementRegex().Replace(html, match =>
            {
                if (!match.Value.StartsWith("<img", StringComparison.OrdinalIgnoreCase))
                {
                    return match.Value;
                }

                var attributes = AttributeRegex().Matches(match.Value[4..^1]);
                var hasGeneratedSrcset = attributes.Any(attribute =>
                    attribute.Groups["name"].Value.Equals(GeneratedSrcsetMarker, StringComparison.OrdinalIgnoreCase));
                if (!hasGeneratedSrcset && attributes.Any(attribute =>
                    attribute.Groups["name"].Value.Equals("srcset", StringComparison.OrdinalIgnoreCase)))
                {
                    return match.Value;
                }

                var fallbackTag = hasGeneratedSrcset
                    ? match.Value.Replace($" {GeneratedSrcsetMarker}=\"\"", string.Empty, StringComparison.Ordinal)
                    : match.Value;
                var src = GetAttributeValue(attributes, "src");
                if (src is null || !TryNormalizeLocalImageUrl(src, normalizedBaseUrl, siteUri, out var sourceUrl) ||
                    !webpSrcsets.TryGetValue(sourceUrl, out var webpSrcset))
                {
                    return fallbackTag;
                }

                var sizes = GetAttributeValue(attributes, "sizes");
                var source = $"<source type=\"image/webp\" srcset=\"{WebUtility.HtmlEncode(webpSrcset)}\"" +
                    (sizes is null ? ">" : $" sizes=\"{WebUtility.HtmlEncode(sizes)}\">");
                return $"<picture>{source}{fallbackTag}</picture>";
            });
        }

        private static string? GetAttributeValue(MatchCollection attributes, string name)
        {
            var match = attributes.FirstOrDefault(attribute =>
                attribute.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return null;
            }

            return WebUtility.HtmlDecode(
                match.Groups["double"].Success ? match.Groups["double"].Value :
                match.Groups["single"].Success ? match.Groups["single"].Value :
                match.Groups["unquoted"].Value);
        }

        private static bool TryNormalizeLocalImageUrl(
            string sourceUrl,
            string baseUrl,
            Uri? siteUri,
            out string normalized)
        {
            normalized = string.Empty;
            var path = sourceUrl;
            if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https")
            {
                if (siteUri is null ||
                    !string.Equals(siteUri.GetLeftPart(UriPartial.Authority), absolute.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                path = absolute.AbsolutePath;
            }
            else if (sourceUrl.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            var suffixAt = path.IndexOfAny(['?', '#']);
            if (suffixAt >= 0)
            {
                path = path[..suffixAt];
            }
            if (!path.StartsWith("/", StringComparison.Ordinal) ||
                Path.GetExtension(path).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
            {
                return false;
            }

            normalized = path;
            if (baseUrl.Length > 0 && !normalized.StartsWith(baseUrl + "/", StringComparison.Ordinal))
            {
                return false;
            }
            return true;
        }

        private string RewriteImageTag(string tag)
        {
            var parsedAttributes = AttributeRegex().Matches(tag[4..^1]);
            var srcMatch = parsedAttributes.FirstOrDefault(attribute =>
                attribute.Groups["name"].Value.Equals("src", StringComparison.OrdinalIgnoreCase));
            if (srcMatch is null)
            {
                return tag;
            }

            var sourceUrl = WebUtility.HtmlDecode(
                srcMatch.Groups["double"].Success ? srcMatch.Groups["double"].Value :
                srcMatch.Groups["single"].Success ? srcMatch.Groups["single"].Value :
                srcMatch.Groups["unquoted"].Value);
            if (!TryResolveMediaSource(sourceUrl, out var sourceFile) ||
                !TryGetImageWidth(sourceFile, logger, out var sourceWidth))
            {
                return tag;
            }

            var attributes = string.Empty;
            var applicableSizes = _sizes.Where(size => size < sourceWidth).ToArray();
            if (applicableSizes.Length > 0 && !parsedAttributes.Any(attribute => attribute.Groups["name"].Value.Equals("srcset", StringComparison.OrdinalIgnoreCase)))
            {
                var candidates = applicableSizes
                    .Select(size => $"{BuildVariantUrl(sourceUrl, size)} {size}w")
                    .Append($"{sourceUrl} {sourceWidth}w");
                attributes += $" srcset=\"{WebUtility.HtmlEncode(string.Join(", ", candidates))}\"";
                if (_webpEnabled)
                {
                    attributes += $" {GeneratedSrcsetMarker}=\"\"";
                }
            }
            if (!parsedAttributes.Any(attribute => attribute.Groups["name"].Value.Equals("decoding", StringComparison.OrdinalIgnoreCase)))
            {
                attributes += " decoding=\"async\"";
            }

            if (attributes.Length == 0)
            {
                return tag;
            }

            var insertAt = tag.LastIndexOf('>');
            if (insertAt > 0 && tag[insertAt - 1] == '/')
            {
                insertAt--;
            }
            return tag.Insert(insertAt, attributes);
        }

        private bool TryResolveMediaSource(string sourceUrl, out string sourceFile)
        {
            sourceFile = string.Empty;
            var path = sourceUrl;
            if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var absolute) &&
                absolute.Scheme is "http" or "https")
            {
                if (_siteUri is null ||
                    !string.Equals(_siteUri.GetLeftPart(UriPartial.Authority), absolute.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                path = absolute.AbsolutePath;
            }

            var prefix = _baseUrl + _mediaUrlBase;
            if (!path.StartsWith(prefix + '/', StringComparison.Ordinal))
            {
                return false;
            }

            string relative;
            try
            {
                relative = Uri.UnescapeDataString(path[(prefix.Length + 1)..]);
            }
            catch (UriFormatException)
            {
                return false;
            }
            if (relative.Contains('\\'))
            {
                return false;
            }

            var candidate = Path.GetFullPath(Path.Combine(
                mediaDownloadDir!,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinDirectory(mediaDownloadDir!, candidate) ||
                !File.Exists(candidate) ||
                Path.GetExtension(candidate).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png"))
            {
                return false;
            }

            sourceFile = candidate;
            return true;
        }

        internal static string NormalizeMediaUrlBase(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? "/assets/uploads" : value.Trim();
            if (!normalized.StartsWith('/'))
            {
                normalized = "/" + normalized;
            }
            return normalized.TrimEnd('/');
        }

        private static string BuildVariantUrl(string sourceUrl, int width)
        {
            var extensionAt = sourceUrl.LastIndexOf('.');
            return $"{sourceUrl[..extensionAt]}-{width}w{sourceUrl[extensionAt..]}";
        }
    }

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

    private static void PreservePriorWebpOutputs(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        HashSet<PluginOutputTrackingInfo> generatedOutputs)
    {
        foreach (var output in priorOutputs.Where(output =>
            output.Path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
            output.Path.EndsWith($".webp{FreshnessSuffix}", StringComparison.OrdinalIgnoreCase)))
        {
            if (TryResolveRelativeIdentity(context.OutputDir, output.Path, out var path) && File.Exists(path))
            {
                generatedOutputs.Add(output);
            }
        }
    }

    private static void SetTrackedOutputs(
        BuildContext context,
        HashSet<PluginOutputTrackingInfo> generatedOutputs)
    {
        if (generatedOutputs.Count > 0)
        {
            context.Data["__plugin_outputs"] = generatedOutputs;
        }
        else
        {
            context.Data.Remove("__plugin_outputs");
        }
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
        // Filename shape only. Generated identity itself must be proven by the
        // ownership/freshness manifest, see IsOwnedGeneratedVariant.
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

    private static bool IsOwnedGeneratedVariant(
        string outputDir,
        string path,
        HashSet<PluginOutputTrackingInfo> priorOutputs)
    {
        // A *-<digits>w file is only a generated variant when the ownership manifest
        // proves it. User source files with the same name shape remain inputs.
        if (!IsGeneratedSizedImage(path))
        {
            return false;
        }

        var sidecarFile = path + FreshnessSuffix;
        if (!HasPriorOwnership(outputDir, priorOutputs, path, sidecarFile) ||
            !TryReadFreshnessRecord(sidecarFile, out var freshness) ||
            !IsExpectedVariantPath(path, freshness.Size, freshness.Format) ||
            !MatchesVariantIdentity(freshness, path))
        {
            return false;
        }

        try
        {
            return string.Equals(
                freshness.VariantPath,
                GetRelativeIdentity(outputDir, path),
                StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
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
        HashSet<PluginOutputTrackingInfo> priorOutputs,
        string? variantExtension = null)
    {
        var dir = Path.GetDirectoryName(sourceFile)!;
        var baseName = Path.GetFileNameWithoutExtension(sourceFile);
        var ext = variantExtension ?? Path.GetExtension(sourceFile);
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
