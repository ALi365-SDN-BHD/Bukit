namespace Bukit.Importing;

public static class ImportPathResolver
{
    /// <summary>
    /// Resolves rootDir and fullConfigPath using the provided working directory context
    /// (instead of the process current directory used by ConfigPathResolver).
    /// </summary>
    public static (string RootDir, string FullConfigPath) ResolveRoot(
        string? configPath, string? site, string rootDir, string workingDir)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var fullConfigPath = Path.GetFullPath(Path.IsPathRooted(configPath)
                ? configPath
                : Path.Combine(workingDir, configPath));
            var resolvedRoot = Path.GetDirectoryName(fullConfigPath) ?? workingDir;
            var configFileName = Path.GetFileName(fullConfigPath);
            var siteDir = Directory.GetParent(resolvedRoot);
            if (configFileName.Equals("site.yaml", StringComparison.OrdinalIgnoreCase) &&
                siteDir?.Name.Equals("sites", StringComparison.OrdinalIgnoreCase) == true &&
                siteDir.Parent is not null)
            {
                resolvedRoot = siteDir.Parent.FullName;
            }
            return (resolvedRoot, fullConfigPath);
        }

        if (!string.IsNullOrWhiteSpace(site))
        {
            var fileName = NormalizeSiteFileName(site);
            var fullConfigPath = Path.GetFullPath(Path.Combine(rootDir, "sites", fileName));
            var safeRoot = Path.GetFullPath(Path.Combine(rootDir, "sites")) + Path.DirectorySeparatorChar;
            if (!fullConfigPath.StartsWith(safeRoot, PlatformPathComparison))
                throw new ImportException(ImportErrorKind.UserInput,
                    $"--site value '{site}' resolves to a path outside the sites directory.");
            return (rootDir, fullConfigPath);
        }

        var defaultFullConfigPath = Path.GetFullPath(Path.Combine(rootDir, "site.yaml"));
        var defaultRootDir = Path.GetDirectoryName(defaultFullConfigPath) ?? rootDir;
        return (defaultRootDir, defaultFullConfigPath);
    }

    public static string ResolveInputFromWorkingDir(string workingDir, string value)
        => Path.GetFullPath(Path.IsPathRooted(value) ? value : Path.Combine(workingDir, value));

    public static string? ResolveSitePath(string rootDir, string? sitePath)
        => string.IsNullOrWhiteSpace(sitePath)
            ? null
            : Path.GetFullPath(Path.IsPathRooted(sitePath) ? sitePath : Path.Combine(rootDir, sitePath));

    public static string? ResolveRouteMapPath(string demoDir, string? routeMapPath)
        => string.IsNullOrWhiteSpace(routeMapPath)
            ? null
            : Path.GetFullPath(Path.IsPathRooted(routeMapPath) ? routeMapPath : Path.Combine(demoDir, routeMapPath));

    private static StringComparison PlatformPathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static string NormalizeSiteFileName(string site)
    {
        var trimmed = site.Trim();
        if (trimmed.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return trimmed + ".yaml";
    }
}
