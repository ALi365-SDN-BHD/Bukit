using System.Buffers;
using System.Globalization;
using System.Text;

namespace Bukit.Engine.Feeds;

/// <summary>
/// RFC 3986 form of a sitemap URL. Characters outside the URI character set (non-ASCII, space,
/// <c>" &lt; &gt; \ ^ ` { | }</c>, and a <c>%</c> that does not start an escape) are percent-encoded as
/// UTF-8 after the scheme and authority. Reserved characters and valid escapes are kept, so encoding
/// an encoded URL returns it unchanged.
/// </summary>
internal static class SitemapUri
{
    private static readonly SearchValues<char> UriChars = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~:/?#[]@!$&'()*+,;=");

    private static readonly SearchValues<char> SchemeChars = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+-.");

    internal static string Encode(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        var index = AuthorityEnd(url);
        while (index < url.Length && IsUriChar(url, index))
        {
            index++;
        }

        if (index == url.Length)
        {
            return url;
        }

        var builder = new StringBuilder(url.Length + 32).Append(url, 0, index);
        Span<byte> bytes = stackalloc byte[4];
        while (index < url.Length)
        {
            if (IsUriChar(url, index))
            {
                builder.Append(url[index++]);
                continue;
            }

            var length = char.IsHighSurrogate(url[index]) && index + 1 < url.Length && char.IsLowSurrogate(url[index + 1]) ? 2 : 1;
            var count = Encoding.UTF8.GetBytes(url.AsSpan(index, length), bytes);
            foreach (var value in bytes[..count])
            {
                builder.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            index += length;
        }

        return builder.ToString();
    }

    private static bool IsUriChar(string url, int index)
        => url[index] == '%'
            ? index + 2 < url.Length && char.IsAsciiHexDigit(url[index + 1]) && char.IsAsciiHexDigit(url[index + 2])
            : UriChars.Contains(url[index]);

    // The scheme and authority come from site.url and are left as written.
    private static int AuthorityEnd(string url)
    {
        var separator = url.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0 || !char.IsAsciiLetter(url[0]) ||
            url.AsSpan(0, separator).ContainsAnyExcept(SchemeChars))
        {
            return 0;
        }

        var authorityEnd = url.AsSpan(separator + 3).IndexOfAny('/', '?', '#');
        return authorityEnd < 0 ? url.Length : separator + 3 + authorityEnd;
    }
}
