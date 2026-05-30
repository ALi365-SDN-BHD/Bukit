using Bukit.Config;

namespace Bukit.Cli.Commands;

internal static class DoctorThemeChecker
{
    internal static void CheckThemeAssetDirs(AppConfig config, string rootDir)
    {
        var theme = config.Theme;
        var themeRoot = string.IsNullOrWhiteSpace(theme.Name)
            ? rootDir
            : Path.Combine(rootDir, "themes", theme.Name);

        if (!string.IsNullOrWhiteSpace(theme.Name) && !Directory.Exists(themeRoot))
        {
            Console.WriteLine($"⚠ Theme root directory not found: {themeRoot}");
            return;
        }

        var assetsPath = string.Equals(theme.Assets, "assets", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "assets")
            : Path.IsPathRooted(theme.Assets)
                ? theme.Assets
                : Path.Combine(rootDir, theme.Assets);

        if (!Directory.Exists(assetsPath))
        {
            Console.WriteLine($"⚠ Theme assets directory '{assetsPath}' does not exist");
        }
        else
        {
            Console.WriteLine("✔ Theme assets directory exists");
        }

        var staticPath = string.Equals(theme.Static, "static", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "static")
            : Path.IsPathRooted(theme.Static)
                ? theme.Static
                : Path.Combine(rootDir, theme.Static);

        if (!Directory.Exists(staticPath))
        {
            Console.WriteLine($"⚠ Theme static directory '{staticPath}' does not exist");
        }
        else
        {
            Console.WriteLine("✔ Theme static directory exists");
        }
    }

    internal static void CheckThemeAssetContent(AppConfig config, string rootDir)
    {
        var theme = config.Theme;
        var themeRoot = string.IsNullOrWhiteSpace(theme.Name)
            ? rootDir
            : Path.Combine(rootDir, "themes", theme.Name);

        var assetsPath = string.Equals(theme.Assets, "assets", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "assets")
            : Path.IsPathRooted(theme.Assets)
                ? theme.Assets
                : Path.Combine(rootDir, theme.Assets);

        if (Directory.Exists(assetsPath) && !Directory.EnumerateFileSystemEntries(assetsPath).Any())
        {
            Console.WriteLine($"⚠ Theme assets directory '{assetsPath}' is empty");
        }

        var staticPath = string.Equals(theme.Static, "static", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(themeRoot, "static")
            : Path.IsPathRooted(theme.Static)
                ? theme.Static
                : Path.Combine(rootDir, theme.Static);

        if (Directory.Exists(staticPath) && !Directory.EnumerateFileSystemEntries(staticPath).Any())
        {
            Console.WriteLine($"⚠ Theme static directory '{staticPath}' is empty");
        }
    }
}
