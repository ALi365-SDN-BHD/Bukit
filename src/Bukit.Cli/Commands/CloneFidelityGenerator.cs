using System.Text.RegularExpressions;

namespace Bukit.Cli.Commands;

internal static partial class CloneFidelityGenerator
{
    internal sealed record FidelityResult(
        int TemplateCount,
        int PartialCount,
        int AssetCount,
        int PageCount,
        List<string> Warnings);

    internal static FidelityResult Generate(string rootDir, string htmlDir, string themeName)
    {
        var warnings = new List<string>();
        var htmlFiles = Directory.GetFiles(htmlDir, "*.html", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (htmlFiles.Count == 0)
        {
            throw new InvalidOperationException($"No .html files found in {htmlDir}");
        }

        var pages = htmlFiles.Select(f => new FidelityPage(f, htmlDir)).ToList();

        var themeDir = Path.Combine(rootDir, "themes", themeName);
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "layouts"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "pages"));
        Directory.CreateDirectory(Path.Combine(themeDir, "layouts", "partials"));
        Directory.CreateDirectory(Path.Combine(themeDir, "assets"));
        Directory.CreateDirectory(Path.Combine(themeDir, "static"));

        var commonBlocks = ExtractCommonBlocks(pages, warnings);

        WritePartial(themeDir, "partials/header.html", commonBlocks.Header);
        WritePartial(themeDir, "partials/nav.html", commonBlocks.Nav);
        WritePartial(themeDir, "partials/footer.html", commonBlocks.Footer);

        var baseLayout = BuildLayout(commonBlocks);
        File.WriteAllText(Path.Combine(themeDir, "layouts", "layouts", "base.html"), baseLayout);

        var pageCount = 0;
        foreach (var page in pages)
        {
            var pageTemplate = BuildPageTemplate(page);
            var pageName = Path.GetFileNameWithoutExtension(page.FilePath);
            var safeName = SanitizeTemplateName(pageName);
            if (safeName is "index" or "list")
            {
                safeName = "page-" + safeName;
            }

            var templatePath = Path.Combine(themeDir, "layouts", "pages", $"{safeName}.html");
            File.WriteAllText(templatePath, pageTemplate);
            pageCount++;
        }

