using Bukit.Shared;

namespace Bukit.Importing;

/// <summary>
/// Guards generated content files: two records must never silently write the same
/// path, and a derived path must stay inside the directory it belongs to.
/// </summary>
internal static class ImportWriteGuard
{
    internal static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static bool TryClaim(HashSet<string> claimed, string path, string description)
    {
        if (claimed.Add(path))
            return true;

        Console.Error.WriteLine(
            $"Skipped duplicate output '{path}' ({description}). Check generated slugs that normalize to the same file name.");
        return false;
    }

    internal static bool IsInside(string path, string directory)
        => PathUtils.IsSameOrSubPathOf(path, directory);
}
