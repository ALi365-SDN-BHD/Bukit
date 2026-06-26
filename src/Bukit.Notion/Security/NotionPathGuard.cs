namespace Bukit.Notion.Security;

public static class NotionPathGuard
{
    public static string ResolvePath(string rootDir, string path)
        => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(rootDir, path));

    public static bool IsWithinRoot(string rootDir, string path)
    {
        string root = NormalizeRoot(rootDir);
        string fullPath = Path.GetFullPath(path);
        return string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), PathComparison)
            || fullPath.StartsWith(root, PathComparison);
    }

    public static string ResolveFinalDirectoryPath(string path)
    {
        var directory = new DirectoryInfo(path);
        FileSystemInfo? target = directory.ResolveLinkTarget(returnFinalTarget: true);
        return Path.GetFullPath(target?.FullName ?? directory.FullName);
    }

    public static string ResolveFinalFilePath(string path)
    {
        var file = new FileInfo(path);
        FileSystemInfo? target = file.ResolveLinkTarget(returnFinalTarget: true);
        return Path.GetFullPath(target?.FullName ?? file.FullName);
    }

    private static string NormalizeRoot(string rootDir)
        => Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
           + Path.DirectorySeparatorChar;

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
}
