using System.Runtime.InteropServices;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine;

public sealed record BuildResult(
    string Version,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    long DurationMs,
    BuildEnvironmentInfo Environment,
    BuildProjectInfo Project,
    BuildSummary Summary,
    BuildIncrementalSummary Incremental,
    IReadOnlyList<BuildVariantSummary> Variants,
    IReadOnlyList<string> GeneratedFiles);

public sealed record BuildVariantSummary(
    string Language,
    string OutputDir,
    string BaseUrl,
    int RouteCount,
    int RenderedCount,
    int SkippedCount);

public sealed record BuildEnvironmentInfo(
    string OS,
    string Runtime,
    bool Aot);

public sealed record BuildProjectInfo(
    string Root,
    string Output,
    string ContentSource,
    string? ThemeName);

public sealed record BuildSummary(
    int PageCount,
    int RouteCount,
    int AssetCount,
    int MediaCount,
    int PluginCount,
    int WarningCount,
    int ErrorCount,
    int SchemaErrorCount);

public sealed record BuildIncrementalSummary(
    bool Enabled,
    int CacheHitCount,
    int CacheMissCount);

internal static class BuildResultFactory
{
    /// <summary><paramref name="outputDir"/> relative to <paramref name="rootDir"/>, with forward slashes.</summary>
    internal static string PortableOutputPath(string rootDir, string outputDir)
    {
        var root = Path.GetFullPath(rootDir);
        var relative = Path.GetRelativePath(root, Path.GetFullPath(outputDir, root));
        return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    /// <summary>
    /// The configured relative output as written; an absolute output, or a worktree build's published output,
    /// relative to the project root.
    /// </summary>
    internal static string ReportedOutputPath(string configuredOutput, string rootDir, string? reportedOutputDir)
    {
        if (reportedOutputDir is not null)
        {
            return PortableOutputPath(rootDir, reportedOutputDir);
        }

        return Path.IsPathRooted(configuredOutput)
            ? PortableOutputPath(rootDir, configuredOutput)
            : configuredOutput;
    }

    internal static BuildResult Create(
        AppConfig config,
        string rootDir,
        string outputDir,
        ConfigOverrides overrides,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        long durationMs,
        IReadOnlyList<BuildVariantResult> variants,
        IReadOnlyList<ContentValidationIssue>? schemaErrors = null,
        IReadOnlyList<string>? generatedFiles = null,
        int warningCount = 0,
        int errorCount = 0,
        string? reportedOutputDir = null)
    {
        var pageCount = variants.Sum(v => v.RenderedCount + v.SkippedCount);
        var routeCount = variants.Sum(v => v.RoutedDocuments.Count + v.DerivedDocuments.Count);
        var pluginCount = variants.Sum(v => v.PluginExecutions.Count);
        var cacheHitCount = variants.Sum(v => v.SkippedCount);
        var cacheMissCount = variants.Sum(v => v.RenderedCount);

        return new BuildResult(
            Version: typeof(BuildResultFactory).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            StartedAt: startedAt,
            EndedAt: endedAt,
            DurationMs: durationMs,
            Environment: new BuildEnvironmentInfo(
                OS: RuntimeInformation.OSDescription,
                Runtime: RuntimeInformation.FrameworkDescription,
                Aot: !System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported),
            Project: new BuildProjectInfo(
                // Portable paths: the report is copied with the output, so it names no local directories. A worktree
                // build reports the output it publishes to, not its staging directory.
                Root: ".",
                Output: ReportedOutputPath(config.Build.Output, rootDir, reportedOutputDir),
                ContentSource: ContentConfigResolver.Describe(config.Content),
                ThemeName: config.Theme.Name),
            Summary: new BuildSummary(
                PageCount: pageCount,
                RouteCount: routeCount,
                AssetCount: CountFiles(Path.Combine(outputDir, "assets")),
                MediaCount: CountFiles(Path.Combine(outputDir, "assets", "uploads")),
                PluginCount: pluginCount,
                WarningCount: warningCount,
                ErrorCount: errorCount,
                SchemaErrorCount: schemaErrors?.Count ?? 0),
            Incremental: new BuildIncrementalSummary(
                Enabled: overrides.Incremental ?? true,
                CacheHitCount: cacheHitCount,
                CacheMissCount: cacheMissCount),
            Variants: variants.Select(v => new BuildVariantSummary(
                v.Language,
                BuildTransaction.Logical(Path.GetFullPath(v.OutputDir)),
                v.BaseUrl,
                v.RoutedDocuments.Count + v.DerivedDocuments.Count,
                v.RenderedCount,
                v.SkippedCount)).ToList(),
            GeneratedFiles: generatedFiles ?? Array.Empty<string>());
    }

    private static int CountFiles(string directory)
    {
        return Directory.Exists(directory)
            ? SafeFileEnumerator.EnumerateFiles(directory).Count()
            : 0;
    }
}
