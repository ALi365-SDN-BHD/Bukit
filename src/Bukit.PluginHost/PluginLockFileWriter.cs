using System.Text;

namespace Bukit.PluginHost;

public sealed class PluginLockFileWriter
{
    public async Task WriteAsync(
        string projectRoot,
        IReadOnlyList<PluginLockEntry> entries,
        CancellationToken cancellationToken)
    {
        string bukitDirectory = Path.Combine(projectRoot, ".bukit");
        Directory.CreateDirectory(bukitDirectory);
        string lockPath = Path.Combine(bukitDirectory, "plugins.lock.yaml");

        var builder = new StringBuilder();
        builder.AppendLine("version: 1");
        builder.AppendLine("plugins:");
        foreach (PluginLockEntry entry in entries.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            builder.AppendLine($"  {Escape(entry.Id)}:");
            builder.AppendLine($"    version: {Escape(entry.Version)}");
            builder.AppendLine($"    source: {Escape(entry.Source)}");
            builder.AppendLine($"    entry: {Escape(entry.Entry)}");
            builder.AppendLine($"    platform: {Escape(entry.Platform)}");
            builder.AppendLine($"    sha256: {Escape(entry.Sha256)}");
            builder.AppendLine($"    sha256Verified: {entry.Sha256Verified.ToString().ToLowerInvariant()}");
        }

        await File.WriteAllTextAsync(lockPath, builder.ToString(), cancellationToken);
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(":", "\\:", StringComparison.Ordinal);
}
