using Bukit.Theme;

namespace Bukit.Labs.Cli.Commands;

internal static class ThemeFileHelper
{
    internal static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);
        foreach (var dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, dir);
            Directory.CreateDirectory(Path.Combine(destinationDir, relative));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    public static void ApplyCssColorOverrides(string themeRoot, string? primaryColor, string? accentColor)
    {
        var stylePath = Path.Combine(themeRoot, "assets", "style.css");
        if (!File.Exists(stylePath))
        {
            return;
        }

        var css = File.ReadAllText(stylePath);
        var updated = StarterThemeScaffold.ApplyColorOverrides(css, primaryColor, accentColor);
        if (!string.Equals(css, updated, StringComparison.Ordinal))
        {
            File.WriteAllText(stylePath, updated);
        }
    }

    internal static bool IsSafeThemeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name is "." or "..")
        {
            return false;
        }

        return !Path.IsPathRooted(name) &&
               name.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }) < 0;
    }
}
