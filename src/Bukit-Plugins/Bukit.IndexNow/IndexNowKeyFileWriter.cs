using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.IndexNow;

public static partial class IndexNowKeyFileWriter
{
    public static string Write(string outputRoot, string key)
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

        var targetInfo = new FileInfo(path);
        if (targetInfo.LinkTarget is not null)
        {
            throw new InvalidOperationException("IndexNow key file target must not be a symbolic link.");
        }

        var temporary = Path.Combine(root, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       leaveOpen: true))
            {
                writer.Write(key);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            targetInfo.Refresh();
            if (targetInfo.LinkTarget is not null)
            {
                throw new InvalidOperationException("IndexNow key file target must not be a symbolic link.");
            }

            File.Move(temporary, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
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
