using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Bukit.Engine;

internal static partial class MarkdownBodyProjection
{
    internal static string FromHtml(string html, string? sourceUrl = null)
    {
        var result = new StringBuilder(html.Length);
        var offset = 0;
        var search = 0;
        var close = -1;
        while (HtmlHeadScanner.FindStartTag(html, "a", search, html.Length) is var start && start >= 0)
        {
            var end = HtmlHeadScanner.FindTagEnd(html, start);
            // Nested malformed anchors share the next closing tag; do not rescan
            // the entire remainder for every opening anchor.
            if (close <= end) close = HtmlHeadScanner.FindClosingTagStart(html, "a", end + 1, html.Length);
            if (close < 0) break;
            search = end + 1;
            // Do not infer a target for malformed/nested anchors.
            if (HtmlHeadScanner.FindStartTag(html, "a", search, close) >= 0) continue;
            var href = ReadHref(html[start..(end + 1)]);
            var label = ReadLabel(html[(end + 1)..close]);
            if (!IsSafeTarget(href) || string.IsNullOrWhiteSpace(label)) continue;
            href = ResolveTarget(href!, sourceUrl);

            result.Append(EscapeText(SearchIndexBuilder.StripHtmlToText(html[offset..start])));
            result.Append(" [").Append(EscapeText(label)).Append("](<");
            foreach (var c in href!)
            {
                // Angle-delimited destinations retain parentheses and Unicode. Encode
                // delimiters/whitespace and entities without decoding a target twice.
                result.Append(c switch
                {
                    '&' => "&amp;",
                    '<' => "%3C",
                    '>' => "%3E",
                    '\\' => "%5C",
                    _ => char.IsWhiteSpace(c) ? Uri.EscapeDataString(c.ToString()) : c.ToString()
                });
            }
            result.Append(">) ");
            offset = HtmlHeadScanner.FindTagEnd(html, close) + 1;
            search = offset;
        }
        result.Append(EscapeText(SearchIndexBuilder.StripHtmlToText(html[offset..])));
        return result.ToString().Trim();
    }

    private static string ReadLabel(string html)
    {
        // Inline formatting must not split a word (Off<strong>icial</strong>).
        string[] inlineNames = ["strong", "em", "b", "i", "span", "code", "s", "u", "del", "mark", "small", "sub", "sup"];
        var result = new StringBuilder(html.Length);
        var offset = 0;
        while (html.IndexOf('<', offset) is var start && start >= 0)
        {
            var end = HtmlHeadScanner.FindTagEnd(html, start);
            if (end < 0) break;
            result.Append(html[offset..start]);
            var tag = html[start..(end + 1)];
            var opening = tag.StartsWith("</", StringComparison.Ordinal) ? "<" + tag[2..] : tag;
            if (!inlineNames.Any(name => HtmlHeadScanner.IsStartTag(opening, name))) result.Append(tag);
            offset = end + 1;
        }
        result.Append(html[offset..]);
        return SearchIndexBuilder.StripHtmlToText(result.ToString());
    }

    private static string ResolveTarget(string href, string? sourceUrl)
    {
        if (sourceUrl is null || href.StartsWith('/') || Uri.TryCreate(href, UriKind.Absolute, out _)) return href;
        // Projections live at different paths: resolve relative links and fragments
        // against the HTML page, never against content/*.md or llms-full.txt.
        var page = new Uri(new Uri("https://projection.invalid/"), sourceUrl);
        if (!Uri.TryCreate(page, href, out var target)) return href;
        return sourceUrl.StartsWith('/') ? target.PathAndQuery + target.Fragment : target.AbsoluteUri;
    }

    private static string? ReadHref(string tag)
    {
        if (!tag.StartsWith("<a", StringComparison.OrdinalIgnoreCase)) return null;
        // Match whole attributes so data-href and href text inside another value
        // cannot become links. The first duplicate href has HTML's precedence.
        for (var match = Attribute().Match(tag, 2); match.Success; match = match.NextMatch())
        {
            if (match.Groups["name"].Value.Equals("href", StringComparison.OrdinalIgnoreCase))
                return WebUtility.HtmlDecode(match.Groups["value"].Value).Trim();
        }
        return null;
    }

    private static bool IsSafeTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Any(char.IsControl)) return false;
        var colon = target.IndexOf(':');
        var path = target.IndexOfAny(['/', '?', '#']);
        if (colon < 0 || (path >= 0 && path < colon)) return true;
        return target[..colon].ToLowerInvariant() is "https" or "http" or "mailto" or "tel" or "ftp";
    }

    private static string EscapeText(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if ("\\`*_[]!".Contains(c)) result.Append('\\');
            result.Append(c switch { '&' => "&amp;", '<' => "&lt;", '>' => "&gt;", _ => c.ToString() });
        }
        return result.ToString();
    }

    [GeneratedRegex("\\G\\s+(?<name>[^\\s\"'<>/=]+)(?:\\s*=\\s*(?:\"(?<value>[^\"]*)\"|'(?<value>[^']*)'|(?<value>[^\\s\"'=<>`]+)))?", RegexOptions.CultureInvariant)]
    private static partial Regex Attribute();
}
