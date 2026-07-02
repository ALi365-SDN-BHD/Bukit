using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Bukit.Labs.Cli.Commands;

internal sealed record PngImage(int Width, int Height, byte[] Pixels)
{
    public static PngImage Read(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8];
        ReadOnlySpan<byte> expectedSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (stream.Read(signature) != 8 || !signature.SequenceEqual(expectedSignature))
            throw new InvalidOperationException("not a PNG file");

        var width = 0;
        var height = 0;
        var bitDepth = 0;
        var colorType = 0;
        using var idat = new MemoryStream();

        var header = new byte[8];
        while (stream.Position < stream.Length)
        {
            if (stream.Read(header) != 8)
                break;
            var length = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
            var type = Encoding.ASCII.GetString(header.AsSpan(4, 4));
            var data = new byte[length];
            if (stream.Read(data) != length)
                throw new InvalidOperationException("truncated PNG chunk");
            stream.Position += 4;

            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(0, 4));
                height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4, 4));
                bitDepth = data[8];
                colorType = data[9];
            }
            else if (type == "IDAT")
            {
                idat.Write(data, 0, data.Length);
            }
            else if (type == "IEND")
            {
                break;
            }
        }

        if (width <= 0 || height <= 0)
            throw new InvalidOperationException("missing IHDR");
        if (bitDepth != 8 || colorType is not (2 or 6))
            throw new InvalidOperationException($"unsupported PNG format bitDepth={bitDepth} colorType={colorType}");

        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);

        var bytesPerPixel = colorType == 6 ? 4 : 3;
        var stride = width * bytesPerPixel;
        var filtered = raw.ToArray();
        var pixels = new byte[width * height * 4];
        var previous = new byte[stride];
        var current = new byte[stride];
        var offset = 0;
        for (var y = 0; y < height; y++)
        {
            var filter = filtered[offset++];
            Array.Copy(filtered, offset, current, 0, stride);
            offset += stride;
            Unfilter(current, previous, bytesPerPixel, filter);
            for (var x = 0; x < width; x++)
            {
                var src = x * bytesPerPixel;
                var dst = ((y * width) + x) * 4;
                pixels[dst] = current[src];
                pixels[dst + 1] = current[src + 1];
                pixels[dst + 2] = current[src + 2];
                pixels[dst + 3] = bytesPerPixel == 4 ? current[src + 3] : (byte)255;
            }
            (previous, current) = (current, previous);
            Array.Clear(current);
        }

        return new PngImage(width, height, pixels);
    }

    private static void Unfilter(byte[] row, byte[] previous, int bytesPerPixel, int filter)
    {
        for (var i = 0; i < row.Length; i++)
        {
            var left = i >= bytesPerPixel ? row[i - bytesPerPixel] : 0;
            var up = previous[i];
            var upperLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : 0;
            row[i] = filter switch
            {
                0 => row[i],
                1 => unchecked((byte)(row[i] + left)),
                2 => unchecked((byte)(row[i] + up)),
                3 => unchecked((byte)(row[i] + ((left + up) / 2))),
                4 => unchecked((byte)(row[i] + Paeth(left, up, upperLeft))),
                _ => throw new InvalidOperationException($"unsupported PNG filter {filter}")
            };
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }
}
