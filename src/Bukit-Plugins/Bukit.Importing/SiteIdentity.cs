using System.Text.RegularExpressions;

namespace Bukit.Importing;

/// <summary>Site name, address and description read from the demo instead of placeholders.</summary>
internal static partial class SiteIdentity
{
    internal const string PlaceholderUrl = "https://example.com";

    private static readonly HashSet<string> GenericHomeTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "首页", "主页", "home", "home page", "homepage", "index", "welcome"
    };

    /// <summary>
    /// The site name: the title segment most pages share ("关于我们 - 华南精密" and
    /// "新闻 - 华南精密" give "华南精密"), else the home page title.
    /// </summary>
    internal static string? Name(IReadOnlyList<DiscoveredPage> pages)
    {
        var titles = pages.Select(p => p.Title?.Trim()).Where(t => !string.IsNullOrEmpty(t)).Select(t => t!).ToList();
        if (titles.Count == 0)
            return null;

        if (titles.Count >= 2)
        {
            // Spaced separators first, so "e-commerce" stays whole when titles use " - ".
            foreach (var separator in new[] { SpacedSeparator(), AnySeparator() })
            {
                var shared = titles
                    .SelectMany(t => separator.Split(t).Select(s => s.Trim()).Where(s => s.Length > 0).Distinct())
                    .GroupBy(s => s, StringComparer.Ordinal)
                    .Select(g => (Segment: g.Key, Count: g.Count()))
                    .Where(g => g.Count >= 2 && g.Count * 2 >= titles.Count)
                    .OrderByDescending(g => g.Count)
                    .ThenByDescending(g => g.Segment.Length)
                    .FirstOrDefault();
                if (shared.Segment is not null)
                    return shared.Segment;
            }
        }

        // "首页" or "Home" names the page, not the site; the theme name is a better guess.
        var home = pages.FirstOrDefault(p => p.Type == PageType.Home)?.Title?.Trim();
        return string.IsNullOrEmpty(home) || GenericHomeTitles.Contains(home) ? null : home;
    }

    /// <summary>
    /// The site's public origin: an absolute --base-url, else the home page's canonical
    /// or og:url address. Null when the demo does not say.
    /// </summary>
    internal static string? Url(IReadOnlyList<DiscoveredPage> pages, string? baseUrl)
    {
        if (Origin(baseUrl) is { } fromOption)
            return fromOption;

        var home = pages.FirstOrDefault(p => p.Type == PageType.Home);
        return Origin(home?.CanonicalUrl)
               ?? pages.Select(p => Origin(p.CanonicalUrl)).FirstOrDefault(url => url is not null);
    }

    internal static string? Description(IReadOnlyList<DiscoveredPage> pages)
        => pages.FirstOrDefault(p => p.Type == PageType.Home)?.MetaDescription;

    private static string? Origin(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
           (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;

    [GeneratedRegex(@"\s+[|\-–—_·]\s+|[｜丨]")]
    private static partial Regex SpacedSeparator();

    // Unspaced "-" splits only next to Chinese text ("关于我们-华南精密"), so hyphenated
    // words ("E-commerce") stay whole.
    [GeneratedRegex(@"[|–—_·｜丨]|(?<=\p{IsCJKUnifiedIdeographs})-|-(?=\p{IsCJKUnifiedIdeographs})")]
    private static partial Regex AnySeparator();
}