        var indexTemplate = BuildIndexTemplate(pages);
        File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "index.html"), indexTemplate);
        File.WriteAllText(Path.Combine(themeDir, "layouts", "pages", "list.html"), BuildListTemplate());

        CopyAssets(rootDir, htmlDir, themeDir, pages, out var assetCount);
        CopyStaticFiles(rootDir, htmlDir, themeDir, pages);

        return new FidelityResult(
            TemplateCount: 3 + pageCount,
            PartialCount: (string.IsNullOrEmpty(commonBlocks.Header) ? 0 : 1) +
                          (string.IsNullOrEmpty(commonBlocks.Nav) ? 0 : 1) +
                          (string.IsNullOrEmpty(commonBlocks.Footer) ? 0 : 1),
            AssetCount: assetCount,
            PageCount: pageCount,
            Warnings: warnings);
    }

    private sealed partial record FidelityPage
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

    private sealed record CommonBlocks(string Header, string Nav, string Footer);

    private static CommonBlocks ExtractCommonBlocks(List<FidelityPage> pages, List<string> warnings)
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

    private static string FindLongestCommonPrefixLines(List<string> strings)
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

    private static string FindLongestCommonSuffixLines(List<string> strings)
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

    private static int FindClosingTagInString(string html, int openIdx)
    {
        var tagName = FidelityPage_GetTagName(html, openIdx);
        if (tagName is null) return -1;

        var closeTag = $"</{tagName}>";
        var idx = html.IndexOf(closeTag, openIdx, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return -1;

        return idx + closeTag.Length - 1;
    }

    private static string? FidelityPage_GetTagName(string html, int tagStart)
    {
        var end = html.IndexOf('>', tagStart);
        if (end < 0) return null;

        var tag = html[tagStart..end].TrimStart('<').Trim();
        var spaceIdx = tag.IndexOf(' ');
        return spaceIdx > 0 ? tag[..spaceIdx] : tag;
    }

    private static string NormalizeBlock(string raw)
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

    private static int CountIndent(string line)
        => line.TakeWhile(c => c is ' ' or '\t').Count();

    private static string BuildLayout(CommonBlocks blocks)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"{{ site.language }}\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"UTF-8\" />");
        sb.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        sb.AppendLine("  <title>{{ page.title }} | {{ site.title }}</title>");
        sb.AppendLine("  {{ if page.seo }}");
        sb.AppendLine("  <link rel=\"canonical\" href=\"{{ page.seo.canonical }}\" />");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        if (!string.IsNullOrWhiteSpace(blocks.Header))
        {
            sb.AppendLine("  {{ include 'partials/header.html' }}");
        }

        if (!string.IsNullOrWhiteSpace(blocks.Nav))
        {
            sb.AppendLine("  {{ include 'partials/nav.html' }}");
        }

        sb.AppendLine("  {{ content }}");

        if (!string.IsNullOrWhiteSpace(blocks.Footer))
        {
            sb.AppendLine("  {{ include 'partials/footer.html' }}");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string BuildPageTemplate(FidelityPage page)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{{% layout \"layouts/base.html\" %}}");
        sb.AppendLine(page.UniqueBody.Trim());
        return sb.ToString();
    }

    private static string BuildIndexTemplate(List<FidelityPage> pages)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine("<main>");
        sb.AppendLine("  <h1>{{ page.title }}</h1>");
        sb.AppendLine("  <ul>");

        foreach (var page in pages)
        {
            sb.AppendLine($"    <li><a href=\"/{page.Slug}/\">{System.Net.WebUtility.HtmlEncode(page.Title)}</a></li>");
        }

        sb.AppendLine("  </ul>");
        sb.AppendLine("</main>");
        return sb.ToString();
    }

    private static string BuildListTemplate()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("{% layout \"layouts/base.html\" %}");
        sb.AppendLine("<main>");
        sb.AppendLine("  <h1>{{ page.title }}</h1>");
        sb.AppendLine("  {{ for p in pages }}");
        sb.AppendLine("  <article>");
        sb.AppendLine("    <h2><a href=\"{{ p.url }}\">{{ p.title }}</a></h2>");
        sb.AppendLine("  </article>");
        sb.AppendLine("  {{ end }}");
        sb.AppendLine("</main>");
        return sb.ToString();
    }

    private static void WritePartial(string themeDir, string relativePath, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var fullPath = Path.Combine(themeDir, "layouts", relativePath);
        File.WriteAllText(fullPath, NormalizeBlock(content));
    }

    private static void CopyAssets(string rootDir, string htmlDir, string themeDir, List<FidelityPage> pages, out int count)
    {
        count = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            foreach (var asset in page.Assets)
            {
                if (!seen.Add(asset)) continue;

                var sourcePath = Path.GetFullPath(Path.Combine(htmlDir, asset.TrimStart('/')));
                var ext = Path.GetExtension(asset).ToLowerInvariant();
                var isImage = ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ico";
                var destSubDir = isImage ? "assets" : "static";
                var destPath = Path.Combine(themeDir, destSubDir, asset.TrimStart('/'));

                if (File.Exists(sourcePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(sourcePath, destPath, overwrite: true);
                    count++;
                }
            }
        }

        var sourceAssetsDir = Path.Combine(htmlDir, "assets");
        if (Directory.Exists(sourceAssetsDir))
        {
            foreach (var file in Directory.GetFiles(sourceAssetsDir, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(sourceAssetsDir, file);
                var dest = Path.Combine(themeDir, "assets", rel);
                if (!File.Exists(dest))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(file, dest);
                    count++;
                }
            }
        }
    }

    private static void CopyStaticFiles(string rootDir, string htmlDir, string themeDir, List<FidelityPage> pages)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            foreach (var asset in page.Assets)
            {
                if (!seen.Add(asset)) continue;

                var ext = Path.GetExtension(asset).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or ".ico")
                    continue;

                var sourcePath = Path.GetFullPath(Path.Combine(htmlDir, asset.TrimStart('/')));
                if (File.Exists(sourcePath))
                {
                    var destPath = Path.Combine(themeDir, "static", asset.TrimStart('/'));
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(sourcePath, destPath, overwrite: true);
                }
            }
        }
    }

    private static string SanitizeTemplateName(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var result = new string(chars);
        while (result.Contains("--"))
            result = result.Replace("--", "-");

        return result.Trim('-').ToLowerInvariant();
    }
}
