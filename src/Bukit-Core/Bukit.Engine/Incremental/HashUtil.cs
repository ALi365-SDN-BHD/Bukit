using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using Bukit.Shared;

namespace Bukit.Engine.Incremental;

public static class HashUtil
{
    public static string Sha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        return Sha256Hex(bytes);
    }

    public static string Sha256Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return ToHexLower(hash);
    }

    public static string Sha256HexForDirectory(string rootDir, int maxFiles = 10000, long maxTotalSize = 100 * 1024 * 1024)
    {
        if (!Directory.Exists(rootDir))
        {
            return Sha256Hex(string.Empty);
        }

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> separator = stackalloc byte[1];
        separator[0] = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        var files = SafeFileEnumerator.EnumerateFiles(rootDir)
            .Select(p => new
            {
                FullPath = p,
                Relative = Path.GetRelativePath(rootDir, p).Replace('\\', '/')
            })
            .OrderBy(x => x.Relative, StringComparer.Ordinal)
            .ToList();

        try
        {
            var totalSize = 0L;
            var fileCount = files.Count;
            var processedCount = 0;

            if (fileCount > maxFiles)
            {
                WarnOnce($"Directory '{rootDir}' has {fileCount} files, exceeding limit of {maxFiles}. Only first {maxFiles} files will be processed.");
            }

            foreach (var f in files)
            {
                if (processedCount >= maxFiles)
                {
                    break;
                }

                var nameBytes = Encoding.UTF8.GetBytes(f.Relative);
                hasher.AppendData(nameBytes);
                hasher.AppendData(separator);

                var fileInfo = new FileInfo(f.FullPath);
                var fileLength = fileInfo.Length;

                if (totalSize + fileLength > maxTotalSize && processedCount > 0)
                {
                    WarnOnce($"Directory '{rootDir}' total size exceeds limit of {maxTotalSize} bytes. Stopping content processing at file {processedCount} of {fileCount}.");
                    hasher.AppendData(Encoding.UTF8.GetBytes($"skip:{f.Relative}:{fileLength}"));
                    hasher.AppendData(separator);
                    processedCount++;
                    continue;
                }

                totalSize += fileLength;

                using var stream = File.OpenRead(f.FullPath);
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hasher.AppendData(buffer.AsSpan(0, read));
                }

                hasher.AppendData(separator);
                processedCount++;
            }

            for (var i = processedCount; i < fileCount; i++)
            {
                var f = files[i];
                var nameBytes = Encoding.UTF8.GetBytes(f.Relative);
                hasher.AppendData(nameBytes);
                hasher.AppendData(separator);
                var fileLength = new FileInfo(f.FullPath).Length;
                hasher.AppendData(Encoding.UTF8.GetBytes($"skip:{f.Relative}:{fileLength}"));
                hasher.AppendData(separator);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        var digest = hasher.GetHashAndReset();
        return ToHexLower(digest);
    }

    private static void WarnOnce(string message)
    {
        Console.Error.WriteLine($"[warn] {message}");
    }

    public static string ToHexLower(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i * 2] = GetHexLower(b >> 4);
            chars[i * 2 + 1] = GetHexLower(b & 0xF);
        }

        return new string(chars);
    }

    private static char GetHexLower(int value)
    {
        return (char)(value < 10 ? ('0' + value) : ('a' + (value - 10)));
    }
}
