using Bukit.Engine.Abstractions.Plugins.Protocol;

namespace Bukit.Engine.Plugins.Protocol;

internal static class ProtocolOutputWriter
{
    internal static IReadOnlyList<string> WriteOutputs(string outputDir, IReadOnlyList<AfterBuildOutputFile> outputs)
    {
        var written = new List<string>();
        foreach (var output in outputs)
        {
            if (string.IsNullOrWhiteSpace(output.Path))
            {
                throw new InvalidOperationException("Protocol plugin output path is required.");
            }

            if (Path.IsPathRooted(output.Path))
            {
                throw new InvalidOperationException($"Protocol plugin output path must be relative: {output.Path}");
            }

            var fullPath = Path.GetFullPath(Path.Combine(outputDir, output.Path.Replace('/', Path.DirectorySeparatorChar)));
            var safeRoot = Path.GetFullPath(outputDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Protocol plugin output path escapes outputDir: {output.Path}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var hasText = !string.IsNullOrWhiteSpace(output.Text);
            var hasBase64 = !string.IsNullOrWhiteSpace(output.Base64);
            if (hasText && hasBase64)
            {
                throw new InvalidOperationException($"Protocol plugin output must provide either text or base64: {output.Path}");
            }

            if (hasBase64)
            {
                try
                {
                    var bytes = Convert.FromBase64String(output.Base64!);
                    File.WriteAllBytes(fullPath, bytes);
                }
                catch (FormatException ex)
                {
                    throw new InvalidOperationException($"Protocol plugin output base64 is invalid: {output.Path}", ex);
                }
            }
            else
            {
                File.WriteAllText(fullPath, output.Text ?? string.Empty);
            }

            written.Add(output.Path.Replace('\\', '/').TrimStart('/'));
        }

        return written;
    }
}
