using System.Text;
using Bukit.Config;
using Bukit.Shared;
using Bukit.Shared.IO;

namespace Bukit.Engine.Plugins.BuiltIn.DataFiles;

/// <summary>
/// Opens data files without following links and decodes them with strict,
/// size-bounded readers.
/// </summary>
internal static class DataFileReaders
{
    internal static Stream OpenVerifiedDataFile(string path, string dataRoot)
    {
        var verified = new PlatformSafeSourceFileOpener().Open(path, dataRoot);
        return new VerifiedDataFileStream(verified);
    }

    internal static TextReader CreateStrictTextReader(
        Stream input,
        long maxDecodedChars,
        string relativePath,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[5];
        var prefixLength = 0;
        while (prefixLength < prefix.Length)
        {
            var read = input.Read(prefix, prefixLength, prefix.Length - prefixLength);
            if (read == 0)
            {
                break;
            }

            prefixLength += read;
        }

        Encoding encoding;
        var bomLength = 0;
        if (StartsWith(prefix, prefixLength, [0xFF, 0xFE, 0x00, 0x00])
            || StartsWith(prefix, prefixLength, [0x00, 0x00, 0xFE, 0xFF])
            || IsUtf7Bom(prefix, prefixLength))
        {
            throw new ConfigException(
                $"Unsupported data file encoding at {relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }
        else if (StartsWith(prefix, prefixLength, [0xEF, 0xBB, 0xBF]))
        {
            encoding = new UTF8Encoding(false, true);
            bomLength = 3;
        }
        else if (StartsWith(prefix, prefixLength, [0xFF, 0xFE]))
        {
            encoding = new UnicodeEncoding(false, false, true);
            bomLength = 2;
        }
        else if (StartsWith(prefix, prefixLength, [0xFE, 0xFF]))
        {
            encoding = new UnicodeEncoding(true, false, true);
            bomLength = 2;
        }
        else
        {
            encoding = new UTF8Encoding(false, true);
        }

        var prefixedInput = new PrefixReadStream(
            input,
            prefix,
            bomLength,
            prefixLength - bomLength);
        var streamReader = new StreamReader(
            prefixedInput,
            encoding,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: false);
        return new StrictBoundedTextReader(
            streamReader,
            maxDecodedChars,
            relativePath,
            cancellationToken);
    }

    private static bool StartsWith(byte[] value, int valueLength, byte[] prefix)
        => valueLength >= prefix.Length
            && value.AsSpan(0, prefix.Length).SequenceEqual(prefix);

    private static bool IsUtf7Bom(byte[] value, int valueLength)
        => valueLength >= 5
            && value[0] == 0x2B
            && value[1] == 0x2F
            && value[2] == 0x76
            && value[3] is 0x38 or 0x39 or 0x2B or 0x2F
            && value[4] == 0x2D;
}

internal sealed class VerifiedDataFileStream(VerifiedSourceFile verified) : Stream
{
    private Stream Inner => verified.Stream;

    public override bool CanRead => Inner.CanRead;
    public override bool CanSeek => Inner.CanSeek;
    public override bool CanWrite => Inner.CanWrite;
    public override long Length => Inner.Length;
    public override long Position
    {
        get => Inner.Position;
        set => Inner.Position = value;
    }

    public override void Flush() => Inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => Inner.Read(buffer);
    public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
    public override void SetLength(long value) => Inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            verified.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class BudgetedReadStream(Stream inner, Action<int> recordRead) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => inner.Length;
    public override long Position
    {
        get => inner.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = inner.Read(buffer, offset, count);
        recordRead(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        var read = inner.Read(buffer);
        recordRead(read);
        return read;
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class PrefixReadStream(
    Stream inner,
    byte[] prefix,
    int prefixOffset,
    int prefixCount) : Stream
{
    private int _prefixOffset = prefixOffset;
    private readonly int _prefixEnd = prefixOffset + prefixCount;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
        => Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        var copied = Math.Min(buffer.Length, _prefixEnd - _prefixOffset);
        if (copied > 0)
        {
            prefix.AsSpan(_prefixOffset, copied).CopyTo(buffer);
            _prefixOffset += copied;
            if (copied == buffer.Length)
            {
                return copied;
            }
        }

        return copied + inner.Read(buffer[copied..]);
    }

    public override void Flush() { }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}

internal sealed class StrictBoundedTextReader(
    TextReader inner,
    long maxChars,
    string relativePath,
    CancellationToken cancellationToken) : TextReader
{
    private long _chars;

    public override int Peek()
    {
        cancellationToken.ThrowIfCancellationRequested();
        return inner.Peek();
    }

    public override int Read()
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = inner.Read();
        if (value >= 0) Record(1);
        return value;
    }

    public override int Read(char[] buffer, int index, int count)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var read = inner.Read(buffer, index, count);
        Record(read);
        return read;
    }

    public override int Read(Span<char> buffer)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var read = inner.Read(buffer);
        Record(read);
        return read;
    }

    private void Record(int read)
    {
        if (_chars > maxChars - read)
        {
            throw new ConfigException(
                $"Data file decodes to more than {maxChars} characters at {relativePath}.",
                DiagnosticCode.ConfigInvalidValue);
        }

        _chars += read;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }
}
