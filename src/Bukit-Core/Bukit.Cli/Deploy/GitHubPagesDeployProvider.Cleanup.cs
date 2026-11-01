namespace Bukit.Cli.Deploy;

public sealed partial class GitHubPagesDeployProvider
{
    private static void CleanupDeploymentDirectory(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                DeleteDeploymentEntry(tempDir);
            }
        }
        catch (Exception cleanupEx)
        {
            // A completed push is not rolled back by a cleanup failure. Report the
            // warning without echoing paths or remote content that may be sensitive.
            Console.Error.WriteLine($"Deploy: failed to clean up temp dir: {cleanupEx.GetType().Name}");
        }
    }

    private static void DeleteDeploymentEntry(string path)
    {
        var attributes = File.GetAttributes(path);
        var directory = (attributes & FileAttributes.Directory) != 0;
        var link = (attributes & FileAttributes.ReparsePoint) != 0;
        if (directory && !link)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                DeleteDeploymentEntry(entry);
            }
        }

        // Git objects are read-only on Windows. Never change attributes through a
        // symlink/junction or recurse into its target, including at the root.
        if (OperatingSystem.IsWindows() && !link && (attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }

        if (directory)
        {
            Directory.Delete(path, recursive: false);
        }
        else
        {
            File.Delete(path);
        }
    }
}
