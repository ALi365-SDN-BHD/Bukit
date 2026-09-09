using System.Text.Json;

namespace Bukit.Cli.Deploy;

internal static class DeploymentReadinessValidator
{
    internal static string? Validate(string outputDir)
    {
        try
        {
            using var state = JsonDocument.Parse(File.ReadAllText(Path.Combine(outputDir, ".bukit-build-state.json")));
            var root = state.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("version", out var version) &&
                version.ValueKind == JsonValueKind.Number &&
                version.TryGetInt32(out var number) && number == 1 &&
                root.TryGetProperty("status", out var status) &&
                status.ValueKind == JsonValueKind.String &&
                string.Equals(status.GetString(), "completed", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Missing or unreadable state cannot prove this output finished building.
        }

        return "Build output is not ready for deployment. Rebuild the site successfully before deploying.";
    }
}
