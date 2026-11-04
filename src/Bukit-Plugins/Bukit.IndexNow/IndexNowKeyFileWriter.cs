using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.IndexNow;

public static partial class IndexNowKeyFileWriter
{
    public static string Write(string outputRoot, string key)
        => Write(outputRoot, key, beforeCreateTarget: null);

    internal static string Write(string outputRoot, string key, Action<string>? beforeCreateTarget)
    {
        if (string.IsNullOrWhiteSpace(key) || !SafeKey().IsMatch(key))
        {
            throw new InvalidOperationException("INDEXNOW_KEY contains characters that cannot form a public key filename.");
        }

        var root = Path.GetFullPath(outputRoot);
        EnsureNoSymbolicLinks(root);
        Directory.CreateDirectory(root);
        EnsureNoSymbolicLinks(root);
        var path = Path.GetFullPath(Path.Combine(root, key + ".txt"));
        if (!Path.GetDirectoryName(path)!.Equals(root, PathComparison))
        {
            throw new InvalidOperationException("IndexNow key file must stay in the production output root.");
        }

        if (Directory.Exists(path))
        {
            throw new InvalidOperationException("IndexNow key file target must not be a directory.");
        }

        var targetInfo = new FileInfo(path);
        if (targetInfo.LinkTarget is not null)
        {
            throw new InvalidOperationException("IndexNow key file target must not be a symbolic link.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (targetInfo.Exists)
        {
            if (HasExpectedContent(path, keyBytes))
            {
                return path;
            }

            throw new InvalidOperationException(
                $"IndexNow key file '{path}' already exists with different content; refusing to overwrite.");
        }

        beforeCreateTarget?.Invoke(path);

        var created = false;
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.WriteThrough);
            created = true;
            stream.Write(keyBytes);
            stream.Flush(flushToDisk: true);
            return path;
        }
        catch (IOException) when (!created && File.Exists(path))
        {
            targetInfo.Refresh();
            if (targetInfo.LinkTarget is not null)
            {
                throw new InvalidOperationException("IndexNow key file target must not be a symbolic link.");
            }

            if (!HasExpectedContent(path, keyBytes))
            {
                throw new InvalidOperationException(
                    $"IndexNow key file '{path}' already exists with different content; refusing to overwrite.");
            }

            return path;
        }
        catch
        {
            if (created)
            {
                try
                {
                    File.Delete(path);
                }
                catch
                {
                    // Preserve the primary write failure.
                }
            }

            throw;
        }
    }

    private static bool HasExpectedContent(string path, byte[] expected)
    {
        var info = new FileInfo(path);
        return info.Exists &&
               info.Length == expected.Length &&
               File.ReadAllBytes(path).AsSpan().SequenceEqual(expected);
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void EnsureNoSymbolicLinks(string path)
    {
        var current = new DirectoryInfo(path);
        if (current.LinkTarget is not null)
        {
            throw new InvalidOperationException("Production output root must not be a symbolic link.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeKey();
}
