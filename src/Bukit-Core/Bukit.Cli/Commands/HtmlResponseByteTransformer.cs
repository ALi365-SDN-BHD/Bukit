using System.Text;

namespace Bukit.Cli.Commands;

internal static class HtmlResponseByteTransformer
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding StrictUtf16Le = new(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding StrictUtf16Be = new(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly UTF32Encoding StrictUtf32Le = new(bigEndian: false, byteOrderMark: false, throwOnInvalidCharacters: true);
    private static readonly UTF32Encoding StrictUtf32Be = new(bigEndian: true, byteOrderMark: false, throwOnInvalidCharacters: true);
    private static readonly byte[] Utf16LePreamble = [0xFF, 0xFE];
    private static readonly byte[] Utf16BePreamble = [0xFE, 0xFF];
    private static readonly byte[] Utf32LePreamble = [0xFF, 0xFE, 0x00, 0x00];
    private static readonly byte[] Utf32BePreamble = [0x00, 0x00, 0xFE, 0xFF];

    internal static byte[] RewriteUtf8(byte[] source, Func<string, string> rewrite)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rewrite);

        var hasBom = source.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        var content = hasBom ? source.AsSpan(Encoding.UTF8.Preamble.Length) : source.AsSpan();

        string html;
        try
        {
            html = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException ex)
        {
            var bytePreservingProbe = DecodeStructuralProbe(source, content, ex);
            if (string.Equals(bytePreservingProbe, rewrite(bytePreservingProbe), StringComparison.Ordinal))
            {
                return source;
            }

            throw new InvalidDataException("HTML rewrite requires valid UTF-8 input.", ex);
        }

        var rewritten = rewrite(html);
        if (string.Equals(html, rewritten, StringComparison.Ordinal))
        {
            return source;
        }

        var rewrittenBytes = StrictUtf8.GetBytes(rewritten);
        if (!hasBom)
        {
            return rewrittenBytes;
        }

        var result = new byte[Encoding.UTF8.Preamble.Length + rewrittenBytes.Length];
        Encoding.UTF8.Preamble.CopyTo(result);
        rewrittenBytes.CopyTo(result, Encoding.UTF8.Preamble.Length);
        return result;
    }

    private static string DecodeStructuralProbe(byte[] source, ReadOnlySpan<byte> utf8Content, DecoderFallbackException utf8Exception)
    {
        try
        {
            if (source.AsSpan().StartsWith(Utf32LePreamble))
            {
                return StrictUtf32Le.GetString(source.AsSpan(Utf32LePreamble.Length));
            }

            if (source.AsSpan().StartsWith(Utf32BePreamble))
            {
                return StrictUtf32Be.GetString(source.AsSpan(Utf32BePreamble.Length));
            }

            if (source.AsSpan().StartsWith(Utf16LePreamble))
            {
                return StrictUtf16Le.GetString(source.AsSpan(Utf16LePreamble.Length));
            }

            if (source.AsSpan().StartsWith(Utf16BePreamble))
            {
                return StrictUtf16Be.GetString(source.AsSpan(Utf16BePreamble.Length));
            }

            return Encoding.Latin1.GetString(utf8Content);
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException("HTML rewrite requires valid UTF-8 input.", new AggregateException(utf8Exception, ex));
        }
    }
}
