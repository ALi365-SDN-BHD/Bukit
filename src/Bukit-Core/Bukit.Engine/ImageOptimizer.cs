using System.Diagnostics;
using System.Threading;
using Bukit.Config;
using Bukit.Content.Media;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ImageOptimizer
{
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

        foreach (var format in formats)
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

    private static async Task ConvertToWebp(string inputFile, string outputFile, int quality, ILogger logger, CancellationToken cancellationToken)
    {
        var tool = await FindImageToolAsync("webp", cancellationToken);
        if (tool is null)
        {
            logger.Warn("event=image_optimize.skip reason=no_tool message=Install cwebp (libwebp) or ImageMagick for WebP conversion.");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = tool.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (tool.Kind == ImageToolKind.Cwebp)
        {
            startInfo.ArgumentList.Add("-q");
            startInfo.ArgumentList.Add(quality.ToString());
            startInfo.ArgumentList.Add(inputFile);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputFile);
        }
        else
        {
            startInfo.ArgumentList.Add(inputFile);
            startInfo.ArgumentList.Add("-quality");
            startInfo.ArgumentList.Add(quality.ToString());
            startInfo.ArgumentList.Add(outputFile);
        }

        await RunTool(startInfo, logger, inputFile, outputFile, expectedOutputMime: "image/webp", cancellationToken);
    }

    private static async Task ConvertToAvif(string inputFile, string outputFile, int quality, ILogger logger, CancellationToken cancellationToken)
    {
        var tool = await FindImageToolAsync("avif", cancellationToken);
        if (tool is null)
        {
            logger.Warn("event=image_optimize.skip reason=no_tool message=Install ImageMagick (magick) for AVIF conversion.");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = tool.Path,
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

    private static async Task<ImageTool?> FindImageToolAsync(
        string format,
        CancellationToken cancellationToken = default)
    {
        ImageTool[] candidates = string.Equals(format, "webp", StringComparison.OrdinalIgnoreCase)
            ?
            [
                new ImageTool("cwebp", ImageToolKind.Cwebp),
                new ImageTool("magick", ImageToolKind.Magick),
                new ImageTool("convert", ImageToolKind.Convert)
            ]
            :
            [
                new ImageTool("magick", ImageToolKind.Magick),
                new ImageTool("convert", ImageToolKind.Convert)
            ];

        foreach (var candidate in candidates)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = await ExternalToolProcessRunner.RunAsync(new ProcessStartInfo
                {
                    FileName = candidate.Path,
                    Arguments = candidate.Kind == ImageToolKind.Cwebp ? "-version" : "--version",
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

    private enum ImageToolKind
    {
        Cwebp,
        Magick,
        Convert
    }

    private sealed record ImageTool(string Path, ImageToolKind Kind);
}
