using Bukit.Shared;

namespace Bukit.Notion.Security;

public static class NotionPathGuard
{
    public static string ResolvePath(string rootDir, string path)
        => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(rootDir, path));

    public static bool IsWithinRoot(string rootDir, string path)
        => PathUtils.IsSameOrSubPathOf(path, rootDir);

    public static bool IsWithinAnyRoot(string path, params string[] allowedRoots)
        => allowedRoots.Any(root => PathUtils.IsSameOrSubPathOf(path, root));

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

}
