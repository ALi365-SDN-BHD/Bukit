using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class HtmlDocumentParser
{
    private static readonly Regex TitleRegex = TitlePattern();
    private static readonly Regex AssetSrcRegex = AssetSrcPattern();
    private static readonly Regex AssetHrefRegex = AssetHrefPattern();

    internal static DiscoveredPage Parse(string filePath, string baseDir)
    {
        var html = File.ReadAllText(filePath);
        var relativePath = Path.GetRelativePath(baseDir, filePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var slug = fileNameWithoutExtension.Equals("index", StringComparison.OrdinalIgnoreCase)
            ? ""
            : SanitizeSlug(fileNameWithoutExtension);

        var title = ExtractTitle(html);
        var headContent = ExtractBetween(html, "<head>", "</head>");
        var bodyContent = ExtractBodyContent(html);
        var pageType = PageClassifier.Classify(fileNameWithoutExtension, html);

        var (bodyOpening, uniqueBody, bodyClosing) = SplitBody(html);
        var assetPaths = ExtractAssetPaths(html);

        return new DiscoveredPage
        {
            FilePath = filePath,
            RelativePath = relativePath,
            Slug = slug,
            Type = pageType,
            Title = title,
            FullHtml = html,
            HeadContent = headContent,
            BodyContent = bodyContent,
            BodyOpening = bodyOpening,
            UniqueBody = uniqueBody,
            BodyClosing = bodyClosing,
            AssetPaths = assetPaths
        };
    }

    private static string? ExtractTitle(string html)
    {
        var title = ExtractBetween(html, "<title>", "</title>");
        return string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    private static string? ExtractBetween(string text, string startMarker, string endMarker)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return null;

        startIndex += startMarker.Length;

        var endIndex = text.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);
        if (endIndex < 0) return null;

        return text[startIndex..endIndex];
    }

    private static string ExtractBodyContent(string html)
    {
        var bodyStart = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyStart < 0) return html;

        var tagClose = html.IndexOf('>', bodyStart);
        if (tagClose < 0) return html[(bodyStart + 5)..];

        var bodyEnd = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (bodyEnd < 0) return html[(tagClose + 1)..];

        return html[(tagClose + 1)..bodyEnd];
    }

    private static (string opening, string unique, string closing) SplitBody(string html)
    {
        var bodyContent = ExtractBodyContent(html);
        if (string.IsNullOrWhiteSpace(bodyContent))
            return ("", bodyContent, "");

        var mainStart = bodyContent.IndexOf("<main", StringComparison.OrdinalIgnoreCase);
        if (mainStart < 0)
        {
            mainStart = bodyContent.IndexOf("<article", StringComparison.OrdinalIgnoreCase);
        }

        if (mainStart < 0)
        {
            var contentComment = bodyContent.IndexOf("<!-- content -->", StringComparison.OrdinalIgnoreCase);
            if (contentComment >= 0)
            {
                return (bodyContent[..contentComment], bodyContent[contentComment..], "");
            }

            return ("", bodyContent, "");
        }

        var opening = bodyContent[..mainStart];

        var tagName = GetTagName(bodyContent, mainStart);
        var uniqueEnd = FindClosingTag(bodyContent, mainStart, tagName);

        string unique;
        string closing;
        if (uniqueEnd >= 0)
        {
            unique = bodyContent[mainStart..(uniqueEnd + tagName.Length + 3)];
            closing = bodyContent[(uniqueEnd + tagName.Length + 3)..];
        }
        else
        {
            unique = bodyContent[mainStart..];
            closing = "";
        }

        return (opening, unique, closing);
    }

    private static string GetTagName(string html, int tagStart)
    {
        var space = html.IndexOf(' ', tagStart);
        var close = html.IndexOf('>', tagStart);
        var end = (space > 0 && space < close) ? space : close;
        if (end < 0) return "";
        return html[(tagStart + 1)..end].TrimStart('/').ToLowerInvariant();
    }

    private static int FindClosingTag(string html, int openEnd, string tagName)
    {
        var closeTag = $"</{tagName}>";
        var depth = 1;
        var pos = openEnd;

        while (pos < html.Length && depth > 0)
        {
            var nextOpen = html.IndexOf($"<{tagName}", pos + 1, StringComparison.OrdinalIgnoreCase);
            var nextClose = html.IndexOf(closeTag, pos + 1, StringComparison.OrdinalIgnoreCase);

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

    private static List<string> ExtractAssetPaths(string html)
    {
        var paths = new List<string>();
        paths.AddRange(AssetSrcRegex.Matches(html).Select(m => m.Groups[1].Value));
        paths.AddRange(AssetHrefRegex.Matches(html).Select(m => m.Groups[1].Value));
        return paths
            .Where(p => !p.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                         !p.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                         !p.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string SanitizeSlug(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var result = new string(chars);
        while (result.Contains("--"))
            result = result.Replace("--", "-");
        return result.Trim('-').ToLowerInvariant();
    }

    [GeneratedRegex(@"<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitlePattern();

    [GeneratedRegex(@"src=[""']([^""']*?)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex AssetSrcPattern();

    [GeneratedRegex(@"href=[""']([^""']*?)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex AssetHrefPattern();
}
