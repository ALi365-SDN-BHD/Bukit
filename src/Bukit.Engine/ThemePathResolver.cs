using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record ResolvedThemePaths(
    string ThemeName,
    string ThemeRoot,
    bool IsRemote,
    string LayoutsDir,
    string AssetsDir,
    string StaticDir,
    string? ParentThemeRoot,
    string? ParentLayoutsDir,
    string? ParentAssetsDir,
    string? ParentStaticDir,
    string? UserLayoutsDir);

internal static class ThemePathResolver
{
    internal static ResolvedThemePaths Resolve(string rootDir, ThemeConfig theme, ILogger logger, IGitRunner? gitRunner = null)
    {
        string? resolvedThemeRoot = null;
        var isRemote = false;

        if (!string.IsNullOrWhiteSpace(theme.Source))
        {
            var themesCacheDir = Path.Combine(rootDir, ".cache", "themes");
            Directory.CreateDirectory(themesCacheDir);
            var resolved = ThemeSourceManager.Resolve(theme.Source, themesCacheDir, msg => logger.Warn(msg), gitRunner);
            if (resolved is not null)
            {
                resolvedThemeRoot = string.IsNullOrWhiteSpace(theme.Name)
                    ? resolved.ThemeRoot
                    : Path.Combine(resolved.ThemeRoot, theme.Name);
                isRemote = true;
            }
        }

        var hasTheme = !string.IsNullOrWhiteSpace(theme.Name) || isRemote;
        var themeName = theme.Name ?? "default";
        var themeRoot = hasTheme
            ? (resolvedThemeRoot ?? Path.Combine(rootDir, "themes", themeName))
            : rootDir;

        var (layoutsDir, assetsDir, staticDir) = ResolveThemeDirs(rootDir, theme, themeRoot, hasTheme);

        string? parentThemeRoot = null;
        string? parentLayoutsDir = null;
        string? parentAssetsDir = null;
        string? parentStaticDir = null;

        if (!string.IsNullOrWhiteSpace(theme.Extends))
        {
            parentThemeRoot = Path.Combine(rootDir, "themes", theme.Extends);
            var parentTheme = new ThemeConfig { Name = theme.Extends };
            var (pLayouts, pAssets, pStatic) = ResolveThemeDirs(rootDir, parentTheme, parentThemeRoot, true);
            parentLayoutsDir = pLayouts;
            parentAssetsDir = pAssets;
            parentStaticDir = pStatic;
        }

        var userLayoutsDir = Path.Combine(rootDir, "layouts");
        if (!Directory.Exists(userLayoutsDir))
        {
            userLayoutsDir = null;
        }

        return new ResolvedThemePaths(
            themeName,
            themeRoot,
            isRemote,
            layoutsDir,
            assetsDir,
            staticDir,
            parentThemeRoot,
            parentLayoutsDir,
            parentAssetsDir,
            parentStaticDir,
            userLayoutsDir);
    }

    private static (string Layouts, string Assets, string Static) ResolveThemeDirs(string rootDir, ThemeConfig theme, string themeRoot, bool hasTheme)
    {
        if (!hasTheme)
        {
            return (
                BuildPathUtils.MakeAbsolute(rootDir, theme.Layouts),
                BuildPathUtils.MakeAbsolute(rootDir, theme.Assets),
                BuildPathUtils.MakeAbsolute(rootDir, theme.Static)
            );
        }

        var layouts = string.Equals(theme.Layouts, "layouts", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "layouts")
            : BuildPathUtils.MakeAbsolute(rootDir, theme.Layouts);

        var assets = string.Equals(theme.Assets, "assets", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "assets")
            : BuildPathUtils.MakeAbsolute(rootDir, theme.Assets);

        var stat = string.Equals(theme.Static, "static", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "static")
            : BuildPathUtils.MakeAbsolute(rootDir, theme.Static);

        return (layouts, assets, stat);
    }
}
