using Bukit.Shared;

namespace Bukit.Engine.Incremental;

/// <summary>
/// Forwards one page's HTML transform log to the build logger and keeps its warnings, so the
/// incremental manifest can report them again when a later build skips the unchanged page.
/// </summary>
internal sealed class RenderWarningRecorder(ILogger inner) : ILogger
{
    private List<string>? _warnings;

    /// <summary>The warnings logged so far, or null when there were none.</summary>
    internal IReadOnlyList<string>? Warnings => _warnings;

    public void Debug(string message) => inner.Debug(message);

    public void Info(string message) => inner.Info(message);

    public void Warn(string message)
    {
        (_warnings ??= []).Add(message);
        inner.Warn(message);
    }

    public void Error(string message) => inner.Error(message);

    /// <summary>Logs the warnings a skipped page's last render recorded.</summary>
    internal static void Replay(BuildManifestEntry entry, ILogger logger)
    {
        if (entry.RenderWarnings is not { } warnings)
        {
            return;
        }

        foreach (var warning in warnings)
        {
            logger.Warn(warning);
        }
    }
}
