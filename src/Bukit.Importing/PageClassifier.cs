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
    };

    internal static PageType Classify(string fileNameWithoutExtension, string html)
    {
        if (FileNameMapping.TryGetValue(fileNameWithoutExtension, out var type))
            return type;

        return ClassifyByContent(html);
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
