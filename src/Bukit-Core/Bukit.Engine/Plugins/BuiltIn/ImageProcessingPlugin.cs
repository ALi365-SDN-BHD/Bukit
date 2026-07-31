using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

internal sealed class ImageProcessingPlugin : IBukitPlugin, IAfterBuildPlugin
{
    private readonly AppConfig _config;

    internal ImageProcessingPlugin(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
    }

    public string Name => "image-processing";
    public string Version => "1.0.0";

    public void AfterBuild(BuildContext context)
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
        var imageFiles = SafeFileEnumerator.EnumerateFiles(assetsDir, "*.*")
            .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count == 0)
        {
            return;
        }

        var sizes = config.Sizes ?? new[] { 480, 768, 1200 };
        var quality = config.Quality > 0 ? config.Quality : 80;

        var tool = FindResizeTool(context.Logger);
        if (tool is null)
        {
            context.Logger.Warn("event=image_processing.skip reason=no_tool message=Install ImageMagick (magick) for image resizing.");
            return;
        }

        foreach (var imageFile in imageFiles)
        {
            foreach (var size in sizes)
            {
                var baseName = Path.GetFileNameWithoutExtension(imageFile);
                var ext = Path.GetExtension(imageFile);
                var sizedFile = Path.Combine(Path.GetDirectoryName(imageFile)!, $"{baseName}-{size}w{ext}");
                if (File.Exists(sizedFile))
                {
                    continue;
                }

                try
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
                    startInfo.ArgumentList.Add(sizedFile);
                    using var process = Process.Start(startInfo);

                    if (process is not null && process.WaitForExit(10000))
                    {
                        if (process.ExitCode == 0)
                        {
                            context.Logger.Info($"event=image_resize.ok file={Path.GetFileName(sizedFile)}");
                        }
                        else
                        {
                            var err = process.StandardError.ReadToEnd();
                            context.Logger.Warn($"event=image_resize.error file={Path.GetFileName(imageFile)} reason={err}");
                        }
                    }
                    else if (process is not null)
                    {
                        process.Kill(entireProcessTree: true);
                        context.Logger.Warn($"event=image_resize.error file={Path.GetFileName(imageFile)} reason=timeout");
                    }
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
            foreach (var size in sizes)
            {
                var sizedRel = Path.Combine(Path.GetDirectoryName(relPath) ?? "", $"{baseName}-{size}w{ext}")
                    .Replace("\\", "/", StringComparison.Ordinal);
                srcsetParts.Add($"/assets/{sizedRel} {size}w");
            }

            var rel = relPath.Replace("\\", "/", StringComparison.Ordinal);
            data[rel] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["srcset"] = string.Join(", ", srcsetParts),
                ["sizes"] = sizes,
                ["url"] = $"/assets/{rel}"
            };
        }

        if (data.Count > 0)
        {
            context.Data["__image_srcsets"] = data;
        }
    }

    private static string? FindResizeTool(ILogger? logger = null)
    {
        foreach (var name in new[] { "magick", "convert" })
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
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
            catch (Exception ex)
            {
                logger?.Warn($"event=image_processing.tool.probe.failed tool={name} reason={ex.Message}");
            }
        }

        return null;
    }
}
