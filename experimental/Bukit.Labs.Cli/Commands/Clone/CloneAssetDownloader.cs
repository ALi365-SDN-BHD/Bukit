using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class CloneAssetDownloader
{
    internal static async Task DownloadAssetsAsync(string rootDir, string themeName, List<CloneAsset> assets)
    {
        var ssrfHandler = new System.Net.Http.SocketsHttpHandler
        {
            ConnectCallback = SsrfGuard.SsrfSafeConnectAsync
        };
        using var http = new HttpClient(ssrfHandler, disposeHandler: true);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("bukit-clone/1.0");
        http.Timeout = TimeSpan.FromSeconds(30);

        var total = 0;
        var downloaded = 0;

        foreach (var asset in assets.Where(a => !string.IsNullOrWhiteSpace(a.Src) && a.Src.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
        {
            total++;
            try
            {
                var fileName = string.IsNullOrWhiteSpace(asset.LocalPath)
                    ? CloneContentAssetHelpers.AssetFileName(asset, total)
                    : Path.GetFileName(asset.LocalPath);
                var subdir = CloneContentAssetHelpers.AssetSubdir(asset.Type);
                var assetDir = Path.Combine(rootDir, "themes", themeName, "assets", subdir);
                Directory.CreateDirectory(assetDir);
                var filePath = Path.Combine(assetDir, fileName);
                if (File.Exists(filePath)) continue;

                var bytes = await http.GetByteArrayAsync(asset.Src);
                await File.WriteAllBytesAsync(filePath, bytes);
                downloaded++;
            }
            catch
            {
            }
        }

        if (downloaded > 0)
            Console.WriteLine($"  Downloaded {downloaded}/{total} assets to theme assets/");
    }

    internal static void WriteIcons(string rootDir, string themeName, List<CloneIcon> icons, out int iconCount)
    {
        iconCount = 0;
        if (icons.Count == 0)
            return;

        var iconsDir = Path.Combine(rootDir, "themes", themeName, "assets", "icons");
        Directory.CreateDirectory(iconsDir);
        foreach (var icon in icons)
        {
            if (string.IsNullOrWhiteSpace(icon.Svg))
                continue;

            var fileName = SanitizeFileName(icon.Name) + ".svg";
            File.WriteAllText(Path.Combine(iconsDir, fileName), icon.Svg);
            iconCount++;
        }
    }

    internal static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "icon";

        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "icon" : result;
    }

    internal static int CountBehaviors(CloneBehaviors? b)
    {
        if (b is null) return 0;
        var count = 0;
        if (b.StickyHeader) count++;
        if (b.CardHoverLift) count++;
        if (b.AnimateOnScroll) count++;
        if (b.ScrollShrinkNav) count++;
        if (b.DarkModeToggle) count++;
        if (b.MobileHamburger) count++;
        if (b.SmoothScroll) count++;
        if (b.BackToTop) count++;
        if (b.HasModal) count++;
        if (b.HasDropdown) count++;
        if (b.HasTabs) count++;
        if (b.UseLenis) count++;
        return count;
    }
}
