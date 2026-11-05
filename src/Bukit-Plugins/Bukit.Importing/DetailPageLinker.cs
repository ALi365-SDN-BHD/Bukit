using System.Text.RegularExpressions;

namespace Bukit.Importing;

/// <summary>
/// Recognises detail pages the file-name rules miss, such as news/expo-2026.html:
/// a page a list page's cards link to, or a page in a news/blog style folder, is a
/// detail page of that list's kind. Pages typed by the route map are left alone.
/// </summary>
internal static partial class DetailPageLinker
{
    private static readonly Dictionary<string, PageType> FolderTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["news"] = PageType.PostDetail,
        ["blog"] = PageType.PostDetail,
        ["article"] = PageType.PostDetail,
        ["articles"] = PageType.PostDetail,
        ["post"] = PageType.PostDetail,
        ["posts"] = PageType.PostDetail,
        ["insights"] = PageType.PostDetail,
        ["company"] = PageType.CompanyDetail,
        ["companies"] = PageType.CompanyDetail,
        ["service"] = PageType.ServiceDetail,
        ["services"] = PageType.ServiceDetail
    };

    internal static List<DiscoveredPage> Apply(List<DiscoveredPage> pages, RouteMapConfig? routeMap)
    {
        var byPath = new Dictionary<string, DiscoveredPage>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
            byPath.TryAdd(DemoLinks.Normalize(page.RelativePath), page);

        bool CanRetype(DiscoveredPage page)
            => page.Type == PageType.Unknown &&
               string.IsNullOrWhiteSpace(
                   PageClassifier.FindEntry(routeMap, Path.GetFileNameWithoutExtension(page.RelativePath))?.Type);

        var retyped = new Dictionary<string, PageType>(StringComparer.OrdinalIgnoreCase);

        foreach (var list in pages)
        {
            var detailType = list.Type switch
            {
                PageType.PostList => PageType.PostDetail,
                PageType.CompanyList => PageType.CompanyDetail,
                PageType.ServiceList => PageType.ServiceDetail,
                _ => (PageType?)null
            };
            if (detailType is null)
                continue;

            foreach (Match card in CardPattern().Matches(list.UniqueBody))
            {
                foreach (Match link in HrefPattern().Matches(card.Value))
                {
                    var target = DemoLinks.ResolvePath(list.RelativePath, link.Groups["href"].Value);
                    if (target is not null &&
                        byPath.TryGetValue(target, out var detail) &&
                        CanRetype(detail))
                    {
                        retyped.TryAdd(target, detailType.Value);
                    }
                }
            }
        }

        foreach (var page in pages)
        {
            var key = DemoLinks.Normalize(page.RelativePath);
            if (retyped.ContainsKey(key) || !CanRetype(page))
                continue;

            var folder = Path.GetFileName(Path.GetDirectoryName(key) ?? "");
            if (FolderTypes.TryGetValue(folder, out var type))
                retyped[key] = type;
        }

        return retyped.Count == 0
            ? pages
            : pages.Select(page => retyped.TryGetValue(DemoLinks.Normalize(page.RelativePath), out var type)
                ? page with { Type = type }
                : page).ToList();
    }

    // Same card shape ContentExtractor reads list items from.
    [GeneratedRegex(@"<(?:div|article|li)[^>]*(?:class\s*=\s*""[^""]*(?:card|item|entry)[^""]*"")[^>]*>.*?</(?:div|article|li)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CardPattern();

    [GeneratedRegex(@"\bhref\s*=\s*[""'](?<href>[^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefPattern();
}
