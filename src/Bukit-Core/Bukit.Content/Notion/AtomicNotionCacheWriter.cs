namespace Bukit.Content.Notion;

/// <summary>
/// Writes Notion cache JSON documents atomically: serialize to a unique same-directory
/// temp file, flush to disk, hold a cross-process lock file, then replace the live file
/// in one rename. The live document is never truncated by a crash or cancellation, and
/// temp files are always cleaned up on failure.
/// </summary>
internal static class AtomicNotionCacheWriter
{
    /// <summary>Test seam invoked immediately before the atomic replace.</summary>
    internal static Action<string>? BeforeReplaceHook { get; set; }

    internal static async Task WriteJsonAsync(string targetPath, byte[] json, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrEmpty(directory))
        {
            throw new ArgumentException("Cache path must include a directory.", nameof(targetPath));
        }

        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(json, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            var lockPath = targetPath + ".lock";
            await using (var lockStream = await OpenLockAsync(lockPath, cancellationToken).ConfigureAwait(false))
            {
                BeforeReplaceHook?.Invoke(targetPath);
                if (File.Exists(targetPath))
                {
                    File.Replace(tempPath, targetPath, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(tempPath, targetPath);
                }
            }
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    private static async Task<FileStream> OpenLockAsync(string lockPath, CancellationToken cancellationToken)
    {
        var deadline = TimeSpan.FromSeconds(10);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException) when (stopwatch.Elapsed < deadline)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
