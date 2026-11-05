using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Importing;

internal static partial class AssetImporter
{
    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".key", ".pem", ".pfx", ".p12", ".crt", ".cert"
    };

    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", ".vscode", ".npmrc"
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

        var fullInputPath = Path.GetFullPath(options.InputPath);

        foreach (var page in pages)
        {
            var pageDir = Path.GetDirectoryName(page.RelativePath.Replace('\\', '/')) ?? "";

            foreach (var rawAssetPath in page.AssetPaths)
            {
                var assetPath = StripQueryAndFragment(rawAssetPath);
                if (string.IsNullOrWhiteSpace(assetPath)) continue;

                if (!IsSafeAsset(assetPath))
                {
                    warnings.Add($"Skipped sensitive file: {assetPath}");
                    continue;
                }

                if (HtmlExtensions.Contains(Path.GetExtension(assetPath)))
                    continue;

                // Relative asset paths are authored relative to the page, not the demo root.
                var sourcePath = Path.GetFullPath(assetPath.StartsWith('/')
                    ? Path.Combine(options.InputPath, assetPath.TrimStart('/'))
                    : Path.Combine(options.InputPath, pageDir, assetPath));

                if (!PathUtils.IsSameOrSubPathOf(sourcePath, fullInputPath))
                {
                    warnings.Add($"Path traversal rejected: {assetPath}");
                    continue;
                }

                if (!File.Exists(sourcePath)) continue;

                var canonicalRel = Path.GetRelativePath(fullInputPath, sourcePath).Replace('\\', '/');
                if (canonicalRel.StartsWith("../", StringComparison.Ordinal)) continue;

                var destUrl = "/" + canonicalRel;
                if (!mappings.TryAdd(assetPath, destUrl) && mappings[assetPath] != destUrl)
                {
                    warnings.Add(
                        $"Asset reference '{assetPath}' resolves to multiple files; kept '{mappings[assetPath]}'.");
                }

                if (!seen.Add(canonicalRel)) continue;

                var isImage = ImageExtensions.Contains(
                    Path.GetExtension(canonicalRel).ToLowerInvariant());
                var destSubDir = isImage ? "assets" : "static";
                var destPath = Path.Combine(themeDir, destSubDir, canonicalRel);

                var destDir = Path.GetDirectoryName(destPath);
                if (destDir is not null)
                    Directory.CreateDirectory(destDir);

                File.Copy(sourcePath, destPath, overwrite: true);
                count++;
            }
        }

        var sourceAssetsDir = Path.Combine(options.InputPath, "assets");
        if (Directory.Exists(sourceAssetsDir))
        {
            foreach (var file in DemoFiles.Enumerate(sourceAssetsDir))
            {
                var fileName = Path.GetFileName(file);
                if (SensitiveExtensions.Contains(Path.GetExtension(fileName)) ||
                    SensitiveNames.Contains(fileName))
                    continue;

                var rel = Path.Combine("assets", Path.GetRelativePath(sourceAssetsDir, file));
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

    private static string StripQueryAndFragment(string value)
    {
        var cut = value.IndexOfAny(['?', '#']);
        return cut < 0 ? value : value[..cut];
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
