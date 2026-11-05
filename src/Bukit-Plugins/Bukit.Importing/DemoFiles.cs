using System.IO.Enumeration;
using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Importing;

/// <summary>Reads files of an HTML demo.</summary>
internal static partial class DemoFiles
{
    // Symbolic links are skipped: they can point outside the demo, and their targets
    // would otherwise be copied into the site.
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        IgnoreInaccessible = false
    };

    static DemoFiles() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Files under <paramref name="root"/>, not following symbolic links.</summary>
    internal static string[] Enumerate(string root, string pattern = "*")
        => Directory.GetFiles(root, pattern, Recursive);

    /// <summary>Directories under <paramref name="root"/>, not following symbolic links.</summary>
    internal static string[] EnumerateDirectories(string root, string pattern = "*")
        => Directory.GetDirectories(root, pattern, Recursive);

    /// <summary>Symbolic links under <paramref name="root"/>, which the import skips.</summary>
    internal static IEnumerable<string> SymbolicLinks(string root)
        // Reports each link once and never descends into one, so a link to a parent
        // folder or to a large tree outside the demo is not walked.
        => new FileSystemEnumerable<string>(
            root,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => IsLink(entry.Attributes),
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !IsLink(entry.Attributes)
        };

    private static bool IsLink(FileAttributes attributes) => (attributes & FileAttributes.ReparsePoint) != 0;

    /// <summary>
    /// Reads an HTML file in the encoding it declares: a byte order mark, then
    /// &lt;meta charset&gt; or a Content-Type meta. See <see cref="DecodeHtml"/> for
    /// files that declare none.
    /// </summary>
    internal static string ReadHtml(string path, out string? guessedEncoding)
        => DecodeHtml(File.ReadAllBytes(path), out guessedEncoding);

    internal static string DecodeHtml(byte[] bytes) => DecodeHtml(bytes, out _);

    /// <summary>
    /// Decodes HTML bytes. A file that declares no encoding and is not valid UTF-8 is
    /// read as GB18030 when that yields Chinese text, else as Windows-1252;
    /// <paramref name="guessedEncoding"/> then names the guess so the import can say so.
    /// </summary>
    internal static string DecodeHtml(byte[] bytes, out string? guessedEncoding)
    {
        guessedEncoding = null;
        if (bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }))
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE }))
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF }))
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

        var declared = DeclaredEncoding(bytes);
        if (declared is not null && declared.CodePage != Encoding.UTF8.CodePage)
        {
            // Files re-saved as UTF-8 often keep their old <meta charset="gb2312">. Valid
            // multi-byte UTF-8 practically never occurs by chance in GBK text, so trust the bytes.
            if (bytes.Any(b => b > 0x7F) && TryDecodeUtf8(bytes) is { } utf8Text)
            {
                guessedEncoding = $"UTF-8 (declared {declared.WebName})";
                return utf8Text;
            }

            return declared.GetString(bytes);
        }

        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException) when (declared is not null)
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            if (TryDecodeChinese(bytes) is { } chinese)
            {
                guessedEncoding = "GB18030";
                return chinese;
            }

            guessedEncoding = "Windows-1252";
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    private static string? TryDecodeUtf8(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    // GB18030 only when every byte decodes and most non-ASCII characters are Chinese;
    // Western text in a legacy single-byte encoding fails one of the two.
    private static string? TryDecodeChinese(byte[] bytes)
    {
        string text;
        try
        {
            text = Encoding.GetEncoding("GB18030", EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        var nonAscii = text.Count(c => c > 0x7F);
        var chinese = text.Count(c => c is >= '\u4E00' and <= '\u9FFF' or >= '\u3000' and <= '\u303F' or >= '\uFF00' and <= '\uFFEF');
        return nonAscii > 0 && chinese * 10 >= nonAscii * 8 ? text : null;
    }

    private static Encoding? DeclaredEncoding(byte[] bytes)
    {
        // Charset declarations must be in the first 1024 bytes and are ASCII.
        var head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 1024));
        var match = CharsetPattern().Match(head);
        if (!match.Success)
            return null;

        var name = match.Groups["name"].Value.Trim();
        // Browsers read gb2312 and gbk labels as GBK; GB18030 is its superset.
        if (name.Equals("gb2312", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("gbk", StringComparison.OrdinalIgnoreCase))
        {
            name = "GB18030";
        }

        try
        {
            return Encoding.GetEncoding(name);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"<meta\b[^>]*?charset\s*=\s*[""']?(?<name>[A-Za-z0-9_\-:.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CharsetPattern();
}
