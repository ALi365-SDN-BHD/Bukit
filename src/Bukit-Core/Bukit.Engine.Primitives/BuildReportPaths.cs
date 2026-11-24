using Bukit.Config;

namespace Bukit.Engine;

/// <summary>Locations of build report artifacts shared by Engine and its feature modules.</summary>
internal static class BuildReportPaths
{
    internal const string ReportDirectoryName = ".bukit";

    /// <summary>
    /// Report directories relative to the output root: the root <c>.bukit</c> and, in a multilingual
    /// build, each configured language's <c>&lt;lang&gt;/.bukit</c>, where a language written under its
    /// prefix keeps its own reports. None of them is public output.
    /// </summary>
    internal static IReadOnlyList<string> ReportDirectories(SiteConfig? site)
    {
        var languages = site is null ? [] : SiteLanguageResolver.Clean(site.Languages);
        return languages.Count < 2
            ? [ReportDirectoryName]
            : [ReportDirectoryName, .. languages.Select(language => language.Trim('/') + "/" + ReportDirectoryName)];
    }

    /// <summary>Whether <paramref name="relativePath"/>, relative to the output root, is inside one of <paramref name="reportDirectories"/>.</summary>
    internal static bool IsInReportDirectory(string relativePath, IReadOnlyList<string> reportDirectories)
    {
        var path = relativePath.Replace('\\', '/');
        foreach (var directory in reportDirectories)
        {
            if (path.Equals(directory, StringComparison.OrdinalIgnoreCase) ||
                (path.StartsWith(directory, StringComparison.OrdinalIgnoreCase) &&
                 path.Length > directory.Length && path[directory.Length] == '/'))
            {
                return true;
            }
        }

        return false;
    }
}
