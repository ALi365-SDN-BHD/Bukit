using System.Buffers;
using System.Security.Cryptography;

namespace Bukit.Content.Media;

/// <summary>
/// Temp-file side of image downloads: size-limited writes with signature sniffing,
/// content identity, and atomic move into place.
/// </summary>
internal static class MediaTempFiles
{
    internal enum WriteStatus
    {
        Written,
        TooLarge,
        SignatureRejected
    }

    /// <summary>
    /// Outcome of writing a response body. <see cref="ContentType"/> is the header type, or the
    /// type identified from the leading bytes; it is set whenever any bytes were written.
    /// </summary>
    internal readonly record struct WriteResult(WriteStatus Status, long BytesWritten, string ContentType)
    {
        public static WriteResult TooLarge => new(WriteStatus.TooLarge, 0, string.Empty);
        public static WriteResult SignatureRejected => new(WriteStatus.SignatureRejected, 0, string.Empty);
    }

    internal static bool IsGenericBinaryContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        var normalized = contentType.Trim();
        return normalized.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("binary/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    // Without a known content type (generic binary response), the leading bytes must match a
    // raster signature before any file is created; the identified type is returned.
    internal static async Task<WriteResult> WriteWithLimitAsync(
        HttpContent content,
        string tempPath,
        long maxBytes,
        string? knownContentType,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is > 0 && content.Headers.ContentLength.Value > maxBytes)
        {
            return WriteResult.TooLarge;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(Math.Max(8192, ImageContentSignature.MaxSignatureBytes));
        try
        {
            await using var stream = await content.ReadAsStreamAsync(cancellationToken);
            var contentType = knownContentType ?? string.Empty;
            var headerLength = 0;
            if (knownContentType is null)
            {
                int headerRead;
                while (headerLength < ImageContentSignature.MaxSignatureBytes &&
                       (headerRead = await stream.ReadAsync(
                           buffer.AsMemory(headerLength, ImageContentSignature.MaxSignatureBytes - headerLength),
                           cancellationToken)) > 0)
                {
                    headerLength += headerRead;
                    if (headerLength > maxBytes)
                    {
                        return WriteResult.TooLarge;
                    }
                }

                if (headerLength > 0)
                {
                    var detected = ImageContentSignature.Detect(buffer.AsSpan(0, headerLength));
                    if (detected is null)
                    {
                        return WriteResult.SignatureRejected;
                    }

                    contentType = detected;
                }
            }

            var completed = false;
            await using var output = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            long totalRead = headerLength;
            try
            {
                if (headerLength > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, headerLength), cancellationToken);
                }

                int read;
                while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    totalRead += read;
                    if (totalRead > maxBytes)
                    {
                        return WriteResult.TooLarge;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                completed = true;
                return new WriteResult(WriteStatus.Written, totalRead, contentType);
            }
            finally
            {
                if (!completed)
                {
                    DeleteFileBestEffort(tempPath);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    internal static string BuildTempFilePath(string root, string fileName)
    {
        return Path.Combine(root, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    internal static async Task<FileContentIdentity> ReadFileContentIdentityAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var length = stream.Length;
        var sha256 = await SHA256.HashDataAsync(stream, cancellationToken);
        return new FileContentIdentity(length, sha256);
    }

    internal static bool MoveTempFileIntoPlace(string tempPath, string localPath, out bool winnerIsTrusted)
    {
        winnerIsTrusted = true;
        try
        {
            File.Move(tempPath, localPath);
            return true;
        }
        catch (IOException) when (File.Exists(localPath))
        {
            // Another writer won the race; the winner needs re-validation
            winnerIsTrusted = false;
            DeleteFileBestEffort(tempPath);
            return true;
        }
        catch
        {
            DeleteFileBestEffort(tempPath);
            return false;
        }
    }

    internal static void DeleteFileBestEffort(string path)
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

    internal readonly record struct FileContentIdentity(long Length, byte[] Sha256);
}
