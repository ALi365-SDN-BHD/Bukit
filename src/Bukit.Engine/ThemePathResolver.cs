using Bukit.Config;
using Bukit.Shared;
using Bukit.Theme;

namespace Bukit.Engine;

internal sealed record ResolvedThemePaths(
    string ThemeName,
    string ThemeRoot,
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
    internal static ResolvedThemePaths Resolve(string rootDir, ThemeConfig theme, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(theme.Name) &&
            !ThemeNameSanitizer.TrySanitize(theme.Name, out _, out var nameError))
        {
            throw new ConfigException(
                $"theme.name '{theme.Name}' is invalid: {nameError}",
                DiagnosticCode.ConfigPathTraversal);
        }

        var hasTheme = !string.IsNullOrWhiteSpace(theme.Name);
        var themeName = theme.Name ?? "default";
        var themeRoot = hasTheme
            ? Path.Combine(rootDir, "themes", themeName)
            : rootDir;

        var (layoutsDir, assetsDir, staticDir) = ResolveThemeDirs(rootDir, theme, themeRoot, hasTheme);
        var (parentThemeRoot, parentLayoutsDir, parentAssetsDir, parentStaticDir) =
            ResolveParentThemeDirs(rootDir, themeRoot, hasTheme);

        var userLayoutsDir = Path.Combine(rootDir, "layouts");
        if (!Directory.Exists(userLayoutsDir))
        {
            userLayoutsDir = null;
        }

        return new ResolvedThemePaths(
            themeName,
            themeRoot,
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
                BuildPathUtils.MakeAbsolute(rootDir, theme.Layouts, enforceWithinRoot: true),
                BuildPathUtils.MakeAbsolute(rootDir, theme.Assets, enforceWithinRoot: true),
                BuildPathUtils.MakeAbsolute(rootDir, theme.Static, enforceWithinRoot: true)
            );
        }

        var layouts = string.Equals(theme.Layouts, "layouts", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "layouts")
            : BuildPathUtils.MakeAbsolute(rootDir, theme.Layouts, enforceWithinRoot: true);

        var assets = string.Equals(theme.Assets, "assets", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "assets")
            : BuildPathUtils.MakeAbsolute(rootDir, theme.Assets, enforceWithinRoot: true);

        var stat = string.Equals(theme.Static, "static", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "static")
            : BuildPathUtils.MakeAbsolute(rootDir, theme.Static, enforceWithinRoot: true);

        return (layouts, assets, stat);
    }

    private static (string? ThemeRoot, string? LayoutsDir, string? AssetsDir, string? StaticDir) ResolveParentThemeDirs(
        string rootDir,
        string themeRoot,
        bool hasTheme)
    {
        if (!hasTheme)
        {
            return (null, null, null, null);
        }

        var manifest = LoadThemeManifestForExtend(themeRoot);
        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Extends))
        {
            return (null, null, null, null);
        }

        if (!ThemeNameSanitizer.TrySanitize(manifest.Extends, out var safeExtends, out var extendsError))
        {
            throw new ConfigException(
                $"theme.yaml extends '{manifest.Extends}' is invalid: {extendsError}",
                DiagnosticCode.ConfigPathTraversal);
        }

        var parentThemeRoot = Path.Combine(rootDir, "themes", safeExtends);
        var parentTheme = new ThemeConfig { Name = safeExtends };
        var (parentLayoutsDir, parentAssetsDir, parentStaticDir) = ResolveThemeDirs(rootDir, parentTheme, parentThemeRoot, hasTheme: true);
        return (parentThemeRoot, parentLayoutsDir, parentAssetsDir, parentStaticDir);
    }

    private static ThemeManifestV2? LoadThemeManifestForExtend(string themeRoot)
    {
        try
        {
            return ThemeManifestLoader.Load(themeRoot, required: false);
        }
        catch (ThemeManifestException ex)
        {
            throw new ConfigException($"theme.yaml at '{themeRoot}' is invalid: {ex.Message}", ex, DiagnosticCode.ThemeManifestInvalid);
        }
    }
}
