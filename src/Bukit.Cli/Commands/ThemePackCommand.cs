using System.Formats.Tar;
using System.IO.Compression;
using Bukit.Cli.Cli.Binding;

namespace Bukit.Cli.Commands;

public static class ThemePackCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var name = command.GetArgument(1);
        if (string.IsNullOrWhiteSpace(name))
        {
            var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
            var rootDir = resolved.RootDir;
            name = ResolveActiveThemeName(resolved);
        }

        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('-'))
        {
            Console.Error.WriteLine("Missing theme name. Usage: bukit theme pack <name>");
            return 2;
        }

        var resolvedFinal = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));
        var rootDirFinal = resolvedFinal.RootDir;
        var themeRoot = Path.Combine(rootDirFinal, "themes", name);

        if (!Directory.Exists(themeRoot))
        {
            Console.Error.WriteLine($"Theme not found: {name}");
            return 2;
        }

        var manifest = ThemeManifest.Load(themeRoot);
        var version = manifest?.Version ?? "0.0.0";
        var outputName = $"{name}-{version}.tar.gz";
        var outputPath = command.GetString("--output") ?? outputName;
        if (!outputPath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            outputPath = outputPath + ".tar.gz";
        }

        var fullOutputPath = Path.GetFullPath(outputPath);

        try
        {
            var fileCount = 0;
            using var fs = File.Create(fullOutputPath);
            using var gzip = new GZipStream(fs, CompressionLevel.Optimal);
            using var writer = new TarWriter(gzip);

            var entries = Directory.GetFiles(themeRoot, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var file in entries)
            {
                var relativePath = Path.GetRelativePath(themeRoot, file);
                var entryPath = relativePath.Replace('\\', '/');

                await writer.WriteEntryAsync(file, entryPath);
                fileCount++;
            }

            var fileSize = new FileInfo(fullOutputPath).Length;
            Console.WriteLine($"Packed: {Path.GetFileName(fullOutputPath)}  ({FormatSize(fileSize)})");
            Console.WriteLine($"  {fileCount} files from themes/{name}/");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to pack theme: {ex.Message}");
            try { File.Delete(fullOutputPath); } catch { }
            return 1;
        }

        return 0;
    }

    private static string? ResolveActiveThemeName(ResolvedConfigPath resolved)
    {
        if (!File.Exists(resolved.FullConfigPath))
        {
            return null;
        }

        try
        {
            var yaml = File.ReadAllText(resolved.FullConfigPath);
            var stream = new YamlDotNet.RepresentationModel.YamlStream();
            stream.Load(new StringReader(yaml));
            if (stream.Documents.Count > 0 &&
                stream.Documents[0].RootNode is YamlDotNet.RepresentationModel.YamlMappingNode root &&
                root.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode("theme"), out var themeNode) &&
                themeNode is YamlDotNet.RepresentationModel.YamlMappingNode themeMap &&
                themeMap.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode("name"), out var nameNode) &&
                nameNode is YamlDotNet.RepresentationModel.YamlScalarNode nameScalar)
            {
                return nameScalar.Value;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }
}
