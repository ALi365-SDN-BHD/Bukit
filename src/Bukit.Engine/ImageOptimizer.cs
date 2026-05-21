using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class ImageOptimizer
{
    internal static void OptimizeIfEnabled(string assetsDir, ImageOptimizationConfig? config, ILogger logger)
    {
        if (config is not { Enabled: true })
        {
            return;
        }

        var exts = new[] { ".jpg", ".jpeg", ".png" };
        var imageFiles = Directory.GetFiles(assetsDir, "*.*", SearchOption.AllDirectories)
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
                        ConvertToWebp(imageFile, outputFile, quality, logger);
                    }
                    else if (string.Equals(format, "avif", StringComparison.OrdinalIgnoreCase))
                    {
                        ConvertToAvif(imageFile, outputFile, quality, logger);
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

    private static void ConvertToWebp(string inputFile, string outputFile, int quality, ILogger logger)
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

        RunTool(toolPath, args, logger, inputFile);
    }

    private static void ConvertToAvif(string inputFile, string outputFile, int quality, ILogger logger)
    {
        var toolPath = FindImageTool();
        if (toolPath is null)
        {
            logger.Warn("event=image_optimize.skip reason=no_tool message=Install ImageMagick (magick) for AVIF conversion.");
            return;
        }

        var args = $"magick \"{inputFile}\" -quality {quality} \"{outputFile}\"";
        RunTool(toolPath, args, logger, inputFile);
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
                    process.WaitForExit(3000);
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

    private static void RunTool(string toolPath, string args, ILogger logger, string inputFile)
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

        process?.WaitForExit(10000);
        if (process?.ExitCode == 0)
        {
            logger.Info($"event=image_optimize.ok file={Path.GetFileName(inputFile)}");
        }
        else
        {
            var err = process?.StandardError.ReadToEnd();
            logger.Warn($"event=image_optimize.error file={Path.GetFileName(inputFile)} reason={err}");
        }
    }
}
