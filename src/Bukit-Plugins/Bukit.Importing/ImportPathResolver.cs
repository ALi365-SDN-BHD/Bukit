using Bukit.Cli.Shared;

namespace Bukit.Importing;

public static class ImportPathResolver
{
    public static (string RootDir, string FullConfigPath) ResolveRoot(string? configPath, string? site)
    {
        var resolved = ConfigPathResolver.Resolve(configPath, site);
        return (resolved.RootDir, resolved.FullConfigPath);
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
}
