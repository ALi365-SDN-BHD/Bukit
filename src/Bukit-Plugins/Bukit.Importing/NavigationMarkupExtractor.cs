using System.Net;
using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Importing;

internal static partial class NavigationMarkupExtractor
{
    internal enum CandidateKind
    {
        NavTag,
        MenuContainer,
        HeaderLinks
    }

    internal sealed record NavigationLink(string Title, string Slug, string Href);

    internal sealed record NavigationCandidate(
        string Markup,
        string OpeningTag,
        string InnerHtml,
        string ClosingTag,
        string TagName,
        CandidateKind Kind,
        int Score,
        IReadOnlyList<NavigationLink> Links);

    private static readonly string[] NavigationTokens =
    [
        "menu",
        "nav",
        "navbar",
        "navigation",
        "main-menu",
        "site-menu",
        "header-menu",
        "nav-menu",
        "navbar-nav"
    ];

    internal static NavigationCandidate? ExtractBest(string html, bool includeHeaderFallback = true)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;
        html = StripScriptAndStyleContent(html);

        var candidates = new List<NavigationCandidate>();
        candidates.AddRange(FindTagCandidates(html, NavBlockRegex(), CandidateKind.NavTag));
        candidates.AddRange(FindTagCandidates(html, MenuContainerRegex(), CandidateKind.MenuContainer));

        if (includeHeaderFallback)
        {
            foreach (Match header in HeaderBlockRegex().Matches(html))
            {
                var links = ExtractLinks(header.Value).ToList();
                if (links.Count < 2)
                    continue;

                candidates.Add(new NavigationCandidate(
                    Markup: header.Value,
                    OpeningTag: "",
                    InnerHtml: header.Value,
                    ClosingTag: "",
                    TagName: "header",
                    Kind: CandidateKind.HeaderLinks,
                    Score: Score(CandidateKind.HeaderLinks, header.Value, links, inHeader: true),
                    Links: links));
            }
        }

        return candidates
            .Where(c => c.Links.Count > 0)
            .OrderByDescending(c => c.Score)
            .ThenByDescending(c => c.Links.Count)
            .FirstOrDefault(c => c.Score >= 60);
    }

    internal static IEnumerable<NavigationLink> ExtractLinks(string html)
    {
        html = StripScriptAndStyleContent(html);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match anchor in AnchorRegex().Matches(html))
        {
            var href = WebUtility.HtmlDecode(anchor.Groups[1].Value).Trim();
            var title = CleanText(anchor.Groups[2].Value);
            if (!LooksLikeNavigationLink(title, href))
                continue;

            var key = $"{title}\n{href}";
            if (!seen.Add(key))
                continue;

            yield return new NavigationLink(title, SlugHelper.Slugify(title), href);
        }
    }

    private static IEnumerable<NavigationCandidate> FindTagCandidates(
        string html,
        Regex regex,
        CandidateKind kind)
    {
        foreach (Match match in regex.Matches(html))
        {
            var markup = match.Value;
            var openEnd = markup.IndexOf('>');
            var closeStart = markup.LastIndexOf("</", StringComparison.OrdinalIgnoreCase);
            if (openEnd < 0 || closeStart <= openEnd)
                continue;

            var tagName = match.Groups["tag"].Value;
            var openingTag = markup[..(openEnd + 1)];
            var innerHtml = markup[(openEnd + 1)..closeStart];
            var closingTag = markup[closeStart..];
            var links = ExtractLinks(markup).ToList();
            if (links.Count == 0)
                continue;

            yield return new NavigationCandidate(
                Markup: markup,
                OpeningTag: openingTag,
                InnerHtml: innerHtml,
                ClosingTag: closingTag,
                TagName: tagName,
                Kind: kind,
                Score: Score(kind, markup, links, inHeader: IsInsideHeader(html, match.Index)),
                Links: links);
        }
    }

    private static int Score(CandidateKind kind, string markup, IReadOnlyList<NavigationLink> links, bool inHeader)
    {
        var score = kind switch
        {
            CandidateKind.NavTag => 100,
            CandidateKind.MenuContainer => 70,
            CandidateKind.HeaderLinks => 45,
            _ => 0
        };

        if (inHeader) score += 25;
        if (HasNavigationToken(markup)) score += 20;
        if (links.Count is >= 2 and <= 12) score += 20;
        score += Math.Min(links.Count, 8);
        if (links.Count(l => IsInternalHref(l.Href)) >= Math.Max(1, links.Count - 1))
            score += 15;

        return score;
    }

    private static bool IsInsideHeader(string html, int index)
    {
        var before = html[..index];
        var open = before.LastIndexOf("<header", StringComparison.OrdinalIgnoreCase);
        if (open < 0)
            return false;
        var close = before.LastIndexOf("</header>", StringComparison.OrdinalIgnoreCase);
        return close < open;
    }

    private static bool HasNavigationToken(string markup)
    {
        var lower = markup.ToLowerInvariant();
        return NavigationTokens.Any(lower.Contains);
    }

    private static bool LooksLikeNavigationLink(string title, string href)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 40)
            return false;
        if (string.IsNullOrWhiteSpace(href) || href == "#")
            return false;
        if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            return false;
        if (IsSocialHref(href))
            return false;
        return IsInternalHref(href);
    }

    private static bool IsInternalHref(string href)
    {
        if (href.StartsWith("/", StringComparison.Ordinal))
            return true;
        if (href.StartsWith("#", StringComparison.Ordinal))
            return href.Length > 1;
        return !href.Contains("://", StringComparison.Ordinal);
    }

    private static bool IsSocialHref(string href)
    {
        var lower = href.ToLowerInvariant();
        return lower.Contains("facebook.com", StringComparison.Ordinal) ||
               lower.Contains("twitter.com", StringComparison.Ordinal) ||
               lower.Contains("x.com", StringComparison.Ordinal) ||
               lower.Contains("instagram.com", StringComparison.Ordinal) ||
               lower.Contains("linkedin.com", StringComparison.Ordinal) ||
               lower.Contains("youtube.com", StringComparison.Ordinal);
    }

    private static string CleanText(string html)
    {
        var text = Regex.Replace(html, "<[^>]*>", "");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string StripScriptAndStyleContent(string html)
        => ScriptOrStyleBlockRegex().Replace(html, "");

    [GeneratedRegex(@"<(?<tag>nav)\b[^>]*>.*?</nav>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NavBlockRegex();

    [GeneratedRegex(@"<(?<tag>div|ul|ol)\b(?=[^>]*(?:class|id)\s*=\s*[""'][^""']*(?:menu|nav|navbar|navigation|main-menu|site-menu|header-menu|nav-menu|navbar-nav)[^""']*[""'])[^>]*>.*?</\k<tag>>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex MenuContainerRegex();

    [GeneratedRegex(@"<header\b[^>]*>.*?</header>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HeaderBlockRegex();

    [GeneratedRegex(@"<a[^>]*href=[""']([^""']*)[""'][^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AnchorRegex();

    [GeneratedRegex(@"<(?:script|style)\b[^>]*>.*?</(?:script|style)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyleBlockRegex();
}
