using Bukit.Engine.Analytics;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class AssetsReportInventory
{
    internal static IReadOnlyList<AssetReportEntry> Create(string outputDir)
    {
        var assets = new List<AssetReportEntry>();
        foreach (var file in SafeFileEnumerator.EnumerateFiles(outputDir)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relative = BuildReporter.NormalizePath(Path.GetRelativePath(outputDir, file));
            if (relative is ".bukit-build-state.json" or ".bukit-output-marker")
            {
                continue;
            }

            var topLevelDirectory = relative.Split('/')[0];
            if (string.Equals(
                    topLevelDirectory,
                    BuildReporter.ReportDirectoryName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var info = new FileInfo(file);
            assets.Add(new AssetReportEntry(
                relative,
                relative,
                BuildReportHashing.ComputeSha256(file),
                info.Length));
        }

        return assets;
    }
}

internal static class ReleaseBundleInventory
{
    internal static IReadOnlyList<BuildReportFileEntry> Create(string reportDir, string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            return Array.Empty<BuildReportFileEntry>();
        }

        return SafeFileEnumerator.EnumerateFiles(outputDir)
            .Where(path => !IsUnderReportDirectory(path, reportDir))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildReportFileEntryFactory.Create(outputDir, path))
            .ToList();
    }

    private static bool IsUnderReportDirectory(string path, string reportDir)
    {
        var reportRoot = Path.GetFullPath(reportDir)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        return fullPath.StartsWith(reportRoot, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class ArtifactManifestInventory
{
    internal static IReadOnlyList<BuildReportFileEntry> Create(
        string reportDir,
        string? outputDir,
        IReadOnlyList<BuildVariantResult>? variants)
    {
        var artifactFiles = Directory.EnumerateFiles(reportDir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                "artifact-manifest.json",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var artifactEntries = artifactFiles
            .Select(path => BuildReportFileEntryFactory.Create(reportDir, path))
            .ToList();

        if (outputDir is not null && variants is not null)
        {
            AddVariantAnalyticsReports(artifactFiles, artifactEntries, outputDir, variants);
        }

        return artifactEntries
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddVariantAnalyticsReports(
        IReadOnlyList<string> artifactFiles,
        ICollection<BuildReportFileEntry> artifactEntries,
        string outputDir,
        IReadOnlyList<BuildVariantResult> variants)
    {
        var existingFiles = new HashSet<string>(
            artifactFiles.Select(Path.GetFullPath),
            StringComparer.OrdinalIgnoreCase);
        foreach (var variant in variants.OrderBy(item => item.Language, StringComparer.OrdinalIgnoreCase))
        {
            var analyticsReport = Path.GetFullPath(Path.Combine(
                variant.OutputDir,
                BuildReporter.ReportDirectoryName,
                AnalyticsReportWriter.FileName));
            if (!File.Exists(analyticsReport) ||
                !PathUtils.IsSameOrSubPathOf(variant.OutputDir, outputDir) ||
                !PathUtils.IsSameOrSubPathOf(analyticsReport, outputDir) ||
                !existingFiles.Add(analyticsReport))
            {
                continue;
            }

            artifactEntries.Add(BuildReportFileEntryFactory.Create(outputDir, analyticsReport));
        }
    }
}

internal static class BuildManifestDigestInventory
{
    internal static IReadOnlyList<BuildReportFileEntry> Create(string reportDir)
    {
        return Directory.EnumerateFiles(reportDir, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                "build-manifest-digest.json",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => BuildReportFileEntryFactory.Create(reportDir, path))
            .ToList();
    }
}

internal sealed record AssetReportEntry(
    string Path,
    string? Source,
    string Hash,
    long Size);
