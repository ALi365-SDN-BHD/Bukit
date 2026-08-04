using System.Text;
using System.Text.Json;

namespace Bukit.Engine;

internal static class BuildRecoveryTracker
{
    private const string StateFileName = ".bukit-build-state.json";

    public static bool HasIncompleteBuild(string outputDir)
    {
        var path = StatePath(outputDir);
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("status", out var status))
            {
                return true;
            }

            // Only an explicit completed state proves the previous build finished;
            // unknown statuses fail closed and trigger a clean recovery.
            return !string.Equals(status.GetString(), "completed", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // Malformed or unreadable state is treated as an interrupted build.
            return true;
        }
    }

    public static void MarkStarted(string outputDir) => WriteStateAtomic(outputDir, "started");

    public static void MarkCompleted(string outputDir) => WriteStateAtomic(outputDir, "completed");

    private static void WriteStateAtomic(string outputDir, string status)
    {
        Directory.CreateDirectory(outputDir);
        var path = StatePath(outputDir);
        var tempPath = $"{path}.tmp-{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write($$"""{"status":"{{status}}","ts":"{{DateTimeOffset.UtcNow:O}}"}""");
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
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
            catch
            {
                // Cleanup is best effort; the original state file is untouched.
            }

            throw;
        }
    }

    private static string StatePath(string outputDir) => Path.Combine(outputDir, StateFileName);
}
