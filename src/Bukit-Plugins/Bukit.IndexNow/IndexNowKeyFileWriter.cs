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

        File.WriteAllText(path, key, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static void EnsureNoSymbolicLinks(string path)
    {
        var current = new DirectoryInfo(path);
        if (current.Exists && current.LinkTarget is not null)
        {
            throw new InvalidOperationException("Production output root must not be a symbolic link.");
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeKey();
}
