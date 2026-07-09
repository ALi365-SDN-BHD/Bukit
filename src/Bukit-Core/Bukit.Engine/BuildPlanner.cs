using System.Diagnostics;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

internal sealed record BuildPlan(
    AppConfig EffectiveConfig,
    string OutputDir,
    string LayoutsDir,
    string AssetsDir,
    string StaticDir,
    string? ParentLayoutsDir,
    string? ParentAssetsDir,
    string? ParentStaticDir,
    string? UserLayoutsDir,
    string MediaCacheDir,
    DateTimeOffset StartedAt,
    Stopwatch Stopwatch);

internal static class BuildPlanner
{
    internal static BuildPlan Plan(AppConfig config, string rootDir, ConfigOverrides overrides, ILogger logger)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var effectiveConfig = ConfigApplier.Apply(config, overrides);
        ConfigValidator.Validate(effectiveConfig);

        var outputDir = BuildPathUtils.MakeAbsolute(rootDir, effectiveConfig.Build.Output);
        var resolved = ThemePathResolver.Resolve(rootDir, effectiveConfig.Theme, logger);
        var bootstrap = ThemeBootstrapper.Bootstrap(effectiveConfig, rootDir, logger, resolved);
        var (parentLayoutsDir, parentAssetsDir, parentStaticDir) = ResolveParentThemeDirs(bootstrap.ParentThemeRoot);

        PrepareOutputDirectory(effectiveConfig, rootDir, outputDir, logger);

        var mediaCacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(rootDir, ".cache", "media")
            : Path.Combine(Path.GetFullPath(overrides.CacheDir!), "media");

        return new BuildPlan(
            effectiveConfig, outputDir,
            resolved.LayoutsDir, resolved.AssetsDir, resolved.StaticDir,
            parentLayoutsDir, parentAssetsDir, parentStaticDir, resolved.UserLayoutsDir,
            mediaCacheDir, startedAt, stopwatch);
    }

    private static (string? LayoutsDir, string? AssetsDir, string? StaticDir) ResolveParentThemeDirs(string? parentThemeRoot)
    {
        if (string.IsNullOrWhiteSpace(parentThemeRoot))
        {
            return (null, null, null);
        }

        return (
            Path.Combine(parentThemeRoot, "layouts"),
            Path.Combine(parentThemeRoot, "assets"),
            Path.Combine(parentThemeRoot, "static"));
    }

    private static void PrepareOutputDirectory(AppConfig config, string rootDir, string outputDir, ILogger logger)
    {
        if (config.Build.Clean && Directory.Exists(outputDir))
        {
            OutputDirectoryCleaner.CleanIfExists(rootDir, outputDir);
        }

        if (!config.Build.Clean && BuildRecoveryTracker.HasIncompleteBuild(outputDir))
        {
            logger.Warn($"event=build.recovery previousIncomplete=true outputDir={outputDir} action=autoClean");
            OutputDirectoryCleaner.CleanIfExists(rootDir, outputDir);
        }

        Directory.CreateDirectory(outputDir);

        BuildRecoveryTracker.MarkStarted(outputDir);
        logger.Info($"event=build.start rootDir={rootDir} outputDir={outputDir}");
    }
}
