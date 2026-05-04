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
            var name = Path.GetFileName(file);
            var dest = Path.Combine(destinationDir, name);

            var srcInfo = new FileInfo(file);
            var destInfo = new FileInfo(dest);
            if (destInfo.Exists && destInfo.Length == srcInfo.Length && destInfo.LastWriteTimeUtc == srcInfo.LastWriteTimeUtc)
            {
                continue;
            }

            File.Copy(file, dest, overwrite: true);
            File.SetLastWriteTimeUtc(dest, srcInfo.LastWriteTimeUtc);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            var dest = Path.Combine(destinationDir, name);
            Sync(dir, dest);
        }
    }
}
