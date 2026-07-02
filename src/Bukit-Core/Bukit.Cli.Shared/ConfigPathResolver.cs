using Bukit.Shared;

namespace Bukit.Cli.Shared;

public sealed record ResolvedConfigPath(string FullConfigPath, string RootDir);

public static class ConfigPathResolver
{
    public static ResolvedConfigPath Resolve(string? configPath, string? site)
    {
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var fullConfigPath = Path.GetFullPath(configPath);
            var rootDir = Path.GetDirectoryName(fullConfigPath) ?? Directory.GetCurrentDirectory();
            var configFileName = Path.GetFileName(fullConfigPath);
            var siteDir = Directory.GetParent(rootDir);
            if (configFileName.Equals("site.yaml", StringComparison.OrdinalIgnoreCase) &&
                siteDir?.Name.Equals("sites", StringComparison.OrdinalIgnoreCase) == true &&
                siteDir.Parent is not null)
            {
                rootDir = siteDir.Parent.FullName;
            }
            return new ResolvedConfigPath(fullConfigPath, rootDir);
        }

        if (!string.IsNullOrWhiteSpace(site))
        {
            var rootDir = Directory.GetCurrentDirectory();
            var fileName = NormalizeSiteFileName(site);
            var fullConfigPath = Path.GetFullPath(Path.Combine(rootDir, "sites", fileName));
            var safeRoot = Path.GetFullPath(Path.Combine(rootDir, "sites")) + Path.DirectorySeparatorChar;
            if (!fullConfigPath.StartsWith(safeRoot, Bukit.Shared.PlatformPathHelper.PathComparison))
            {
                throw new ConfigException(
                    $"--site value '{site}' resolves to a path outside the sites directory.",
                    DiagnosticCode.ConfigPathTraversal);
            }
            return new ResolvedConfigPath(fullConfigPath, rootDir);
        }

        var defaultFullConfigPath = Path.GetFullPath("site.yaml");
        var defaultRootDir = Path.GetDirectoryName(defaultFullConfigPath) ?? Directory.GetCurrentDirectory();
        return new ResolvedConfigPath(defaultFullConfigPath, defaultRootDir);
    }

    private static string NormalizeSiteFileName(string site)
    {
        var trimmed = site.Trim();
        if (trimmed.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return trimmed + ".yaml";
    }
}
