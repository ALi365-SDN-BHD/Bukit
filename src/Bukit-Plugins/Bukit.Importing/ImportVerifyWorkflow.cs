using Bukit.Cli.Shared;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Importing;

public static class ImportVerifyWorkflow
{
    public static async Task<int> VerifyAsync(ImportResult result, string rootDir, string themeName)
    {
        var siteDir = string.IsNullOrWhiteSpace(result.SitePath)
            ? Path.Combine(rootDir, "sites", themeName)
            : result.SitePath;
        var siteConfig = Path.Combine(siteDir, "site.yaml");

        try
        {
            var resolved = ConfigPathResolver.Resolve(siteConfig, site: null);
            var config = ConfigLoader.Load(resolved.FullConfigPath);
            ConfigValidator.Validate(config);

            var engine = new SiteEngine(new ConsoleLogger(LogLevel.Warn));
            await engine.BuildAsync(config, resolved.RootDir, new ConfigOverrides { IsCI = true });
            return 0;
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
