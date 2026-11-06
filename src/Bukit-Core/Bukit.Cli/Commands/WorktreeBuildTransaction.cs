using Bukit.Engine;
using Bukit.Shared;

namespace Bukit.Cli.Commands;

internal static class WorktreeBuildTransaction
{
    internal static void CommitDirectories(
        IReadOnlyList<(string Staging, string Final, string Backup)> directories,
        IReadOnlyList<(string Staging, string Final, string Backup)> files,
        Action<string, string, bool>? validateBackup = null,
        string? journalRoot = null,
        BuildTransactionJournal? preparedJournal = null)
    {
        foreach (var (_, final, _) in directories)
            if (File.Exists(final)) throw new IOException($"Directory transaction target is a file: {final}");
        foreach (var (_, final, _) in files)
            if (Directory.Exists(final)) throw new IOException($"File transaction target is a directory: {final}");

        var completedDirectories = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        var completedFiles = new List<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)>();
        var activeFiles = files.Where(item => File.Exists(item.Staging)).ToArray();
        var entries = directories.Select(item => new BuildTransactionJournal.Entry(
                PathUtils.GetCanonicalFullPath(item.Final), PathUtils.GetCanonicalFullPath(item.Staging), PathUtils.GetCanonicalFullPath(item.Backup), true))
            .Concat(activeFiles.Select(item => new BuildTransactionJournal.Entry(
                PathUtils.GetCanonicalFullPath(item.Final), PathUtils.GetCanonicalFullPath(item.Staging), PathUtils.GetCanonicalFullPath(item.Backup), false)))
            .ToArray();
        if (entries.Length == 0) return;
        var journal = preparedJournal ?? BuildTransactionJournal.Create(journalRoot ?? Path.GetDirectoryName(entries[0].Path)!, entries[0].Path, entries);
        if (preparedJournal is not null) journal.EnsureEntries(entries);
        var index = 0;
        try
        {
            foreach (var (staging, final, backup) in directories)
            {
                var hadOriginal = Directory.Exists(final);
                journal.BeforeMove(index++, hadOriginal);
                if (hadOriginal)
                {
                    Directory.Move(final, backup);
                }
                completedDirectories.Add((staging, final, backup, hadOriginal, false));
                validateBackup?.Invoke(final, backup, hadOriginal);
                Directory.Move(staging, final);
                completedDirectories[^1] = (staging, final, backup, hadOriginal, true);
            }
            foreach (var (staging, final, backup) in activeFiles)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(final)!);
                var hadOriginal = File.Exists(final);
                journal.BeforeMove(index++, hadOriginal);
                if (hadOriginal)
                {
                    File.Move(final, backup);
                }
                completedFiles.Add((staging, final, backup, hadOriginal, false));
                validateBackup?.Invoke(final, backup, hadOriginal);
                File.Move(staging, final);
                completedFiles[^1] = (staging, final, backup, hadOriginal, true);
            }
            journal.Complete();
        }
        catch (Exception commitError)
        {
            Rollback(completedDirectories, completedFiles, commitError, journal);
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
        if (completedFiles.All(item => !item.HadOriginal || !File.Exists(item.Backup)) &&
            completedDirectories.All(item => !item.HadOriginal || !Directory.Exists(item.Backup)))
            journal.Remove();
    }

    private static void Rollback(
        IReadOnlyList<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)> completedDirectories,
        IReadOnlyList<(string Staging, string Final, string Backup, bool HadOriginal, bool Installed)> completedFiles,
        Exception commitError,
        BuildTransactionJournal journal)
    {
        var rollbackErrors = new List<Exception>();
        foreach (var (_, final, backup, hadOriginal, installed) in completedFiles.Reverse())
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
        foreach (var (_, final, backup, hadOriginal, installed) in completedDirectories.Reverse())
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
        journal.Remove();
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
