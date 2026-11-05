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

        var (bodyOpening, uniqueBody, bodyClosing) = SplitBody(document, pageType);
        var assetPaths = ExtractAssetPaths(document);

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

    private static (string opening, string unique, string closing) SplitBody(IDocument document, PageType pageType)
    {
        var body = document.Body;
        if (body == null)
            return ("", document.DocumentElement?.OuterHtml ?? "", "");

        var bodyHtml = body.InnerHtml;
        if (string.IsNullOrWhiteSpace(bodyHtml))
            return ("", "", "");

        // <main> is the page body. A single <article> is a detail page's body. Several
        // <article> elements are list cards, so the body must keep all of them.
        var mainElement = body.QuerySelector("main");
        if (mainElement != null)
            return SplitAround(bodyHtml, mainElement.OuterHtml);

        var articles = body.QuerySelectorAll("article");
        if (articles.Length == 1 &&
            (pageType is PageType.PostDetail or PageType.CompanyDetail or PageType.ServiceDetail ||
             articles[0].QuerySelector("h1") is not null))
        {
            return SplitAround(bodyHtml, articles[0].OuterHtml);
        }

        return SplitHeaderFooter(bodyHtml, body);
    }

    private static (string opening, string unique, string closing) SplitAround(string bodyHtml, string uniqueOuter)
    {
        var index = bodyHtml.IndexOf(uniqueOuter, StringComparison.Ordinal);
        if (index < 0)
            return ("", bodyHtml, "");

        return (bodyHtml[..index], uniqueOuter, bodyHtml[(index + uniqueOuter.Length)..]);
    }

    // With no <main>, the page still needs its header and footer removed from the
    // unique body so they can become shared partials instead of page content.
    private static (string opening, string unique, string closing) SplitHeaderFooter(string bodyHtml, IElement body)
    {
        var opening = "";
        var unique = bodyHtml;
        var closing = "";

        var header = FindHeader(body);
        if (header is not null)
        {
            var end = IndexAfter(unique, header.OuterHtml);
            if (end > 0)
            {
                opening = unique[..end];
                unique = unique[end..];
            }
        }

        var footer = FindFooter(body);
        if (footer is not null)
        {
            var index = unique.LastIndexOf(footer.OuterHtml, StringComparison.Ordinal);
            if (index >= 0)
            {
                closing = unique[index..];
                unique = unique[..index];
            }
        }

        return (opening, unique, closing);
    }

    // A page may put its header/footer directly in body or inside one wrapper.
    private static IElement? FindHeader(IElement body)
    {
        var direct = body.Children.FirstOrDefault(child => child.LocalName == "header");
        if (direct is not null || body.Children.Length != 1)
            return direct;

        return body.Children[0].Children.FirstOrDefault(child => child.LocalName == "header");
    }

    private static IElement? FindFooter(IElement body)
    {
        var direct = body.Children.LastOrDefault(child => child.LocalName == "footer");
        if (direct is not null || body.Children.Length != 1)
            return direct;

        return body.Children[0].Children.LastOrDefault(child => child.LocalName == "footer");
    }

    private static int IndexAfter(string html, string value)
    {
        var index = html.IndexOf(value, StringComparison.Ordinal);
        return index < 0 ? -1 : index + value.Length;
    }

    private static List<string> ExtractAssetPaths(IDocument document)
    {
        var paths = new List<string>();

        foreach (var element in document.QuerySelectorAll(
                     "[src], [href], [data-src], [srcset], [data-srcset], [poster], [data-poster]"))
        {
            foreach (var attribute in new[] { "src", "href", "data-src", "poster", "data-poster" })
            {
                var value = element.GetAttribute(attribute);
                if (!string.IsNullOrWhiteSpace(value))
                    paths.Add(value);
            }

            foreach (var attribute in new[] { "srcset", "data-srcset" })
            {
                var value = element.GetAttribute(attribute);
                if (!string.IsNullOrWhiteSpace(value))
                    paths.AddRange(SrcsetUrls(value));
            }
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

    private static IEnumerable<string> SrcsetUrls(string srcset)
    {
        foreach (var candidate in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = candidate.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                yield return parts[0];
        }
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
