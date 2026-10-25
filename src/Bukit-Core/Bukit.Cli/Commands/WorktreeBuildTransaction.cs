namespace Bukit.Cli.Commands;

internal static class WorktreeBuildTransaction
{
    internal static void CommitDirectories(
        IReadOnlyList<(string Staging, string Final, string Backup)> directories,
        IReadOnlyList<(string Staging, string Final, string Backup)> files)
    {
        foreach (var item in directories)
            if (File.Exists(item.Final)) throw new IOException($"Directory transaction target is a file: {item.Final}");
        foreach (var item in files)
            if (Directory.Exists(item.Final)) throw new IOException($"File transaction target is a directory: {item.Final}");

        var completedDirectories = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        var completedFiles = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        try
        {
            foreach (var item in directories)
            {
                var hadOriginal = Directory.Exists(item.Final);
                if (hadOriginal)
                {
                    Directory.Move(item.Final, item.Backup);
                }
                completedDirectories.Add((item.Staging, item.Final, item.Backup, hadOriginal, false));
                Directory.Move(item.Staging, item.Final);
                completedDirectories[^1] = (item.Staging, item.Final, item.Backup, hadOriginal, true);
            }
            foreach (var item in files.Where(item => File.Exists(item.Staging)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.Final)!);
                var hadOriginal = File.Exists(item.Final);
                if (hadOriginal)
                {
                    File.Move(item.Final, item.Backup);
                }
                completedFiles.Add((item.Staging, item.Final, item.Backup, hadOriginal, false));
                File.Move(item.Staging, item.Final);
                completedFiles[^1] = (item.Staging, item.Final, item.Backup, hadOriginal, true);
            }
        }
        catch (Exception commitError)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var item in completedFiles.AsEnumerable().Reverse())
            {
                try
                {
                    if (item.Installed) File.Delete(item.Final);
                    if (item.HadOriginal) File.Move(item.Backup, item.Final);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    rollbackErrors.Add(new IOException($"Unable to restore '{item.Final}'; backup: '{item.Backup}'.", ex));
                }
            }
            foreach (var item in completedDirectories.AsEnumerable().Reverse())
            {
                try
                {
                    if (item.Installed) Directory.Delete(item.Final, recursive: true);
                    if (item.HadOriginal) Directory.Move(item.Backup, item.Final);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    rollbackErrors.Add(new IOException($"Unable to restore '{item.Final}'; backup: '{item.Backup}'.", ex));
                }
            }
            if (rollbackErrors.Count > 0)
                throw new AggregateException("Output transaction failed and rollback is incomplete.", new[] { commitError }.Concat(rollbackErrors));
            throw;
        }
        foreach (var item in completedFiles.Where(item => item.HadOriginal))
        {
            CleanupBackup(item.Backup, isDirectory: false);
        }
        foreach (var item in completedDirectories.Where(item => item.HadOriginal))
        {
            CleanupBackup(item.Backup, isDirectory: true);
        }
    }

    internal static void CleanupBackup(string path, bool isDirectory)
    {
        try
        {
            if (isDirectory)
                Directory.Delete(path, recursive: true);
            else
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[i18n-worktree-cleanup-failed] Build committed; backup retained at '{path}': {ex.Message}");
        }
    }
}
