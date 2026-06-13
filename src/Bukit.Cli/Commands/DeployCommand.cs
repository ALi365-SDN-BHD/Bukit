using Bukit.Cli.Cli.Binding;
using Bukit.Cli.Deploy;
using Bukit.Config;
using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

public static class DeployCommand
{
    public static async Task<int> RunAsync(CliBoundCommand command)
    {
        var resolved = ConfigPathResolver.Resolve(command.GetString("--config"), command.GetString("--site"));

        AppConfig config;
        try
        {
            config = ConfigLoader.Load(resolved.FullConfigPath);
        }
        catch (ConfigException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var deployConfig = config.Deploy ?? new DeployConfig();
        var providerName = deployConfig.Provider ?? "github-pages";
        if (!providerName.Equals("github-pages", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("deploy.provider must be 'github-pages' in Bukit 1.0.");
            return 2;
        }

        var dryRun = command.GetBool("--dry-run");
        var skipBuild = command.GetBool("--skip-build");
        var force = command.GetBool("--force");

        var cliBaseUrl = command.GetString("--base-url");
        var cliSiteUrl = command.GetString("--site-url");
        var cliOutput = command.GetString("--output");
        var cliBranch = command.GetString("--branch");
        var cliMessage = command.GetString("--message");

        if (!string.IsNullOrWhiteSpace(cliBaseUrl))
        {
            config = config with { Site = config.Site with { BaseUrl = cliBaseUrl } };
        }

        if (!string.IsNullOrWhiteSpace(cliSiteUrl))
        {
            config = config with { Site = config.Site with { Url = cliSiteUrl } };
        }

        var isCI = command.GetBool("--ci");
        var logger = new ConsoleLogger(isCI ? LogLevel.Warn : LogLevel.Info);

        var effectiveOutput = !string.IsNullOrWhiteSpace(cliOutput)
            ? cliOutput
            : (config.Build.Output ?? "dist");

        if (!skipBuild)
        {
            logger.Info("Building site before deploy...");
            var buildResult = await BuildCommand.RunAsync(new CliBoundCommand(
                new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["--config"] = command.GetString("--config"),
                    ["--site"] = command.GetString("--site"),
                    ["--output"] = cliOutput,
                    ["--base-url"] = cliBaseUrl,
                    ["--site-url"] = cliSiteUrl,
                    ["--clean"] = "true",
                    ["--ci"] = isCI ? "true" : null,
                }
                .Where(x => x.Value is not null)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                Array.Empty<string>()));

            if (buildResult != 0)
            {
                logger.Error("Build failed. Aborting deploy.");
                return buildResult;
            }
        }

        var outputDir = Path.IsPathRooted(effectiveOutput)
            ? Path.GetFullPath(effectiveOutput)
            : Path.GetFullPath(Path.Combine(resolved.RootDir, effectiveOutput));

        var baseUrl = string.IsNullOrWhiteSpace(config.Site.BaseUrl) ? "/" : config.Site.BaseUrl;
        if (!baseUrl.StartsWith('/'))
        {
            baseUrl = "/" + baseUrl;
        }

        var siteUrl = config.Site.Url ?? string.Empty;

        if (dryRun)
        {
            logger.Info("--dry-run: skipping actual deployment.");
            logger.Info($"Would deploy {outputDir} to GitHub Pages");
            logger.Info($"  branch: {cliBranch ?? deployConfig.Branch}");
            logger.Info($"  message: {cliMessage ?? deployConfig.Message}");
            logger.Info($"  baseUrl: {baseUrl}");
            logger.Info($"  siteUrl: {siteUrl}");
            return 0;
        }

        var context = new DeployContext
        {
            OutputDir = outputDir,
            SiteUrl = siteUrl,
            BaseUrl = baseUrl,
            Branch = cliBranch ?? deployConfig.Branch,
            Message = cliMessage ?? deployConfig.Message,
            Cname = deployConfig.Cname,
            KeepHistory = deployConfig.KeepHistory,
            Force = force,
            Logger = logger
        };

        var provider = new GitHubPagesDeployProvider();
        var result = await provider.DeployAsync(context, CancellationToken.None);

        if (result.Success)
        {
            logger.Info($"Deployment complete. Site available at: {result.DeployedUrl}");
            return 0;
        }

        logger.Error($"Deployment failed: {result.Error}");
        return 1;
    }

}
