using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class ImageProcessingPlugin : IBukitPlugin, IAfterBuildAsyncPlugin
{
    private readonly AppConfig _config;

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

        var exts = new[] { ".jpg", ".jpeg", ".png" };
        var sizes = config.Sizes ?? new[] { 480, 768, 1200 };
        var imageFiles = SafeFileEnumerator.EnumerateFiles(assetsDir, "*.*")
            .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .Where(f => !IsGeneratedSizedImage(f, sizes))
            .ToList();

        if (imageFiles.Count == 0)
        {
            return;
        }

        var quality = config.Quality > 0 ? config.Quality : 80;

        var tool = await FindResizeToolAsync(context.Logger, cancellationToken);
        if (tool is null)
        {
            context.Logger.Warn("event=image_processing.skip reason=no_tool message=Install ImageMagick (magick) for image resizing.");
            return;
        }

        var generatedOutputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Delete variants that no longer match configured sizes
            CleanupStaleVariants(imageFile, sizes);

            var sourceLastWrite = File.GetLastWriteTimeUtc(imageFile);

            foreach (var size in sizes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var baseName = Path.GetFileNameWithoutExtension(imageFile);
                var ext = Path.GetExtension(imageFile);
                var sizedFile = Path.Combine(Path.GetDirectoryName(imageFile)!, $"{baseName}-{size}w{ext}");

                // Skip if variant exists and source hasn't changed since it was generated
                if (File.Exists(sizedFile) && File.GetLastWriteTimeUtc(sizedFile) >= sourceLastWrite)
                {
                    generatedOutputs.Add(sizedFile);
                    continue;
                }

                try
                {
                    var temporarySizedFile = Path.Combine(
                        Path.GetDirectoryName(sizedFile)!,
                        $".{Path.GetFileNameWithoutExtension(sizedFile)}.bukit-{Guid.NewGuid():N}{ext}");
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
                        if (result.ExitCode == 0 && File.Exists(temporarySizedFile))
                        {
                            File.Move(temporarySizedFile, sizedFile, overwrite: true);
                            generatedOutputs.Add(sizedFile);
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
            context.Data["__plugin_outputs"] = generatedOutputs
                .Select(f => Path.GetRelativePath(context.OutputDir, f).Replace("\\", "/", StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
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

    private static bool IsGeneratedSizedImage(string path, IReadOnlyList<int> sizes)
    {
        var stem = Path.GetFileNameWithoutExtension(path);
        return sizes.Any(size => stem.EndsWith($"-{size}w", StringComparison.Ordinal));
    }

    private static void CleanupStaleVariants(string sourceFile, IReadOnlyList<int> currentSizes)
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
                && !currentSizeSet.Contains(parsedSize))
            {
                TryDelete(existingFile);
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
