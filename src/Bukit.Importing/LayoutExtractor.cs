using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class LayoutExtractor
{
    internal sealed record LayoutInfo(
        string Header,
        string Nav,
        string Footer,
        string HeadExtras,
        bool HeaderContainsNav);

    private static readonly Regex NavRegex = NavPattern();
    private static readonly Regex HeaderRegex = HeaderTagPattern();
    private static readonly Regex FooterRegex = FooterTagPattern();
    private static readonly Regex ClassAttrRegex = ClassAttrStripPattern();

    internal static LayoutInfo Extract(List<DiscoveredPage> pages, List<string> warnings)
    {
        if (pages.Count == 1)
        {
            var single = pages[0];
            var extractedHeader = ExtractByTag(single.BodyOpening, "header") ?? single.BodyOpening;
            var extractedNav = ExtractNavBlock(extractedHeader);
            var extractedFooter = ExtractByTag(single.BodyClosing, "footer") ?? single.BodyClosing;

            if (extractedHeader == single.BodyOpening && extractedHeader.Length > 0)
                warnings.Add("单页面: 未找到 <header> 标签，使用 BodyOpening 作为 header。建议手动审查。");
            if (extractedFooter == single.BodyClosing && extractedFooter.Length > 0)
                warnings.Add("单页面: 未找到 <footer> 标签，使用 BodyClosing 作为 footer。建议手动审查。");

            return new LayoutInfo(
                Header: string.IsNullOrWhiteSpace(extractedHeader) ? "" : extractedHeader,
                Nav: extractedNav,
                Footer: string.IsNullOrWhiteSpace(extractedFooter) ? "" : extractedFooter,
                HeadExtras: single.HeadContent ?? "",
                HeaderContainsNav: !string.IsNullOrWhiteSpace(extractedNav));
        }

        var normalizedOpenings = pages.Select(p => StripClassId(p.BodyOpening)).ToList();
        var normalizedClosings = pages.Select(p => StripClassId(p.BodyClosing)).ToList();

        var headerLines = FindLongestCommonPrefixLines(
            pages.Select(p => p.BodyOpening).ToList(),
            normalizedOpenings);
        var footerLines = FindLongestCommonSuffixLines(
            pages.Select(p => p.BodyClosing).ToList(),
            normalizedClosings);

        var headerContent = string.Join("\n", headerLines);

        if (headerContent.Length < 20 && headerContent.Length > 0)
            warnings.Add("检测到的 header 过短（< 20 字符），可能不准确");

        var footerContent = string.Join("\n", footerLines);

        if (footerContent.Length < 20 && footerContent.Length > 0)
            warnings.Add("检测到的 footer 过短（< 20 字符），可能不准确");

        var navContent = ExtractNavBlock(headerContent);

        var firstHead = pages[0].HeadContent ?? "";

        return new LayoutInfo(
            Header: headerContent,
            Nav: navContent,
            Footer: footerContent,
            HeadExtras: firstHead,
            HeaderContainsNav: !string.IsNullOrWhiteSpace(ExtractNavBlock(headerContent)));
    }

    private static string? ExtractByTag(string content, string tagName)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var openRegex = new Regex($"<{tagName}[\\s>]", RegexOptions.IgnoreCase);
        var match = openRegex.Match(content);
        if (!match.Success) return null;

        var rest = content[match.Index..];
        var closeIndex = FindClosingTagInString(rest, 0);
        if (closeIndex < 0) return null;

        var closeTagEnd = rest.IndexOf('>', closeIndex);
        if (closeTagEnd < 0) return null;

        return rest[..(closeTagEnd + 1)];
    }

    private static string StripClassId(string text)
    {
        return ClassAttrRegex.Replace(text, "");
    }

    private static string ExtractNavBlock(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return "";

        var match = NavRegex.Match(header);
        if (!match.Success)
            return "";

        var rest = header[match.Index..];
        var closeIndex = FindClosingTagInString(rest, 0);
        if (closeIndex < 0)
            return match.Value;

        var closeTagEnd = rest.IndexOf('>', closeIndex);
        if (closeTagEnd < 0)
            return match.Value;

        return rest[..(closeTagEnd + 1)];
    }

    private static int FindClosingTagInString(string text, int openTagStart)
    {
        var tagName = GetTagNameFromMarkup(text, openTagStart);
        if (string.IsNullOrEmpty(tagName))
            return -1;

        var closeTag = $"</{tagName}";
        var depth = 1;
        var pos = text.IndexOf('>', openTagStart);
        if (pos < 0) return -1;

        while (pos < text.Length && depth > 0)
        {
            var nextOpen = text.IndexOf($"<{tagName}", pos + 1, StringComparison.OrdinalIgnoreCase);
            var nextClose = text.IndexOf(closeTag, pos + 1, StringComparison.OrdinalIgnoreCase);

            if (nextClose < 0) break;

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                pos = nextOpen;
            }
            else
            {
                depth--;
                pos = nextClose;
            }
        }

        return depth == 0 ? pos : -1;
    }

    private static string GetTagNameFromMarkup(string markup, int tagStart)
    {
        var space = markup.IndexOf(' ', tagStart);
        var close = markup.IndexOf('>', tagStart);
        var end = (space > 0 && space < close) ? space : close;
        if (end < 0) return "";
        return markup[(tagStart + 1)..end].ToLowerInvariant();
    }

    private static List<string> FindLongestCommonPrefixLines(
        List<string> originalTexts, List<string> normalizedTexts)
    {
        if (originalTexts.Count == 0) return [];
        if (originalTexts.Any(string.IsNullOrEmpty)) return [];

        var originalLines = originalTexts.Select(t => t.Split('\n').ToList()).ToList();
        var normalizedLines = normalizedTexts.Select(t => t.Split('\n').ToList()).ToList();
        var minLines = originalLines.Min(l => l.Count);
        var result = new List<string>();

        for (var i = 0; i < minLines; i++)
        {
            var normLine = normalizedLines[0][i];
            if (normalizedLines.All(l => i < l.Count &&
                    string.Equals(l[i], normLine, StringComparison.Ordinal)))
            {
                result.Add(originalLines[0][i]);
            }
            else
            {
                break;
            }
        }

        return result;
    }

    private static List<string> FindLongestCommonSuffixLines(
        List<string> originalTexts, List<string> normalizedTexts)
    {
        if (originalTexts.Count == 0) return [];
        if (originalTexts.Any(string.IsNullOrEmpty)) return [];

        var originalLines = originalTexts.Select(t => t.Split('\n').ToList()).ToList();
        var normalizedLines = normalizedTexts.Select(t => t.Split('\n').ToList()).ToList();
        var minLines = originalLines.Min(l => l.Count);
        var result = new List<string>();

        for (var i = 0; i < minLines; i++)
        {
            var origIdx = originalLines[0].Count - 1 - i;
            if (origIdx < 0) break;

            var normLine = normalizedLines[0][normalizedLines[0].Count - 1 - i];
            if (normalizedLines.All(l =>
                {
                    var ni = l.Count - 1 - i;
                    var oi = originalLines[normalizedLines.IndexOf(l)].Count - 1 - i;
                    return ni >= 0 && oi >= 0 &&
                        string.Equals(l[ni], normLine, StringComparison.Ordinal);
                }))
            {
                result.Insert(0, originalLines[0][origIdx]);
            }
            else
            {
                break;
            }
        }

        return result;
    }

    internal static string NormalizeBlock(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        var lines = content.Split('\n');
        var minIndent = lines
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Min(l => l.Length - l.TrimStart().Length);

        if (minIndent <= 0) return content;

        return string.Join('\n', lines.Select(l =>
            l.Length >= minIndent ? l[minIndent..] : l.TrimStart()));
    }

    [GeneratedRegex(@"<nav[\s>]", RegexOptions.IgnoreCase)]
    private static partial Regex NavPattern();

    [GeneratedRegex(@"<header[\s>]", RegexOptions.IgnoreCase)]
    private static partial Regex HeaderTagPattern();

    [GeneratedRegex(@"<footer[\s>]", RegexOptions.IgnoreCase)]
    private static partial Regex FooterTagPattern();

    [GeneratedRegex(@"\s+(?:class|id)\s*=\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex ClassAttrStripPattern();
}
