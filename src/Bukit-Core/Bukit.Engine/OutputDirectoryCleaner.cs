using Bukit.Shared;

namespace Bukit.Engine;

public static class OutputDirectoryCleaner
{
    private const string OutputMarkerFileName = ".bukit-output-marker";

    public static void CleanIfExists(string rootDir, string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            return;
        }

        EnsureCanClean(rootDir, outputDir);
        Directory.Delete(outputDir, recursive: true);
    }

    private static void EnsureCanClean(string rootDir, string outputDir)
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
                $"How to fix: review and move or remove the existing files, or set build.output to a dedicated empty output directory. " +
                $"Then rerun the build; a successful build creates .bukit-output-marker automatically.",
                DiagnosticCode.BuildOutputNoMarker);
        }
    }
}
