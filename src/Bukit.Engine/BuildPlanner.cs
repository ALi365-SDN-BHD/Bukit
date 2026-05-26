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
        var resolvedThemeRoot = ResolveThemeRoot(rootDir, effectiveConfig.Theme, logger);
        var (layoutsDir, assetsDir, staticDir, parentLayoutsDir, parentAssetsDir, parentStaticDir, userLayoutsDir) =
            BuildPathUtils.ResolveThemeDirectories(rootDir, effectiveConfig.Theme, resolvedThemeRoot);

        PrepareOutputDirectory(effectiveConfig, rootDir, outputDir, logger);

        var mediaCacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(rootDir, ".cache", "media")
            : Path.Combine(Path.GetFullPath(overrides.CacheDir!), "media");

        return new BuildPlan(
            effectiveConfig, outputDir,
            layoutsDir, assetsDir, staticDir,
            parentLayoutsDir, parentAssetsDir, parentStaticDir, userLayoutsDir,
            mediaCacheDir, startedAt, stopwatch);
    }

    private static string? ResolveThemeRoot(string rootDir, ThemeConfig theme, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(theme.Source))
        {
            return null;
        }

        var themesCacheDir = Path.Combine(rootDir, ".cache", "themes");
        Directory.CreateDirectory(themesCacheDir);
        var resolved = ThemeSourceManager.Resolve(theme.Source, themesCacheDir, msg => logger.Warn(msg));
        if (resolved is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(theme.Name)
            ? resolved.ThemeRoot
            : Path.Combine(resolved.ThemeRoot, theme.Name);
    }

    private static void PrepareOutputDirectory(AppConfig config, string rootDir, string outputDir, ILogger logger)
    {
        if (config.Build.Clean && Directory.Exists(outputDir))
        {
            EnsureOutputDirectoryCanBeCleaned(rootDir, outputDir);
            Directory.Delete(outputDir, recursive: true);
        }

        if (!config.Build.Clean && BuildRecoveryTracker.HasIncompleteBuild(outputDir))
        {
            logger.Warn($"event=build.recovery previousIncomplete=true outputDir={outputDir} action=autoClean");
            Directory.Delete(outputDir, recursive: true);
        }

        Directory.CreateDirectory(outputDir);

        BuildRecoveryTracker.MarkStarted(outputDir);
        logger.Info($"event=build.start rootDir={rootDir} outputDir={outputDir}");
    }

    private const string OutputMarkerFileName = ".bukit-output-marker";

    private static void EnsureOutputDirectoryCanBeCleaned(string rootDir, string outputDir)
    {
        var fullRoot = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullOutput = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullOutput, fullRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullOutput, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullOutput, Path.GetPathRoot(fullOutput)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(fullOutput), ".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigException($"Refusing to clean unsafe output directory: {outputDir}");
        }

        if (!Directory.EnumerateFileSystemEntries(fullOutput).Any())
        {
            return;
        }

        if (!File.Exists(Path.Combine(fullOutput, OutputMarkerFileName)))
        {
            throw new ConfigException($"Refusing to clean output directory without Bukit marker: {outputDir}");
        }
    }
}
