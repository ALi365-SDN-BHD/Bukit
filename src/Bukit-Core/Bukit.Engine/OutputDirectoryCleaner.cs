using Bukit.Shared;

namespace Bukit.Engine;

public static class OutputDirectoryCleaner
{
    private const string OutputMarkerFileName = ".bukit-output-marker";
    private static readonly char[] PathSeparators =
        [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    public static void CleanIfExists(string rootDir, string outputDir)
    {
        if (!Directory.Exists(outputDir))
        {
            return;
        }

        EnsureCanClean(rootDir, outputDir);
        Directory.Delete(outputDir, recursive: true);
    }

    internal static void EnsureCanClean(string rootDir, string outputDir, bool requireMarker = true)
    {
        var fullRoot = NormalizeForComparison(rootDir);
        var fullOutput = NormalizeForComparison(outputDir);
        var userHomePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var userHome = string.IsNullOrWhiteSpace(userHomePath) ? null : NormalizeForComparison(userHomePath);
        var userHomeComparison = OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : PlatformPathHelper.PathComparison;
        if (string.Equals(fullOutput, fullRoot, PlatformPathHelper.PathComparison)
            || !PathUtils.IsSubPathOf(fullOutput, fullRoot)
            || userHome is not null && string.Equals(fullOutput, userHome, userHomeComparison)
            || ContainsGitSegment(Path.GetRelativePath(fullRoot, fullOutput))
            || ContainsReparsePointBelowRoot(fullRoot, fullOutput))
        {
            throw new ConfigException($"Refusing to clean unsafe output directory: {outputDir}. How to fix: set build.output to a dedicated subdirectory like 'dist' or 'public'.", DiagnosticCode.BuildOutputUnsafe);
        }

        if (!requireMarker || !Directory.Exists(fullOutput) || !Directory.EnumerateFileSystemEntries(fullOutput).Any())
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

    private static string NormalizeForComparison(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        return string.Equals(full, root, PlatformPathHelper.PathComparison)
            ? root!
            : full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool ContainsGitSegment(string path)
        => path.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsReparsePointBelowRoot(string fullRoot, string fullOutput)
    {
        var relativePath = Path.GetRelativePath(fullRoot, fullOutput);
        var current = fullRoot;
        foreach (var segment in relativePath.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            try
            {
                var info = Directory.Exists(current)
                    ? (FileSystemInfo)new DirectoryInfo(current)
                    : new FileInfo(current);
                if (info.LinkTarget is not null ||
                    (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                return false;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return true;
            }
        }

        return false;
    }
}
