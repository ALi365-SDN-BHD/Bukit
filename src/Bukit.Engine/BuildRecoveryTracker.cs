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
            return doc.RootElement.TryGetProperty("status", out var status) &&
                   string.Equals(status.GetString(), "started", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void MarkStarted(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(StatePath(outputDir), $$"""{"status":"started","ts":"{{DateTimeOffset.UtcNow:O}}"}""");
    }

    public static void MarkCompleted(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        File.WriteAllText(StatePath(outputDir), $$"""{"status":"completed","ts":"{{DateTimeOffset.UtcNow:O}}"}""");
    }

    private static string StatePath(string outputDir) => Path.Combine(outputDir, StateFileName);
}
