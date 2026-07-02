using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

using Bukit.Engine.Abstractions.Plugins;
namespace Bukit.Engine.Plugins.BuiltIn;

public sealed class ImageProcessingPlugin : IBukitPlugin, IAfterBuildPlugin
{
    public string Name => "image-processing";
    public string Version => "1.0.0";

    public void AfterBuild(BuildContext context)
    {
        var config = context.Config.Theme.Images;
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
        var imageFiles = Directory.GetFiles(assetsDir, "*.*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        if (imageFiles.Count == 0)
        {
            return;
        }

        var sizes = config.Sizes ?? new[] { 480, 768, 1200 };
        var quality = config.Quality > 0 ? config.Quality : 80;

        var tool = FindResizeTool();
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
                    var args = $"\"{imageFile}\" -resize {size}x -quality {quality} \"{sizedFile}\"";
                    using var process = Process.Start(new ProcessStartInfo
                    {
                        FileName = tool,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    process?.WaitForExit(10000);
                    if (process?.ExitCode == 0)
                    {
                        context.Logger.Info($"event=image_resize.ok file={Path.GetFileName(sizedFile)}");
                    }
                    else
                    {
                        var err = process?.StandardError.ReadToEnd();
                        context.Logger.Warn($"event=image_resize.error file={Path.GetFileName(imageFile)} reason={err}");
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

    private static string? FindResizeTool()
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
            catch
            {
            }
        }

        return null;
    }
}
