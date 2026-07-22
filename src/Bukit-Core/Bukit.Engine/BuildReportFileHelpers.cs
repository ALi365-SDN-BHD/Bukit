using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bukit.Engine;

internal sealed record BuildReportFileEntry(string Path, string Hash, long Size);

internal static class BuildReportFileEntryFactory
{
    internal static BuildReportFileEntry Create(string rootDir, string path)
    {
        return new BuildReportFileEntry(
            BuildReporter.NormalizePath(Path.GetRelativePath(rootDir, path)),
            BuildReportHashing.ComputeSha256(path),
            new FileInfo(path).Length);
    }
}

internal static class BuildReportFileEntryWriter
{
    internal static void Write(Utf8JsonWriter writer, BuildReportFileEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("path", entry.Path);
        writer.WriteString("hash", entry.Hash);
        writer.WriteNumber("size", entry.Size);
        writer.WriteEndObject();
    }
}

internal static class BuildReportHashing
{
    internal static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static string ComputeBundleHash(IReadOnlyList<BuildReportFileEntry> files)
    {
        using var sha = SHA256.Create();
        foreach (var file in files)
        {
            var line = $"{file.Path}|{file.Hash}|{file.Size}\n";
            var bytes = Encoding.UTF8.GetBytes(line);
            sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return $"sha256:{Convert.ToHexStringLower(sha.Hash!)}";
    }
}
