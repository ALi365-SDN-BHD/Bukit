using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Bukit.Importing;

internal static class HtmlDocumentParser
{
    private static readonly HtmlParser Parser = new(new HtmlParserOptions
    {
        IsKeepingSourceReferences = false,
        IsNotConsumingCharacterReferences = false
    });

    internal static DiscoveredPage Parse(string filePath, string baseDir, RouteMapConfig? routeMap = null)
    {
        var html = File.ReadAllText(filePath);
        var relativePath = Path.GetRelativePath(baseDir, filePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var slug = fileNameWithoutExtension.Equals("index", StringComparison.OrdinalIgnoreCase)
            ? ""
            : SanitizeSlug(fileNameWithoutExtension);

        var document = Parser.ParseDocument(html);
        var title = document.QuerySelector("title")?.TextContent?.Trim();
        var headContent = document.Head?.InnerHtml?.Trim();
        var bodyContent = document.Body?.InnerHtml?.Trim() ?? html;
        var pageType = PageClassifier.Classify(fileNameWithoutExtension, html, routeMap);

        if (routeMap != null)
        {
            var routeSlug = GetSlugFromRouteMap(routeMap, fileNameWithoutExtension);
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

    private static string? GetSlugFromRouteMap(RouteMapConfig routeMap, string fileNameWithoutExtension)
    {
        var match = routeMap.Pages.FirstOrDefault(p =>
            string.Equals(p.Source, $"{fileNameWithoutExtension}.html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(p.Source), fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
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
