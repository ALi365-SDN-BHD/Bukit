namespace Bukit.Importing;

internal static class PageClassifier
{
    private static readonly Dictionary<string, PageType> FileNameMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["index"] = PageType.Home,
        ["about"] = PageType.Page,
        ["contact"] = PageType.Page,
        ["privacy"] = PageType.Page,
        ["terms"] = PageType.Page,
        ["insights"] = PageType.PostList,
        ["blog"] = PageType.PostList,
        ["news"] = PageType.PostList,
        ["article"] = PageType.PostDetail,
        ["article-detail"] = PageType.PostDetail,
        ["articles"] = PageType.PostList,
        ["post"] = PageType.PostDetail,
        ["posts"] = PageType.PostList,
        ["companies"] = PageType.CompanyList,
        ["company"] = PageType.CompanyDetail,
        ["company-detail"] = PageType.CompanyDetail,
        ["services"] = PageType.ServiceList,
        ["service-detail"] = PageType.ServiceDetail,
        ["service"] = PageType.ServiceDetail,
        // 中文/混合语义文件名映射
        ["china-companies"] = PageType.CompanyList,
        ["malaysia-companies"] = PageType.CompanyList,
        ["join"] = PageType.Page,
    };

    /// <summary>
    /// The name a demo file stands for: "about" for about.html and for about/index.html.
    /// Only the demo's root index.html is "index" (the home page). Accepts a relative
    /// path or a bare name.
    /// </summary>
    internal static string PageName(string relativePathOrName)
    {
        var path = relativePathOrName.Replace('\\', '/').Trim('/');
        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.Equals("index", StringComparison.OrdinalIgnoreCase))
            return name;

        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
        return dir.Length == 0 ? name : dir[(dir.LastIndexOf('/') + 1)..];
    }

    /// <summary>
    /// The folder a demo page belongs to: "news" for news/a.html and for
    /// news/a/index.html, "" for root pages.
    /// </summary>
    internal static string PageFolder(string relativePath)
    {
        var path = relativePath.Replace('\\', '/').Trim('/');
        var dir = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
        if (Path.GetFileNameWithoutExtension(path).Equals("index", StringComparison.OrdinalIgnoreCase))
            dir = Path.GetDirectoryName(dir)?.Replace('\\', '/') ?? "";
        return dir.Length == 0 ? "" : dir[(dir.LastIndexOf('/') + 1)..];
    }

    internal static PageType Classify(string relativePathOrName, string html)
        => Classify(relativePathOrName, html, null);

    internal static PageType Classify(string relativePathOrName, string html, RouteMapConfig? routeMap)
    {
        // An entry without a type only sets the route or template; the usual rules decide the kind.
        var entry = FindEntry(routeMap, relativePathOrName);
        if (entry != null && !string.IsNullOrWhiteSpace(entry.Type))
            return ParsePageType(entry.Type);

        if (FileNameMapping.TryGetValue(PageName(relativePathOrName), out var type))
            return type;

        return ClassifyByContent(html);
    }

    internal static PageType ParsePageType(string typeName)
    {
        if (Enum.TryParse<PageType>(typeName, ignoreCase: true, out var result))
            return result;

        return typeName.ToLowerInvariant() switch
        {
            "home" => PageType.Home,
            "page" => PageType.Page,
            "postlist" => PageType.PostList,
            "postdetail" => PageType.PostDetail,
            "companylist" => PageType.CompanyList,
            "companydetail" => PageType.CompanyDetail,
            "servicelist" => PageType.ServiceList,
            "servicedetail" => PageType.ServiceDetail,
            _ => PageType.Unknown
        };
    }

    internal static string? GetRoute(RouteMapConfig? routeMap, string relativePathOrName)
        => FindEntry(routeMap, relativePathOrName)?.Route;

    internal static string? GetTemplate(RouteMapConfig? routeMap, string relativePathOrName)
        => FindEntry(routeMap, relativePathOrName)?.Template;

    /// <summary>
    /// The route-map entry for a demo file. A source that names a path
    /// ("about/index.html", "en/about.html") matches only that file, so each such entry
    /// belongs to one page. A bare file name ("about.html") matches every page of that
    /// name, including about/index.html; an exact path entry wins over it.
    /// </summary>
    internal static RouteMapPage? FindEntry(RouteMapConfig? routeMap, string relativePathOrName)
    {
        if (routeMap is null)
            return null;

        var path = relativePathOrName.Replace('\\', '/').Trim('/');
        var name = PageName(path);
        return routeMap.Pages.FirstOrDefault(p =>
                   string.Equals(NormalizeSource(p.Source), path, StringComparison.OrdinalIgnoreCase))
               ?? routeMap.Pages.FirstOrDefault(p =>
                   !NormalizeSource(p.Source).Contains('/') &&
                   string.Equals(PageName(NormalizeSource(p.Source)), name, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeSource(string source)
    {
        var normalized = source.Replace('\\', '/').Trim('/');
        while (normalized.StartsWith("./", StringComparison.Ordinal))
            normalized = normalized[2..].TrimStart('/');
        return normalized;
    }

    private static PageType ClassifyByContent(string html)
    {
        var hasArticleCards = CountOccurrences(html, "article-card") >= 2 ||
                               CountOccurrences(html, "post-card") >= 2;
        if (hasArticleCards)
            return PageType.PostList;

        var hasCompanyCards = CountOccurrences(html, "company-card") >= 2;
        if (hasCompanyCards)
            return PageType.CompanyList;

        var hasServiceCards = CountOccurrences(html, "service-card") >= 2;
        if (hasServiceCards)
            return PageType.ServiceList;

        return PageType.Unknown;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
