using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Bukit.Engine.Abstractions.Content;
using Bukit.Engine.Incremental;
using Bukit.Shared;

namespace Bukit.Engine;

internal static class I18nWorktreeProtocolStore
{
    internal static async Task WriteContentAsync(string path, I18nContentSnapshot snapshot, CancellationToken cancellationToken)
    {
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidOperationException("Content snapshot path has no parent directory.");
        var bodyDirectory = Path.Combine(manifestDirectory, "bodies");
        Directory.CreateDirectory(bodyDirectory);
        var files = new Dictionary<string, I18nBodyFileSnapshot>(StringComparer.Ordinal);
        var index = 0;
        foreach (var (key, body) in snapshot.Bodies.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.Combine("bodies", $"{index++:D8}.html");
            var bodyPath = Path.Combine(manifestDirectory, relativePath);
            await File.WriteAllTextAsync(bodyPath, body.Html, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            RestrictPermissions(bodyPath);
            files[key] = new I18nBodyFileSnapshot(
                relativePath.Replace(Path.DirectorySeparatorChar, '/'),
                HashUtil.Sha256Hex(Encoding.UTF8.GetBytes(body.Html)));
        }

        await WriteAsync(
            path,
            snapshot with
            {
                Bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal),
                BodyFiles = files
            },
            I18nWorktreeJsonContext.Default.I18nContentSnapshot,
            cancellationToken).ConfigureAwait(false);
    }

    internal static Task WriteRequestAsync(string path, I18nWorkerRequest request, CancellationToken cancellationToken)
        => WriteAsync(path, request, I18nWorktreeJsonContext.Default.I18nWorkerRequest, cancellationToken);

    internal static Task WriteResultAsync(string path, I18nVariantSnapshot result, CancellationToken cancellationToken)
        => WriteAsync(path, result, I18nWorktreeJsonContext.Default.I18nVariantSnapshot, cancellationToken);

    internal static async Task<I18nContentSnapshot> ReadContentAsync(string path, CancellationToken cancellationToken)
    {
        var snapshot = await ReadAsync(path, I18nWorktreeJsonContext.Default.I18nContentSnapshot, cancellationToken).ConfigureAwait(false);
        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new InvalidDataException("Content snapshot path has no parent directory.");
        var bodies = new Dictionary<string, ContentBody>(StringComparer.Ordinal);
        foreach (var (key, file) in snapshot.BodyFiles)
        {
            var bodyPath = FileWriter.GetSafeFullPath(manifestDirectory, file.Path);
            var html = await File.ReadAllTextAsync(bodyPath, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var actualHash = HashUtil.Sha256Hex(Encoding.UTF8.GetBytes(html));
            if (!string.Equals(actualHash, file.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Content snapshot body hash is invalid: {file.Path}");
            }
            bodies[key] = new ContentBody(html);
        }
        return snapshot with { Bodies = bodies };
    }

    internal static Task<I18nWorkerRequest> ReadRequestAsync(string path, CancellationToken cancellationToken)
        => ReadAsync(path, I18nWorktreeJsonContext.Default.I18nWorkerRequest, cancellationToken);

    internal static Task<I18nVariantSnapshot> ReadResultAsync(string path, CancellationToken cancellationToken)
        => ReadAsync(path, I18nWorktreeJsonContext.Default.I18nVariantSnapshot, cancellationToken);

    internal static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static async Task WriteAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Protocol path has no parent directory.");
        Directory.CreateDirectory(directory);
        var tempPath = fullPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            RestrictPermissions(tempPath);
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static async Task<T> ReadAsync<T>(string path, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Protocol payload is empty.");
    }

    private static void RestrictPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
