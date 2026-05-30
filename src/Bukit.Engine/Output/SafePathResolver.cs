using Bukit.Shared;

namespace Bukit.Engine.Output;

public class SafePathResolver : IOutputPathPolicy
{
    public string ResolveSafePath(string outputRoot, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));
        var safeRoot = Path.GetFullPath(outputRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(safeRoot, PlatformPathHelper.PathComparison))
        {
            throw new OutputPathSecurityException(
                $"Path traversal detected: resolved path '{fullPath}' escapes output root '{safeRoot}'.");
        }

        return fullPath;
    }
}
