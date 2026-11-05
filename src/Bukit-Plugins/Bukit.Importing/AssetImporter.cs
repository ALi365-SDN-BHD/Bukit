using System.Text;
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
        var pendingCss = new Queue<(string SourcePath, int Depth)>();
        var fullInputPath = Path.GetFullPath(options.InputPath);
        var basePath = InternalLinkRewriter.BasePath(options.BaseUrl);

        void CopyAsset(string sourcePath, string? rawReference, int depth)
        {
            if (!PathUtils.IsSameOrSubPathOf(sourcePath, fullInputPath))
            {
                warnings.Add($"Path traversal rejected: {rawReference ?? sourcePath}");
                return;
            }

            if (!File.Exists(sourcePath))
                return;

            var canonicalRel = Path.GetRelativePath(fullInputPath, sourcePath).Replace('\\', '/');
            if (canonicalRel.StartsWith("../", StringComparison.Ordinal))
                return;

            var fileName = Path.GetFileName(canonicalRel);
            if (SensitiveExtensions.Contains(Path.GetExtension(fileName)) ||
                SensitiveNames.Contains(fileName))
            {
                warnings.Add($"Skipped sensitive file: {canonicalRel}");
                return;
            }

            var destUrl = "/" + canonicalRel;
            if (rawReference is not null &&
                !mappings.TryAdd(rawReference, destUrl) &&
                mappings[rawReference] != destUrl)
            {
                warnings.Add(
                    $"Asset reference '{rawReference}' resolves to multiple files; kept '{mappings[rawReference]}'.");
            }

            if (!seen.Add(canonicalRel))
                return;

            var isImage = ImageExtensions.Contains(
                Path.GetExtension(canonicalRel).ToLowerInvariant());
            var destPath = Path.Combine(themeDir, isImage ? "assets" : "static", canonicalRel);
            var destDir = Path.GetDirectoryName(destPath);
            if (destDir is not null)
                Directory.CreateDirectory(destDir);

            if (Path.GetExtension(canonicalRel).Equals(".css", StringComparison.OrdinalIgnoreCase))
            {
                CopyCss(sourcePath, destPath, basePath);
            }
            else
            {
                File.Copy(sourcePath, destPath, overwrite: true);
            }
            count++;

            // A linked stylesheet may reference images/fonts outside the assets/ tree.
            if (depth < 5 &&
                Path.GetExtension(canonicalRel).Equals(".css", StringComparison.OrdinalIgnoreCase))
            {
                pendingCss.Enqueue((sourcePath, depth));
            }
        }

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

                CopyAsset(sourcePath, assetPath, 0);
            }
        }

        while (pendingCss.Count > 0)
        {
            var (cssPath, depth) = pendingCss.Dequeue();
            string css;
            try
            {
                css = DemoFiles.DecodeCss(File.ReadAllBytes(cssPath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add($"Could not read stylesheet '{Path.GetRelativePath(fullInputPath, cssPath)}': {ex.Message}");
                continue;
            }

            foreach (Match match in CssUrlPattern().Matches(css).Concat(CssImportPattern().Matches(css)))
            {
                var value = match.Groups["value"].Value.Trim();
                if (value.Length == 0 ||
                    value.StartsWith('#') ||
                    value.StartsWith("//", StringComparison.Ordinal) ||
                    value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var reference = StripQueryAndFragment(value);
                if (string.IsNullOrWhiteSpace(reference) || !IsSafeAsset(reference))
                    continue;

                var target = reference.StartsWith('/')
                    ? Path.Combine(fullInputPath, reference.TrimStart('/'))
                    : Path.Combine(Path.GetDirectoryName(cssPath)!, reference);
                CopyAsset(Path.GetFullPath(target), null, depth + 1);
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

                    if (Path.GetExtension(file).Equals(".css", StringComparison.OrdinalIgnoreCase))
                    {
                        CopyCss(file, dest, basePath);
                    }
                    else
                    {
                        File.Copy(file, dest);
                    }

                    var origKey = "assets/" + rel.Replace('\\', '/');
                    mappings[origKey] = "/assets/" + rel.Replace('\\', '/');
                    count++;
                }
            }
        }

        return new AssetImportResult(count, warnings, mappings);
    }

    [GeneratedRegex(@"url\(\s*(?<quote>['""]?)(?<value>[^'"")]+)\k<quote>\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CssUrlPattern();

    [GeneratedRegex(@"@import\s+(?:url\(\s*)?[""']?(?<value>[^""')\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CssImportPattern();

    /// <summary>
    /// UTF-8 stylesheets are copied as they are (and only rewritten in place for a
    /// sub-path deploy). Other encodings are converted to UTF-8, because the built
    /// pages are UTF-8 and a browser would otherwise misread a GBK stylesheet.
    /// </summary>
    private static void CopyCss(string sourcePath, string destPath, string basePath)
    {
        var bytes = File.ReadAllBytes(sourcePath);
        var needsConversion =
            !DemoFiles.IsValidUtf8(bytes) || DemoFiles.HasNonUtf8CssCharset(bytes);

        if (!needsConversion && string.IsNullOrEmpty(basePath))
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            return;
        }

        var text = RemoveCssCharset(DemoFiles.DecodeCss(bytes));
        text = RewriteRootCssUrls(text, basePath);
        File.WriteAllBytes(destPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));
    }

    private static string RemoveCssCharset(string css)
        => CssCharsetPattern().Replace(css, "", 1);

    private static string RewriteRootCssUrls(string css, string basePath)
        => string.IsNullOrEmpty(basePath)
            ? css
            : CssRootImportPattern().Replace(
                CssRootUrlPattern().Replace(css, match =>
                    $"url({match.Groups["quote"].Value}{basePath}{match.Groups["value"].Value}{match.Groups["quote"].Value})"),
                match =>
                    $"@import {match.Groups["quote"].Value}{basePath}{match.Groups["value"].Value}{match.Groups["quote"].Value}");

    [GeneratedRegex(@"url\(\s*(?<quote>['""]?)(?<value>/(?!/)[^'"")]+)\k<quote>\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex CssRootUrlPattern();

    [GeneratedRegex(@"@import\s+(?<quote>[""'])(?<value>/(?!/)[^""']+)\k<quote>", RegexOptions.IgnoreCase)]
    private static partial Regex CssRootImportPattern();

    [GeneratedRegex(@"^\s*@charset\s+[""'][^""']+[""']\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex CssCharsetPattern();

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
