using System.Text;
using Bukit.Engine.Output;

namespace Bukit.Engine;

internal static class FileWriter
{
    private static IOutputPathPolicy? s_defaultPolicy;

    internal static IOutputPathPolicy DefaultPolicy
    {
        get => s_defaultPolicy ??= new SafePathResolver();
        set => s_defaultPolicy = value;
    }

    public static string GetSafeFullPath(string outputRoot, string relativePath, IOutputPathPolicy? pathPolicy = null)
    {
        return (pathPolicy ?? DefaultPolicy).ResolveSafePath(outputRoot, relativePath);
    }

    public static void WriteUtf8(string outputRoot, string relativePath, string content, IOutputPathPolicy? pathPolicy = null)
    {
        var fullPath = GetSafeFullPath(outputRoot, relativePath, pathPolicy);

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
