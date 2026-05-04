using System.Text;

namespace Bukit.Engine;

public static class FileWriter
{
    public static void WriteUtf8(string outputRoot, string relativePath, string content)
    {
        var fullPath = Path.GetFullPath(Path.Combine(outputRoot, relativePath));
        var safeRoot = Path.GetFullPath(outputRoot) + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Path traversal detected: resolved path '{fullPath}' escapes output root '{safeRoot}'.");
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}

