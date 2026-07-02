using System.Text.RegularExpressions;

namespace Bukit.Labs.Cli.Commands;

internal sealed record CommonBlocks(string Header, string Nav, string Footer);

internal static class CloneFidelityCommonBlocks
{
    internal static CommonBlocks ExtractCommonBlocks(List<FidelityPage> pages, List<string> warnings)
    {
        if (pages.Count <= 1)
        {
            var p = pages[0];
            return new CommonBlocks(
                NormalizeBlock(p.BodyOpening),
                "",
                NormalizeBlock(p.BodyClosing));
        }

        var openings = pages.Select(p => p.BodyOpening).ToList();
        var closings = pages.Select(p => p.BodyClosing).ToList();

        var header = FindLongestCommonPrefixLines(openings);
        var footer = FindLongestCommonSuffixLines(closings);

        var nav = "";
        var navPattern = new Regex(@"<nav[\s>]", RegexOptions.IgnoreCase);
        if (header.Length > 0)
        {
            var navMatch = navPattern.Match(header);
            if (navMatch.Success)
            {
                var navStart = navMatch.Index;
                var navEnd = FindClosingTagInString(header, navStart);
                if (navEnd > navStart)
                {
                    nav = header[navStart..(navEnd + 1)];
                    header = header[..navStart] + header[(navEnd + 1)..];
                }
            }
        }

        if (header.Length < 20 && pages[0].BodyOpening.Length > 20)
        {
            warnings.Add("Could not reliably detect common header across all pages. Each page keeps its own header.");
            header = "";
        }

        if (footer.Length < 20 && pages[0].BodyClosing.Length > 20)
        {
            warnings.Add("Could not reliably detect common footer. Each page keeps its own footer.");
            footer = "";
        }

        return new CommonBlocks(
            string.IsNullOrWhiteSpace(header) ? "" : header.Trim(),
            string.IsNullOrWhiteSpace(nav) ? "" : nav.Trim(),
            string.IsNullOrWhiteSpace(footer) ? "" : footer.Trim());
    }

    internal static string FindLongestCommonPrefixLines(List<string> strings)
    {
        if (strings.Count == 0) return "";

        var lines = strings[0].Split('\n');
        var commonEnd = lines.Length;

        foreach (var s in strings.Skip(1))
        {
            var otherLines = s.Split('\n');
            var match = 0;
            var len = Math.Min(lines.Length, otherLines.Length);
            for (var i = 0; i < len; i++)
            {
                if (string.Equals(lines[i].Trim(), otherLines[i].Trim(), StringComparison.Ordinal))
                    match++;
                else
                    break;
            }

            commonEnd = Math.Min(commonEnd, match);
        }

        return commonEnd > 0 ? string.Join('\n', lines[..commonEnd]) : "";
    }

    internal static string FindLongestCommonSuffixLines(List<string> strings)
    {
        if (strings.Count == 0) return "";

        var lines = strings[0].Split('\n');
        var commonStart = 0;

        foreach (var s in strings.Skip(1))
        {
            var otherLines = s.Split('\n');
            var match = 0;
            var len = Math.Min(lines.Length, otherLines.Length);
            for (var i = 1; i <= len; i++)
            {
                if (string.Equals(lines[^i].Trim(), otherLines[^i].Trim(), StringComparison.Ordinal))
                    match++;
                else
                    break;
            }

            if (match == 0) return "";
            commonStart = commonStart == 0 ? match : Math.Min(commonStart, match);
        }

        return commonStart > 0 ? string.Join('\n', lines[^commonStart..]) : "";
    }

    internal static int FindClosingTagInString(string html, int openIdx)
    {
        var tagName = FidelityPage_GetTagName(html, openIdx);
        if (tagName is null) return -1;

        var closeTag = $"</{tagName}>";
        var idx = html.IndexOf(closeTag, openIdx, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return -1;

        return idx + closeTag.Length - 1;
    }

    internal static string? FidelityPage_GetTagName(string html, int tagStart)
    {
        var end = html.IndexOf('>', tagStart);
        if (end < 0) return null;

        var tag = html[tagStart..end].TrimStart('<').Trim();
        var spaceIdx = tag.IndexOf(' ');
        return spaceIdx > 0 ? tag[..spaceIdx] : tag;
    }

    internal static string NormalizeBlock(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";

        var trimmed = raw.Trim();
        var lines = trimmed.Split('\n');
        if (lines.Length <= 1) return trimmed;

        var firstIndent = CountIndent(lines[0]);
        var minIndent = lines.Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(CountIndent)
            .DefaultIfEmpty(firstIndent)
            .Min();

        var normalized = lines.Select(l =>
            l.Length > minIndent ? l[minIndent..] : l
        );
        return string.Join('\n', normalized).Trim();
    }

    internal static int CountIndent(string line)
        => line.TakeWhile(c => c is ' ' or '\t').Count();
}
