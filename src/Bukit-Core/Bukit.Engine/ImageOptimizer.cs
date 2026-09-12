using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Bukit.Config;
using Bukit.Content.Media;
using Bukit.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace Bukit.Engine;

internal static class ImageOptimizer
{
    private static readonly string s_webpEncoderIdentity = "imagesharp-webp-" + (
        typeof(WebpEncoder).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ??
        typeof(WebpEncoder).Assembly.GetName().Version?.ToString() ??
        "unknown");

    internal static async Task OptimizeIfEnabled(string assetsDir, ImageOptimizationConfig? config, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (config is not { Enabled: true })
        {
            return;
        }

        var exts = new[] { ".jpg", ".jpeg", ".png" };
        var imageFiles = SafeFileEnumerator.EnumerateFiles(assetsDir, "*.*")
            .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count == 0)
        {
            return;
        }

        var formats = config.Formats ?? new[] { "webp" };
        var quality = config.Quality > 0 ? config.Quality : 80;

        foreach (var format in formats.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(format, "avif", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var imageFile in imageFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var outputFile = Path.ChangeExtension(imageFile, $".{format}");
                    if (File.Exists(outputFile))
                    {
                        continue;
                    }

                    if (string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase))
                    {
                        await ConvertToWebp(imageFile, outputFile, quality, logger, cancellationToken);
                    }
                    else if (string.Equals(format, "avif", StringComparison.OrdinalIgnoreCase))
                    {
                        await ConvertToAvif(imageFile, outputFile, quality, logger, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Warn($"event=image_optimize.error file={Path.GetFileName(imageFile)} format={format} reason={ex.Message}");
                }
            }
        }
    }

    public static string BuildSrcset(string baseName, IReadOnlyList<int> sizes, string format)
    {
        var parts = new List<string>();
        var ext = $".{format.TrimStart('.')}";
        foreach (var size in sizes)
        {
            var sizedFile = $"{baseName}-{size}w{ext}";
            parts.Add($"{sizedFile} {size}w");
        }

        return string.Join(", ", parts);
    }

    internal static string WebpEncoderIdentity => s_webpEncoderIdentity;

    internal static async Task EncodeWebpAsync(
        string inputFile,
        string outputFile,
        int? width,
        int quality,
        CancellationToken cancellationToken)
    {
        if (width is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        try
        {
            using var image = await Image.LoadAsync(inputFile, cancellationToken);
            if (width is { } targetWidth && targetWidth < image.Width)
            {
                image.Mutate(operation => operation.Resize(targetWidth, 0));
            }

            await image.SaveAsWebpAsync(
                outputFile,
                new WebpEncoder { Quality = Math.Clamp(quality, 1, 100) },
                cancellationToken);

            if (!await new ImageContentValidator().ValidateAsync(outputFile, "image/webp", cancellationToken))
            {
                throw new InvalidDataException("ImageSharp produced an invalid WebP output.");
            }
        }
        catch
        {
            TryDelete(outputFile);
            throw;
        }
    }

    private static async Task ConvertToWebp(string inputFile, string outputFile, int quality, ILogger logger, CancellationToken cancellationToken)
    {
        var temporaryOutput = Path.Combine(
            Path.GetDirectoryName(outputFile)!,
            $".{Path.GetFileNameWithoutExtension(outputFile)}.bukit-{Guid.NewGuid():N}{Path.GetExtension(outputFile)}");
        try
        {
            await EncodeWebpAsync(inputFile, temporaryOutput, width: null, quality, cancellationToken);
            File.Move(temporaryOutput, outputFile, overwrite: true);
            logger.Info($"event=image_optimize.ok file={Path.GetFileName(inputFile)}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Warn($"event=image_optimize.error file={Path.GetFileName(inputFile)} reason=encode_failed type={ex.GetType().Name}");
        }
        finally
        {
            TryDelete(temporaryOutput);
        }
    }

    private static async Task ConvertToAvif(string inputFile, string outputFile, int quality, ILogger logger, CancellationToken cancellationToken)
    {
        var tool = await FindImageToolAsync(cancellationToken);
        if (tool is null)
        {
            logger.Warn("event=image_optimize.skip reason=no_tool message=Install ImageMagick (magick) for AVIF conversion.");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = tool,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(inputFile);
        startInfo.ArgumentList.Add("-quality");
        startInfo.ArgumentList.Add(quality.ToString());
        startInfo.ArgumentList.Add(outputFile);
        // The pinned decoder set has no approved AVIF decoder, so AVIF converter
        // output cannot be proven valid: fail closed instead of publishing it.
        await RunTool(startInfo, logger, inputFile, outputFile, expectedOutputMime: null, cancellationToken);
    }

    private static async Task<string?> FindImageToolAsync(CancellationToken cancellationToken = default)
    {
        foreach (var candidate in new[] { "magick", "convert" })
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await ExternalToolProcessRunner.RunAsync(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }, TimeSpan.FromSeconds(3), cancellationToken);
                if (result.ExitCode == 0)
                {
                    return candidate;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
            }
        }

        return null;
    }

    private static async Task RunTool(
        ProcessStartInfo startInfo,
        ILogger logger,
        string inputFile,
        string outputFile,
        string? expectedOutputMime,
        CancellationToken cancellationToken)
    {
        var temporaryOutput = Path.Combine(
            Path.GetDirectoryName(outputFile)!,
            $".{Path.GetFileNameWithoutExtension(outputFile)}.bukit-{Guid.NewGuid():N}{Path.GetExtension(outputFile)}");
        startInfo.ArgumentList[^1] = temporaryOutput;
        try
        {
            var result = await ExternalToolProcessRunner.RunAsync(
                startInfo,
                TimeSpan.FromSeconds(10),
                cancellationToken);
            var validationAttempted = result.ExitCode == 0 && File.Exists(temporaryOutput);
            var outputIsValid = validationAttempted &&
                await ValidateConverterOutputAsync(temporaryOutput, expectedOutputMime, logger, inputFile, cancellationToken);
            if (outputIsValid)
            {
                File.Move(temporaryOutput, outputFile, overwrite: true);
                logger.Info($"event=image_optimize.ok file={Path.GetFileName(inputFile)}");
            }
            else
            {
                var reason = validationAttempted
                    ? "output_validation_failed"
                    : result.ExitCode == 0
                        ? "output_missing"
                        : string.IsNullOrWhiteSpace(result.StandardError)
                            ? $"tool_failed_exit_{result.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture)}"
                            : result.StandardError;
                logger.Warn($"event=image_optimize.error file={Path.GetFileName(inputFile)} reason={reason}");
            }
        }
        finally
        {
            TryDelete(temporaryOutput);
        }
    }

    private static async Task<bool> ValidateConverterOutputAsync(
        string outputPath,
        string? expectedOutputMime,
        ILogger logger,
        string inputFile,
        CancellationToken cancellationToken)
    {
        if (expectedOutputMime is null)
        {
            logger.Warn(
                $"event=image_optimize.unverifiable file={Path.GetFileName(inputFile)} reason=no_approved_decoder");
            return false;
        }

        try
        {
            return await new ImageContentValidator().ValidateAsync(outputPath, expectedOutputMime, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.Warn(
                $"event=image_optimize.validation_failed file={Path.GetFileName(inputFile)} reason={ex.GetType().Name}");
            return false;
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
