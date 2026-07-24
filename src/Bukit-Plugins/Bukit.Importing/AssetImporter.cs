using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class AssetImporter
{
    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".key", ".pem", ".pfx", ".p12", ".crt", ".cert"
    };

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", ".vscode", "dist", "build", ".npmrc"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico", ".bmp"
    };

    private static readonly HashSet<string> HtmlExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm"
    };

    internal sealed record AssetImportResult(
        int Count,
        List<string> Warnings,
        Dictionary<string, string> PathMappings);

    internal static AssetImportResult Import(
        HtmlDemoImportOptions options, List<DiscoveredPage> pages)
    {
        var themeDir = HtmlDemoImporter.GetThemeDir(options);
        var count = 0;
        var warnings = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            foreach (var assetPath in page.AssetPaths)
            {
                if (!seen.Add(assetPath)) continue;

                if (!IsSafeAsset(assetPath))
                {
                    warnings.Add($"Skipped sensitive file: {assetPath}");
                    continue;
                }

                if (HtmlExtensions.Contains(Path.GetExtension(assetPath)))
                    continue;

                var sourcePath = Path.GetFullPath(
                    Path.Combine(options.InputPath, assetPath.TrimStart('/')));

                var fullInputPath = Path.GetFullPath(options.InputPath);
                if (!sourcePath.StartsWith(fullInputPath + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(sourcePath, fullInputPath, StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"Path traversal rejected: {assetPath}");
                    continue;
                }

                if (!File.Exists(sourcePath)) continue;

                var isImage = ImageExtensions.Contains(
                    Path.GetExtension(assetPath).ToLowerInvariant());
                var destSubDir = isImage ? "assets" : "static";
                var destPath = Path.Combine(themeDir, destSubDir,
                    assetPath.TrimStart('/'));

                var destDir = Path.GetDirectoryName(destPath);
                if (destDir is not null)
                    Directory.CreateDirectory(destDir);

                File.Copy(sourcePath, destPath, overwrite: true);

                var destRel = "/" + assetPath.TrimStart('/').Replace('\\', '/');
                mappings[assetPath] = destRel;
                count++;
            }
        }

        var sourceAssetsDir = Path.Combine(options.InputPath, "assets");
        if (Directory.Exists(sourceAssetsDir))
        {
            foreach (var file in Directory.GetFiles(sourceAssetsDir, "*.*", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(file);
                if (SensitiveExtensions.Contains(Path.GetExtension(fileName)) ||
                    SensitiveNames.Contains(fileName))
                    continue;

                var rel = Path.GetRelativePath(sourceAssetsDir, file);
                var dest = Path.Combine(themeDir, "assets", rel);
                if (!File.Exists(dest))
                {
                    var destDir = Path.GetDirectoryName(dest);
                    if (destDir is not null)
                        Directory.CreateDirectory(destDir);
                    File.Copy(file, dest);

                    var origKey = "assets/" + rel.Replace('\\', '/');
                    mappings[origKey] = "/assets/" + rel.Replace('\\', '/');
                    count++;
                }
            }
        }

        return new AssetImportResult(count, warnings, mappings);
    }

    internal static void TransferAssetsToStatic(HtmlDemoImportOptions options)
    {
        var themeBase = HtmlDemoImporter.GetThemeDir(options);
        var themeAssetsDir = Path.Combine(themeBase, "assets");
        var themeStaticDir = Path.Combine(themeBase, "static");

        if (!Directory.Exists(themeAssetsDir))
            return;

        if (!Directory.Exists(themeStaticDir))
            Directory.CreateDirectory(themeStaticDir);

        foreach (var file in Directory.GetFiles(themeAssetsDir, "*.*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(themeAssetsDir, file);
            var dest = Path.Combine(themeStaticDir, rel);
            if (!File.Exists(dest))
            {
                var destDir = Path.GetDirectoryName(dest);
                if (destDir is not null)
                    Directory.CreateDirectory(destDir);
                File.Move(file, dest);
            }
            else
            {
                // Destination already exists (Import wrote to static/assets/...).
                // Only delete source if the destination is valid (non-zero size).
                var destInfo = new FileInfo(dest);
                if (destInfo.Length > 0)
                {
                    File.Delete(file);
                }
            }
        }
    }

    internal static string RewritePaths(string content, Dictionary<string, string> mappings)
    {
        if (mappings.Count == 0) return content;

        var sortedKeys = mappings.Keys.OrderByDescending(k => k.Length).ToList();
        foreach (var key in sortedKeys)
        {
            var replacement = mappings[key];
            content = content.Replace($"\"{key}\"", $"\"{replacement}\"", StringComparison.OrdinalIgnoreCase);
            content = content.Replace($"'{key}'", $"'{replacement}'", StringComparison.OrdinalIgnoreCase);
        }
        return content;
    }

    private static bool IsSafeAsset(string assetPath)
    {
        if (assetPath.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileName = Path.GetFileName(assetPath);
        if (SensitiveExtensions.Contains(Path.GetExtension(fileName)))
            return false;
        if (SensitiveNames.Contains(fileName))
            return false;

        return true;
    }
}
