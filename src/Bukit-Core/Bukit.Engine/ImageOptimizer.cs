using System.Diagnostics;
using System.Threading;
using Bukit.Config;
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
        var toolPath = FindImageTool();
        if (toolPath is null)
        {
            logger.Warn("event=image_optimize.skip reason=no_tool message=Install cwebp (libwebp) or ImageMagick for WebP conversion.");
            return;
        }

        var args = toolPath.EndsWith("cwebp", StringComparison.OrdinalIgnoreCase)
            ? $"-q {quality} \"{inputFile}\" -o \"{outputFile}\""
            : $"magick \"{inputFile}\" -quality {quality} \"{outputFile}\"";

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        await RunTool(toolPath, args, logger, inputFile, linkedCts.Token);
    }

    private static async Task ConvertToAvif(string inputFile, string outputFile, int quality, ILogger logger, CancellationToken cancellationToken)
    {
        var toolPath = FindImageTool();
        if (toolPath is null)
        {
            logger.Warn("event=image_optimize.skip reason=no_tool message=Install ImageMagick (magick) for AVIF conversion.");
            return;
        }

        var args = $"magick \"{inputFile}\" -quality {quality} \"{outputFile}\"";
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        await RunTool(toolPath, args, logger, inputFile, linkedCts.Token);
    }

    private static string? FindImageTool()
    {
        foreach (var name in new[] { "cwebp", "magick", "convert" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = name == "cwebp" ? "-version" : "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process is not null)
                {
                    using var cts = new CancellationTokenSource(3000);
                    process.WaitForExitAsync(cts.Token).GetAwaiter().GetResult();
                    if (process.ExitCode == 0)
                    {
                        return name;
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static async Task RunTool(string toolPath, string args, ILogger logger, string inputFile, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = toolPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            logger.Warn($"event=image_optimize.error file={Path.GetFileName(inputFile)} reason=process_start_failed");
            return;
        }

        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode == 0)
        {
            logger.Info($"event=image_optimize.ok file={Path.GetFileName(inputFile)}");
        }
        else
        {
            var err = await stderrTask;
            logger.Warn($"event=image_optimize.error file={Path.GetFileName(inputFile)} reason={err}");
        }
    }
}
