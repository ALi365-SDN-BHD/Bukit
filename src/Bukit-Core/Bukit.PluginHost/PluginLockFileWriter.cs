using System.Globalization;
using YamlDotNet.RepresentationModel;

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

        var root = new YamlMappingNode
        {
            { "version", "1" }
        };
        var resolved = new YamlMappingNode();
        foreach (PluginLockEntry entry in entries.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            var commands = new YamlSequenceNode();
            foreach (string command in entry.Commands ?? [])
            {
                commands.Add(command);
            }

            resolved.Add(
                entry.Id,
                new YamlMappingNode
                {
                    { "source", entry.Source },
                    { "manifestVersion", entry.ManifestVersion },
                    { "protocol", entry.Protocol },
                    { "platform", entry.Platform },
                    { "entry", entry.Entry },
                    { "sha256", entry.Sha256 },
                    { "commands", commands },
                    { "resolvedAt", entry.ResolvedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) },
                    { "sha256Verified", entry.Sha256Verified.ToString().ToLowerInvariant() }
                });
        }

        root.Add("resolved", resolved);

        var stream = new YamlStream(new YamlDocument(root));
        await using var file = File.Create(lockPath);
        await using var writer = new StreamWriter(file);
        stream.Save(writer, assignAnchors: false);
        await writer.FlushAsync(cancellationToken);
    }
}
