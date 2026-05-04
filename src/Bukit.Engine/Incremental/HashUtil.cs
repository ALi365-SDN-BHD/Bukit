using System.Buffers;
using System.Security.Cryptography;
using System.Text;

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

    public static string Sha256HexForDirectory(string rootDir)
    {
        if (!Directory.Exists(rootDir))
        {
            return Sha256Hex(string.Empty);
        }

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> separator = stackalloc byte[1];
        separator[0] = 0;
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);

        var files = Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories)
            .Select(p => new
            {
                FullPath = p,
                Relative = Path.GetRelativePath(rootDir, p).Replace('\\', '/')
            })
            .OrderBy(x => x.Relative, StringComparer.Ordinal)
            .ToList();

        try
        {
            foreach (var f in files)
            {
                var nameBytes = Encoding.UTF8.GetBytes(f.Relative);
                hasher.AppendData(nameBytes);
                hasher.AppendData(separator);

                using var stream = File.OpenRead(f.FullPath);
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hasher.AppendData(buffer.AsSpan(0, read));
                }

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
