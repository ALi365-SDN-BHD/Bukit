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

    internal static PageType Classify(string fileNameWithoutExtension, string html)
        => Classify(fileNameWithoutExtension, html, null);

    internal static PageType Classify(string fileNameWithoutExtension, string html, RouteMapConfig? routeMap)
    {
        if (routeMap != null)
        {
            var match = routeMap.Pages.FirstOrDefault(p =>
                string.Equals(p.Source, $"{fileNameWithoutExtension}.html", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(p.Source), fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return ParsePageType(match.Type);
        }

        if (FileNameMapping.TryGetValue(fileNameWithoutExtension, out var type))
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

    internal static string? GetRoute(RouteMapConfig? routeMap, string fileNameWithoutExtension)
    {
        if (routeMap == null) return null;

        var match = routeMap.Pages.FirstOrDefault(p =>
            string.Equals(p.Source, $"{fileNameWithoutExtension}.html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(p.Source), fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
        return match?.Route;
    }

    internal static string? GetTemplate(RouteMapConfig? routeMap, string fileNameWithoutExtension)
    {
        if (routeMap == null) return null;

        var match = routeMap.Pages.FirstOrDefault(p =>
            string.Equals(p.Source, $"{fileNameWithoutExtension}.html", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(p.Source), fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase));
        return match?.Template;
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
