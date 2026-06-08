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

        if (IsCI(overrides) && !overrides.AllowExternalPlugins && effectiveConfig.Site.ExternalPlugins is { Count: > 0 })
        {
            throw new ConfigException("External plugins are disabled in CI environments. Use --allow-external-plugins to enable.", DiagnosticCode.ConfigInvalidValue);
        }

        var outputDir = BuildPathUtils.MakeAbsolute(rootDir, effectiveConfig.Build.Output);
        var resolved = ThemePathResolver.Resolve(rootDir, effectiveConfig.Theme, logger);

        PrepareOutputDirectory(effectiveConfig, rootDir, outputDir, logger);

        var mediaCacheDir = string.IsNullOrWhiteSpace(overrides.CacheDir)
            ? Path.Combine(rootDir, ".cache", "media")
            : Path.Combine(Path.GetFullPath(overrides.CacheDir!), "media");

        return new BuildPlan(
            effectiveConfig, outputDir,
            resolved.LayoutsDir, resolved.AssetsDir, resolved.StaticDir,
            resolved.ParentLayoutsDir, resolved.ParentAssetsDir, resolved.ParentStaticDir, resolved.UserLayoutsDir,
            mediaCacheDir, startedAt, stopwatch);
    }

    private static bool IsCI(ConfigOverrides overrides)
    {
        return overrides.IsCI
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUKIT_CI"));
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
        if (string.Equals(fullOutput, fullRoot, PlatformPathHelper.PathComparison)
            || string.Equals(fullOutput, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PlatformPathHelper.PathComparison)
            || string.Equals(fullOutput, Path.GetPathRoot(fullOutput)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PlatformPathHelper.PathComparison)
            || string.Equals(Path.GetFileName(fullOutput), ".git", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConfigException($"Refusing to clean unsafe output directory: {outputDir}. How to fix: set build.output to a dedicated subdirectory like 'dist' or 'public'.", DiagnosticCode.BuildOutputUnsafe);
        }

        if (!Directory.EnumerateFileSystemEntries(fullOutput).Any())
        {
            return;
        }

        if (!File.Exists(Path.Combine(fullOutput, OutputMarkerFileName)))
        {
            throw new ConfigException(
                $"Bukit refuses to clean this directory because it does not contain .bukit-output-marker: {outputDir}. " +
                $"This prevents accidental deletion of non-Bukit files. " +
                $"How to fix: run 'bukit clean --init-marker' to mark this as a Bukit output directory, " +
                $"or set build.clean: false in site.yaml.",
                DiagnosticCode.BuildOutputNoMarker);
        }
    }
}
