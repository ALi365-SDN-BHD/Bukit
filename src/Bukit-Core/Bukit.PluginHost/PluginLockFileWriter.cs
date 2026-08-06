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
        string tempPath = Path.Combine(
            bukitDirectory,
            $".{Path.GetFileName(lockPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var file = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var writer = new StreamWriter(file))
            {
                stream.Save(writer, assignAnchors: false);
                await writer.FlushAsync(cancellationToken);
                file.Flush(flushToDisk: true);
            }

            if (File.Exists(lockPath))
            {
                File.Replace(tempPath, lockPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, lockPath);
            }
        }
        catch
        {
            DeleteFileBestEffort(tempPath);
            throw;
        }
    }

    private static void DeleteFileBestEffort(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
