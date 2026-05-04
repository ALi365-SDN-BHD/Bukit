namespace Bukit.Engine;

public static class DirectoryCopy
{
    public static void Copy(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destinationDir, name);
            File.Copy(file, dest, overwrite: true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(destinationDir, name);
            Copy(dir, dest);
        }
    }

    public static void Sync(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            SyncFile(file, destinationDir);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(destinationDir, name);
            Sync(dir, dest);
        }
    }

    public static void SyncFiles(string sourceDir, string destinationDir, bool ignoreDotPrefixedFiles = false)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            if (ignoreDotPrefixedFiles && name.StartsWith('.'))
            {
                continue;
            }

            SyncFile(file, destinationDir);
        }
    }

    private static void SyncFile(string sourceFile, string destinationDir)
    {
        var name = Path.GetFileName(sourceFile);
        var destinationFile = Path.Combine(destinationDir, name);

        var sourceInfo = new FileInfo(sourceFile);
        var destinationInfo = new FileInfo(destinationFile);
        if (destinationInfo.Exists
            && destinationInfo.Length == sourceInfo.Length
            && destinationInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
        {
            return;
        }

        File.Copy(sourceFile, destinationFile, overwrite: true);
        File.SetLastWriteTimeUtc(destinationFile, sourceInfo.LastWriteTimeUtc);
    }
}
