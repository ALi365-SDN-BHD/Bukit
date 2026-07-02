namespace Bukit.Cli.Commands;

internal static class DoctorPathHelpers
{
    public static string ToRelativeTemplatePath(string layoutsDir, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return filePath;
        }

        if (!Path.IsPathRooted(filePath))
        {
            return filePath.Replace('\\', '/');
        }

        return Path.GetRelativePath(layoutsDir, filePath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace('\\', '/');
    }
}
