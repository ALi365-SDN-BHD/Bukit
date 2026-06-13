using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands;

internal sealed partial record FidelityPage
{
    public string FilePath { get; }
    public string RelativePath { get; }
    public string Slug { get; }
    public string FullHtml { get; }
    public string HeadContent { get; }
    public string BodyContent { get; }
    public string BodyOpening { get; }
    public string BodyClosing { get; }
    public string UniqueBody { get; }
    public string Title { get; }
    public List<string> Assets { get; }

    public FidelityPage(string filePath, string baseDir)
    {
        FilePath = filePath;
        RelativePath = Path.GetRelativePath(baseDir, filePath);
        Slug = Path.GetFileNameWithoutExtension(filePath);
        FullHtml = File.ReadAllText(filePath);

        HeadContent = ExtractBetween(FullHtml, "<head", "</head>", false) ?? "";
        BodyContent = ExtractBetween(FullHtml, "<body", "</body>", false) ?? "";
        BodyContent = StripBodyTags(BodyContent);
        Title = ExtractBetween(FullHtml, "<title>", "</title>", true) ?? Slug;

        var bodyLines = SplitBodyIntoTopAndBottom(BodyContent, out var bodyOpening, out var bodyClosing, out var uniqueBody);
        BodyOpening = bodyOpening;
        BodyClosing = bodyClosing;
        UniqueBody = uniqueBody;

        Assets = ExtractAssetPaths(FullHtml);
    }

    private static string? ExtractBetween(string html, string startMarker, string endMarker, bool trimTags)
    {
        var startIdx = html.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return null;

        var contentStart = trimTags
            ? startIdx + startMarker.Length
            : startIdx;
        var endIdx = html.IndexOf(endMarker, contentStart, StringComparison.OrdinalIgnoreCase);
        if (endIdx < 0) return null;

        var result = html[contentStart..endIdx];
        if (!trimTags)
        {
            result = html[contentStart..(endIdx + endMarker.Length)];
        }

        return result;
    }

    private static string StripBodyTags(string bodyContent)
    {
        if (string.IsNullOrWhiteSpace(bodyContent))
            return bodyContent;

        var result = bodyContent.Trim();

        var openEnd = result.IndexOf('>');
        if (openEnd > 0 && result.StartsWith("<body", StringComparison.OrdinalIgnoreCase))
        {
            result = result[(openEnd + 1)..];
        }

        if (result.EndsWith("</body>", StringComparison.OrdinalIgnoreCase))
        {
            result = result[..^7];
        }

        return result.Trim();
    }

    private static List<string> SplitBodyIntoTopAndBottom(
        string body, out string opening, out string closing, out string unique)
    {
        var trimmed = body.TrimStart();
        var indent = body.Length - trimmed.Length;
        var indentStr = body[..indent];

        var mainTagIdx = FindMainTagIndex(trimmed);

        if (mainTagIdx < 0)
        {
            var lines = trimmed.Split('\n');
            var mid = Math.Max(1, lines.Length / 3);
            if (mid >= lines.Length)
            {
                opening = "";
                closing = "";
                unique = trimmed;
                return [];
            }

            var sliceEnd = lines.Length - mid;
            opening = string.Join('\n', lines[..mid]);
            closing = string.Join('\n', sliceEnd > 0 ? lines[sliceEnd..] : Array.Empty<string>());
            unique = mid < sliceEnd
                ? string.Join('\n', lines[mid..sliceEnd])
                : "";
            return [];
        }

        var mainEndIdx = FindClosingTag(trimmed, mainTagIdx);
        if (mainEndIdx < 0)
        {
            opening = "";
            closing = "";
            unique = body;
            return [];
        }

        opening = indentStr + trimmed[..mainTagIdx];
        closing = trimmed[(mainEndIdx + 1)..];
        unique = trimmed[mainTagIdx..(mainEndIdx + 1)];
        return [];
    }

    private static int FindMainTagIndex(string html)
    {
        foreach (var candidate in new[] { "<main", "<article", "<!-- content -->", "<!-- main -->", "<!-- body -->" })
        {
            var idx = html.IndexOf(candidate, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) return idx;
        }

        return -1;
    }

    private static int FindClosingTag(string html, int openIdx)
    {
        var tagName = GetTagName(html, openIdx);
        if (tagName is null) return -1;

        var closeTag = $"</{tagName}>";
        var idx = html.IndexOf(closeTag, openIdx, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return -1;

        return idx + closeTag.Length - 1;
    }

    private static string? GetTagName(string html, int tagStart)
    {
        var end = html.IndexOf('>', tagStart);
        if (end < 0) return null;

        var tag = html[tagStart..end].TrimStart('<').Trim();
        var spaceIdx = tag.IndexOf(' ');
        return spaceIdx > 0 ? tag[..spaceIdx] : tag;
    }

    private static List<string> ExtractAssetPaths(string html)
    {
        var paths = new List<string>();
        foreach (Match m in AssetRegex().Matches(html))
        {
            var url = m.Groups[1].Value;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(url);
            }
        }

        return paths;
    }

    [GeneratedRegex(@"(?:src|href)=\""([^\""]+)\""", RegexOptions.IgnoreCase)]
    private static partial Regex AssetRegex();
}
