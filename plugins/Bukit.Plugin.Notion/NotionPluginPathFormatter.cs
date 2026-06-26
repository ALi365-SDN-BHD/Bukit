namespace Bukit.Plugin.Notion;

internal static class NotionPluginPathFormatter
{
    public static string ToProjectRelativePath(string projectRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        string relative = Path.GetRelativePath(projectRoot, path);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }
}
