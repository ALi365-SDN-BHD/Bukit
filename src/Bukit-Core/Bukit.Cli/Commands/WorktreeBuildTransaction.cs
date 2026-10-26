namespace Bukit.Cli.Commands;

internal static class WorktreeBuildTransaction
{
    internal static void CommitDirectories(
        IReadOnlyList<(string Staging, string Final, string Backup)> directories,
        IReadOnlyList<(string Staging, string Final, string Backup)> files)
    {
        foreach (var (_, final, _) in directories)
            if (File.Exists(final)) throw new IOException($"Directory transaction target is a file: {final}");
        foreach (var (_, final, _) in files)
            if (Directory.Exists(final)) throw new IOException($"File transaction target is a directory: {final}");

        var completedDirectories = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        var completedFiles = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        try
        {
            foreach (var (staging, final, backup) in directories)
            {
                var hadOriginal = Directory.Exists(final);
                if (hadOriginal)
                {
                    Directory.Move(final, backup);
                }
                completedDirectories.Add((staging, final, backup, hadOriginal, false));
                Directory.Move(staging, final);
                completedDirectories[^1] = (staging, final, backup, hadOriginal, true);
            }
            foreach (var (staging, final, backup) in files.Where(item => File.Exists(item.Staging)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(final)!);
                var hadOriginal = File.Exists(final);
                if (hadOriginal)
                {
                    File.Move(final, backup);
                }
                completedFiles.Add((staging, final, backup, hadOriginal, false));
                File.Move(staging, final);
                completedFiles[^1] = (staging, final, backup, hadOriginal, true);
            }
        }
        catch (Exception commitError)
        {
            var rollbackErrors = new List<Exception>();
            foreach (var (_, final, backup, hadOriginal, installed) in completedFiles.AsEnumerable().Reverse())
            {
                try
                {
                    if (installed) File.Delete(final);
                    if (hadOriginal) File.Move(backup, final);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    rollbackErrors.Add(new IOException($"Unable to restore '{final}'; backup: '{backup}'.", ex));
                }
            }
            foreach (var (_, final, backup, hadOriginal, installed) in completedDirectories.AsEnumerable().Reverse())
            {
                try
                {
                    if (installed) Directory.Delete(final, recursive: true);
                    if (hadOriginal) Directory.Move(backup, final);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    rollbackErrors.Add(new IOException($"Unable to restore '{final}'; backup: '{backup}'.", ex));
                }
            }
            if (rollbackErrors.Count > 0)
                throw new AggregateException("Output transaction failed and rollback is incomplete.", new[] { commitError }.Concat(rollbackErrors));
            throw;
        }
        foreach (var (_, _, backup, _, _) in completedFiles.Where(item => item.HadOriginal))
        {
            CleanupBackup(backup, isDirectory: false);
        }
        foreach (var (_, _, backup, _, _) in completedDirectories.Where(item => item.HadOriginal))
        {
            CleanupBackup(backup, isDirectory: true);
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
