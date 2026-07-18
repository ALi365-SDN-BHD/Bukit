using Bukit.Shared;

namespace Bukit.Engine;

internal static class BuildOutputInventory
{
    private const string BuildStateFileName = ".bukit-build-state.json";
    private const string OutputMarkerFileName = ".bukit-output-marker";

    internal static IReadOnlyList<string> Create(string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            return Array.Empty<string>();
        }

        return SafeFileEnumerator.EnumerateFiles(outputDir)
            .Select(path => Path.GetRelativePath(outputDir, path).Replace('\\', '/'))
            .Where(IsPublicOutput)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsPublicOutput(string relativePath)
    {
        if (string.Equals(relativePath, BuildStateFileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(relativePath, OutputMarkerFileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !relativePath.Split('/').Any(segment =>
            string.Equals(segment, BuildReporter.ReportDirectoryName, StringComparison.OrdinalIgnoreCase));
    }
}
