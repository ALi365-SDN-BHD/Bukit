using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Bukit.Importing;

internal static class HtmlDocumentParser
{
    private static readonly System.Text.RegularExpressions.Regex CssUrlRegex = new(
        @"url\(\s*(?<quote>['""]?)(?<value>[^'"")]+)\k<quote>\s*\)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly HtmlParser Parser = new(new HtmlParserOptions
    {
        IsKeepingSourceReferences = false,
        IsNotConsumingCharacterReferences = false
    });

    internal static DiscoveredPage Parse(string filePath, string baseDir, RouteMapConfig? routeMap = null)
    {
        var html = DemoFiles.ReadHtml(filePath, out var guessedEncoding);
        var relativePath = Path.GetRelativePath(baseDir, filePath);
        // about/index.html is the "about" page; only the root index.html is the home page.
        var pageName = PageClassifier.PageName(relativePath);
        var slug = pageName.Equals("index", StringComparison.OrdinalIgnoreCase)
            ? ""
            : SanitizeSlug(pageName);

        var document = Parser.ParseDocument(html);
        var title = document.QuerySelector("title")?.TextContent?.Trim();
        var metaDescription = NonEmpty(document.QuerySelector("meta[name='description' i]")?.GetAttribute("content"));
        var canonicalUrl = NonEmpty(document.QuerySelector("link[rel='canonical' i]")?.GetAttribute("href"))
                           ?? NonEmpty(document.QuerySelector("meta[property='og:url' i]")?.GetAttribute("content"));
        var language = NonEmpty(document.DocumentElement?.GetAttribute("lang"));
        var headContent = document.Head?.InnerHtml?.Trim();
        var bodyContent = document.Body?.InnerHtml?.Trim() ?? html;
        var pageType = PageClassifier.Classify(relativePath, html, routeMap);

        if (routeMap != null)
        {
            var routeSlug = GetSlugFromRouteMap(routeMap, relativePath);
            if (routeSlug != null)
                slug = routeSlug;
        }

        var (bodyOpening, uniqueBody, bodyClosing) = SplitBody(document);
        var assetPaths = ExtractAssetPaths(document, html);

        return new DiscoveredPage
        {
            FilePath = filePath,
            RelativePath = relativePath,
            Slug = slug,
            Type = pageType,
            Title = title,
            MetaDescription = metaDescription,
            CanonicalUrl = canonicalUrl,
            Language = language,
            GuessedEncoding = guessedEncoding,
            FullHtml = html,
            HeadContent = headContent,
            BodyContent = bodyContent,
            BodyOpening = bodyOpening,
            UniqueBody = uniqueBody,
            BodyClosing = bodyClosing,
            AssetPaths = assetPaths
        };
    }

    private static (string opening, string unique, string closing) SplitBody(IDocument document)
    {
        var body = document.Body;
        if (body == null)
            return ("", document.DocumentElement?.OuterHtml ?? "", "");

        var bodyHtml = body.InnerHtml;
        if (string.IsNullOrWhiteSpace(bodyHtml))
            return ("", "", "");

        var mainElement = body.QuerySelector("main") ?? body.QuerySelector("article");
        if (mainElement == null)
        {
            var docElement = body.QuerySelector("*");
            if (docElement != null)
            {
                var outerHtml = docElement.OuterHtml;
                var outerIndex = bodyHtml.IndexOf(outerHtml, StringComparison.Ordinal);
                if (outerIndex > 0)
                    return (bodyHtml[..outerIndex], outerHtml, "");
            }
            return ("", bodyHtml, "");
        }

        var uniqueOuter = mainElement.OuterHtml;
        var index = bodyHtml.IndexOf(uniqueOuter, StringComparison.Ordinal);
        if (index < 0)
            return ("", bodyHtml, "");

        var opening = bodyHtml[..index];
        var closing = bodyHtml[(index + uniqueOuter.Length)..];
        return (opening, uniqueOuter, closing);
    }

    private static List<string> ExtractAssetPaths(IDocument document, string html)
    {
        var paths = new List<string>();

        foreach (var element in document.QuerySelectorAll("[src], [href]"))
        {
            var src = element.GetAttribute("src");
            var href = element.GetAttribute("href");

            if (!string.IsNullOrWhiteSpace(src))
                paths.Add(src);
            if (!string.IsNullOrWhiteSpace(href))
                paths.Add(href);
        }

        // Inline CSS references assets too; they are copied and rewritten per page.
        var inlineCss = document.QuerySelectorAll("style").Select(style => style.TextContent)
            .Concat(document.QuerySelectorAll("[style]").Select(element => element.GetAttribute("style") ?? ""));
        foreach (var css in inlineCss)
        {
            foreach (System.Text.RegularExpressions.Match match in CssUrlRegex.Matches(css))
            {
                var value = match.Groups["value"].Value.Trim();
                if (value.Length > 0 && !value.StartsWith('#'))
                    paths.Add(value);
            }
        }

        return paths
            .Where(p => !p.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                         !p.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                         !p.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NonEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string SanitizeSlug(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var result = new string(chars);
        while (result.Contains("--"))
            result = result.Replace("--", "-");
        return result.Trim('-').ToLowerInvariant();
    }

    private static string? GetSlugFromRouteMap(RouteMapConfig routeMap, string relativePath)
    {
        var match = PageClassifier.FindEntry(routeMap, relativePath);
        if (match == null || string.IsNullOrWhiteSpace(match.Route))
            return null;

        if (!string.IsNullOrWhiteSpace(match.Slug))
            return match.Slug;

        var route = match.Route.Trim('/');
        if (route.Length == 0)
            return "";
        if (route.Contains('{'))
            return null;
        return SanitizeSlug(route.Split('/').Last());
    }
}
