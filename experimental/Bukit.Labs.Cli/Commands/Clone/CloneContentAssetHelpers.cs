using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Labs.Cli.Commands;

internal static partial class CloneContentAssetHelpers
{
    internal static IReadOnlyDictionary<string, string> BuildAssetMap(IReadOnlyList<CloneAsset> assets)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var asset in assets.Where(a => !string.IsNullOrWhiteSpace(a.Src)))
        {
            index++;
            var local = asset.LocalPath;
            if (string.IsNullOrWhiteSpace(local) && asset.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                local = LocalAssetPath(asset, index);
            }

            if (!string.IsNullOrWhiteSpace(local))
                map[asset.Src] = local!;
        }
        return map;
    }

    internal static string AssetFileName(CloneAsset asset, int index)
    {
        try
        {
            var uri = new Uri(asset.Src);
            var fileName = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"{asset.Type}-{index}.img";
            if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
                fileName += ".img";
            return SanitizeFileName(fileName);
        }
        catch
        {
            var ext = Path.GetExtension(asset.Src);
            return SanitizeFileName($"{asset.Type}-{index}{(string.IsNullOrWhiteSpace(ext) ? ".img" : ext)}");
        }
    }

    internal static string LocalAssetPath(CloneAsset asset, int index)
        => $"/assets/{AssetSubdir(asset.Type)}/{AssetFileName(asset, index)}";

    internal static string AssetSubdir(string? type)
    {
        var normalized = (type ?? "").Trim().ToLowerInvariant();
        if (normalized is "video" or "videos" or "movie" or "lottie")
            return "videos";
        if (normalized is "font" or "fonts" or "typeface")
            return "fonts";
        if (normalized is "favicon" or "og" or "open_graph" or "seo" or "manifest")
            return "seo";
        if (normalized is "svg" or "icon" or "icons" or "sprite")
            return "icons";
        return "images";
    }

    internal static string SectionDataKey(CloneSectionInfo section, int index)
    {
        var type = NormalizeType(section.Type ?? section.Semantic);
        return string.IsNullOrWhiteSpace(section.Id)
            ? $"clone-{index + 1:000}-{type}"
            : SanitizeSlug(section.Id!);
    }

    internal static string SectionSpecFileName(CloneSectionInfo section, int index)
    {
        var name = SanitizeSlug(section.Id ?? section.Type ?? section.Semantic ?? $"section-{index + 1:000}");
        return $"{index + 1:000}-{name}.spec.md";
    }

    internal static IEnumerable<string> RewriteUrls(IEnumerable<string> urls, IReadOnlyDictionary<string, string> assetMap)
        => urls.Select(x => RewriteUrl(x, assetMap));

    internal static string RewriteUrls(string html, IReadOnlyDictionary<string, string> assetMap)
    {
        var result = html;
        foreach (var kv in assetMap)
            result = result.Replace(kv.Key, kv.Value, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    internal static string RewriteUrl(string url, IReadOnlyDictionary<string, string> assetMap)
        => assetMap.TryGetValue(url, out var local) ? local : url;

    internal static string NormalizeType(string? type)
    {
        var text = (type ?? "rich_section").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        if (text is "nav" or "navbar" or "header")
            return "navigation";
        if (text is "feature" or "features_grid" or "feature_grid")
            return "features";
        if (text is "call_to_action" or "call-to-action")
            return "cta";
        return SafeIdentifierRegex().Replace(text, "_").Trim('_') switch
        {
            "" => "rich_section",
            var normalized => normalized
        };
    }

    internal static string GenerateAssetManifest(IReadOnlyList<CloneAsset> assets, IReadOnlyDictionary<string, string> assetMap)
    {
        var manifest = assets.Select(asset => new CloneAssetManifestEntry(
            asset.Type,
            asset.Src,
            asset.Alt,
            asset.Media,
            asset.Width,
            asset.Height,
            assetMap.TryGetValue(asset.Src, out var local) ? local : asset.LocalPath,
            asset.Integrity,
            asset.Failure)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("title: 'Clone assets'");
        sb.AppendLine("type: 'assets'");
        sb.AppendLine("order: 0");
        sb.AppendLine("enabled: true");
        sb.AppendLine($"assets_json: {CloneYamlWriter.YamlScalar(CloneJson.Serialize(manifest))}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Clone asset manifest generated from assets.json.");
        return sb.ToString();
    }

    private static string SanitizeSlug(string value)
    {
        var slug = SafeSlugRegex().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "clone-section" : slug;
    }

    private static string SanitizeFileName(string value)
    {
        var sb = new StringBuilder();
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        return sb.Length == 0 ? "asset.img" : sb.ToString();
    }

    [GeneratedRegex("[^a-z0-9_-]+", RegexOptions.IgnoreCase)]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex("[^a-z0-9-]+", RegexOptions.IgnoreCase)]
    private static partial Regex SafeSlugRegex();
}
