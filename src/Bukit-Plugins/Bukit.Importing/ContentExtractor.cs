using System.Text.RegularExpressions;
using Bukit.Shared;

namespace Bukit.Importing;

internal static partial class ContentExtractor
{
    private static readonly Regex H1Regex = H1Pattern();
    private static readonly Regex FirstParagraphRegex = FirstParagraphPattern();
    private static readonly Regex CardRegex = CardPattern();
    private static readonly Regex FaqItemRegex = FaqItemPattern();
    private static readonly Regex SectionHeadingRegex = SectionHeadingPattern();
    private static readonly Regex SectionLinkRegex = SectionLinkPattern();
    private static readonly Regex CardHeadingRegex = CardHeadingPattern();
    private static readonly Regex CardParagraphRegex = CardParagraphPattern();
    private static readonly Regex CardLinkRegex = CardLinkPattern();
    private static readonly Regex FaqHeadingRegex = FaqHeadingPattern();
    private static readonly Regex SectionOuterRegex = SectionOuterPattern();
    private static readonly Regex SectionClassRegex = SectionClassPattern();

    internal static ExtractedContent Extract(List<DiscoveredPage> pages, RouteMapConfig? routeMap = null)
    {
        var content = new ExtractedContent();
        ExtractNavigation(pages, content, routeMap);

        foreach (var page in pages)
        {
            ExtractPageContent(page, content);
            ExtractCollectionItems(page, content);
            ExtractSections(page, content);
            ExtractFaqs(page, content);
        }

        // The list page yields a summary-only card; the detail page has the body.
        // Keep the richer record instead of whichever page happened to sort first.
        content.Posts = PreferDetail(content.Posts, p => p.Title, p => p.Content).ToList();
        content.Companies = PreferDetail(content.Companies, c => c.Title, c => c.Content).ToList();
        content.Services = PreferDetail(content.Services, s => s.Title, s => s.Content).ToList();

        content.Faqs = content.Faqs
            .GroupBy(f => f.Question)
            .Select(g => g.First())
            .ToList();

        return content;
    }

    private static IEnumerable<T> PreferDetail<T>(IEnumerable<T> records, Func<T, string> title, Func<T, string?> content)
        => records
            .GroupBy(title, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(record => !string.IsNullOrWhiteSpace(content(record)))
                .First());

    private static void ExtractNavigation(List<DiscoveredPage> pages, ExtractedContent content, RouteMapConfig? routeMap)
    {
        var candidate = pages
            .Select(page => NavigationMarkupExtractor.ExtractBest(page.BodyContent))
            .Where(candidate => candidate is not null)
            .OrderByDescending(candidate => candidate!.Score)
            .FirstOrDefault();

        if (candidate is null)
            return;

        var order = 10;
        foreach (var link in candidate.Links)
        {
            content.Navigation.Add(new NavigationRecord
            {
                Title = link.Title,
                Slug = link.Slug,
                Link = ResolveNavigationHref(link.Href, pages, routeMap),
                Order = order
            });
            order += 10;
        }
    }

    internal static string RewriteNavigationLinks(string html, List<DiscoveredPage> pages, RouteMapConfig? routeMap)
        => NavigationHrefPattern().Replace(html, match =>
            match.Groups[1].Value +
            ResolveNavigationHref(match.Groups[2].Value, pages, routeMap) +
            match.Groups[3].Value);

    private static string ResolveNavigationHref(string href, List<DiscoveredPage> pages, RouteMapConfig? routeMap)
    {
        if (string.IsNullOrWhiteSpace(href) ||
            href.StartsWith('#') ||
            href.StartsWith("//", StringComparison.Ordinal) ||
            href.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
            href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        var path = href.Split('#')[0].Split('?')[0];
        if (path.Length == 0)
            return href;

        var normalized = path.TrimStart('/');
        var fileName = Path.GetFileName(normalized);
        var slug = normalized.Trim('/');
        var page = pages.FirstOrDefault(candidate =>
            string.Equals(candidate.RelativePath.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(candidate.RelativePath), fileName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.Slug, slug, StringComparison.OrdinalIgnoreCase));
        if (page is null)
            return href;
        if (page.Type == PageType.Home)
            return "/";

        var mapped = PageClassifier.GetRoute(routeMap, Path.GetFileNameWithoutExtension(page.RelativePath));
        if (!string.IsNullOrWhiteSpace(mapped))
            return mapped;

        if (string.IsNullOrWhiteSpace(page.Slug))
            return href;

        // Mirrors the permalinks SiteConfigGenerator writes for each collection.
        return page.Type switch
        {
            PageType.PostList => "/insights/",
            PageType.CompanyList => "/companies/",
            PageType.ServiceList => "/services/",
            PageType.PostDetail => $"/insights/{page.Slug}/",
            PageType.CompanyDetail => $"/companies/{page.Slug}/",
            PageType.ServiceDetail => $"/services/{page.Slug}/",
            _ => $"/{page.Slug}/"
        };
    }

    private static void ExtractPageContent(DiscoveredPage page, ExtractedContent content)
    {
        if (page.Type is PageType.PostList or PageType.CompanyList or PageType.ServiceList or PageType.Unknown)
            return;

        var h1 = H1Regex.Match(page.UniqueBody);
        var title = h1.Success ? CleanText(h1.Groups[1].Value) : page.Title ?? page.Slug;
        var summary = ExtractSummary(page.UniqueBody);

        content.Pages.Add(new PageRecord
        {
            Title = title,
            Slug = page.Slug,
            Type = page.Type == PageType.Home ? "Home" : "",
            Template = page.Type switch
            {
                PageType.Home => "index",
                PageType.PostDetail => "post",
                PageType.Page => "page",
                _ => "generic"
            },
            Summary = summary,
            Content = ExtractContentBody(page.UniqueBody),
            SeoTitle = title,
            SeoDescription = summary
        });
    }

    private static string? ExtractSummary(string html)
    {
        var match = FirstParagraphRegex.Match(html);
        if (match.Success)
        {
            var text = StripHtml(match.Value);
            return text.Length > 200 ? text[..200] + "..." : text;
        }
        return null;
    }

    private static void ExtractCollectionItems(DiscoveredPage page, ExtractedContent content)
    {
        if (page.Type is PageType.PostList)
        {
            ExtractPosts(page, content);
        }
        else if (page.Type is PageType.PostDetail)
        {
            ExtractPostDetail(page, content);
        }
        else if (page.Type is PageType.CompanyList)
        {
            ExtractCompanies(page, content);
        }
        else if (page.Type is PageType.CompanyDetail)
        {
            ExtractCompanyDetail(page, content);
        }
        else if (page.Type is PageType.ServiceList)
        {
            ExtractServices(page, content);
        }
        else if (page.Type is PageType.ServiceDetail)
        {
            ExtractServiceDetail(page, content);
        }
    }

    private static void ExtractPosts(DiscoveredPage page, ExtractedContent content)
    {
        var counter = 0;
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = CardHeadingRegex.Match(cardHtml);
            var p = CardParagraphRegex.Match(cardHtml);
            var link = CardLinkRegex.Match(cardHtml);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            var slugFromLink = link.Success
                ? Path.GetFileNameWithoutExtension(link.Groups[1].Value.TrimEnd('/'))
                : null;

            var slug = !string.IsNullOrWhiteSpace(slugFromLink)
                ? slugFromLink
                : GetSlugWithFallback(title, "post", ref counter);

            content.Posts.Add(new PostRecord
            {
                Title = title,
                Slug = slug,
                Summary = p.Success ? StripHtml(p.Groups[1].Value).Trim() : null,
                SeoTitle = title
            });
        }

    }

    private static void ExtractPostDetail(DiscoveredPage page, ExtractedContent content)
    {
        var h1 = H1Regex.Match(page.UniqueBody);
        if (h1.Success)
        {
            var title = StripHtml(h1.Groups[1].Value).Trim();
            var counter = 0;
            var slug = string.IsNullOrWhiteSpace(page.Slug)
                ? GetSlugWithFallback(title, "post", ref counter)
                : page.Slug;
            content.Posts.Add(new PostRecord
            {
                Title = title,
                Slug = slug,
                Summary = ExtractSummary(page.UniqueBody),
                Content = ExtractContentBody(page.UniqueBody),
                SeoTitle = title,
                SeoDescription = ExtractSummary(page.UniqueBody)
            });
        }
    }

    private static void ExtractCompanies(DiscoveredPage page, ExtractedContent content)
    {
        var counter = 0;
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = CardHeadingRegex.Match(cardHtml);
            var p = CardParagraphRegex.Match(cardHtml);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            content.Companies.Add(new CompanyRecord
            {
                Title = title,
                Slug = GetSlugWithFallback(title, "company", ref counter),
                Summary = p.Success ? StripHtml(p.Groups[1].Value).Trim() : null,
                SeoTitle = title
            });
        }
    }

    private static void ExtractCompanyDetail(DiscoveredPage page, ExtractedContent content)
    {
        var h1 = H1Regex.Match(page.UniqueBody);
        if (!h1.Success) return;

        var title = StripHtml(h1.Groups[1].Value).Trim();
        var counter = 0;
        var slug = string.IsNullOrWhiteSpace(page.Slug)
            ? GetSlugWithFallback(title, "company", ref counter)
            : page.Slug;
        content.Companies.Add(new CompanyRecord
        {
            Title = title,
            Slug = slug,
            Summary = ExtractSummary(page.UniqueBody),
            Content = ExtractContentBody(page.UniqueBody),
            SeoTitle = title,
            SeoDescription = ExtractSummary(page.UniqueBody)
        });
    }

    private static void ExtractServices(DiscoveredPage page, ExtractedContent content)
    {
        var counter = 0;
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = CardHeadingRegex.Match(cardHtml);
            var p = CardParagraphRegex.Match(cardHtml);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            content.Services.Add(new ServiceRecord
            {
                Title = title,
                Slug = GetSlugWithFallback(title, "service", ref counter),
                Summary = p.Success ? StripHtml(p.Groups[1].Value).Trim() : null,
                SeoTitle = title
            });
        }
    }

    private static void ExtractServiceDetail(DiscoveredPage page, ExtractedContent content)
    {
        var h1 = H1Regex.Match(page.UniqueBody);
        if (!h1.Success) return;

        var title = StripHtml(h1.Groups[1].Value).Trim();
        var counter = 0;
        var slug = string.IsNullOrWhiteSpace(page.Slug)
            ? GetSlugWithFallback(title, "service", ref counter)
            : page.Slug;
        content.Services.Add(new ServiceRecord
        {
            Title = title,
            Slug = slug,
            Summary = ExtractSummary(page.UniqueBody),
            Content = ExtractContentBody(page.UniqueBody),
            SeoTitle = title,
            SeoDescription = ExtractSummary(page.UniqueBody)
        });
    }

    private static void ExtractSections(DiscoveredPage page, ExtractedContent content)
    {
        if (page.Type != PageType.Home) return;

        var sections = SectionOuterRegex.Matches(page.UniqueBody);

        var sortOrder = 10;
        foreach (Match section in sections)
        {
            var sectionHtml = section.Value;
            var classMatch = SectionClassRegex.Match(sectionHtml);
            var className = classMatch.Success ? classMatch.Groups[1].Value : "unknown";
            var sectionType = DetermineSectionType(className);

            var heading = SectionHeadingRegex.Match(sectionHtml);
            var subheading = FirstParagraphRegex.Match(sectionHtml);
            var link = SectionLinkRegex.Match(sectionHtml);

            content.Sections.Add(new SectionRecord
            {
                PageSlug = page.Slug,
                SectionType = sectionType,
                Heading = heading.Success ? StripHtml(heading.Groups[2].Value).Trim() : null,
                Subheading = subheading.Success ? StripHtml(subheading.Groups[1].Value).Trim() : null,
                ButtonText = link.Success ? StripHtml(Regex.Replace(link.Value, "<[^>]*>", "")).Trim() : null,
                ButtonUrl = link.Success ? link.Groups[1].Value : null,
                SortOrder = sortOrder
            });

            sortOrder += 10;
        }
    }

    private static string DetermineSectionType(string className)
    {
        if (className.Contains("hero", StringComparison.OrdinalIgnoreCase)) return "hero";
        if (className.Contains("cta", StringComparison.OrdinalIgnoreCase)) return "cta";
        if (className.Contains("stats", StringComparison.OrdinalIgnoreCase)) return "stats";
        if (className.Contains("features", StringComparison.OrdinalIgnoreCase)) return "features";
        if (className.Contains("about", StringComparison.OrdinalIgnoreCase)) return "about";
        return "section";
    }

    private static void ExtractFaqs(DiscoveredPage page, ExtractedContent content)
    {
        foreach (Match match in FaqItemRegex.Matches(page.UniqueBody))
        {
            var itemHtml = match.Value;
            var h3 = FaqHeadingRegex.Match(itemHtml);
            var p = CardParagraphRegex.Match(itemHtml);

            var question = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(question)) continue;

            content.Faqs.Add(new FaqRecord
            {
                Question = question,
                Answer = p.Success ? StripHtml(p.Groups[1].Value).Trim() : "",
                PageSlug = page.Slug,
                SortOrder = content.Faqs.Count * 10 + 10
            });
        }
    }

    private static string StripHtml(string html)
    {
        return Regex.Replace(html, "<[^>]*>", "").Trim();
    }

    internal static string ExtractContentBody(string html)
    {
        var body = html.Trim();
        if (string.IsNullOrWhiteSpace(body))
            return "";

        body = StripOuterElement(body, "main");
        body = StripOuterElement(body, "article");
        body = H1Regex.Replace(body, "", 1).Trim();

        return body;
    }

    private static string StripOuterElement(string html, string tagName)
    {
        var match = Regex.Match(
            html,
            $@"^\s*<{tagName}\b[^>]*>(?<inner>.*)</{tagName}>\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? match.Groups["inner"].Value.Trim() : html;
    }

    private static string CleanText(string html)
    {
        return Regex.Replace(StripHtml(html), @"\s+", " ").Trim();
    }

    private static string Slugify(string text)
    {
        return SlugHelper.Slugify(text);
    }

    private static string GetSlugWithFallback(string title, string prefix, ref int counter)
    {
        var slug = SlugHelper.Slugify(title);
        if (!string.IsNullOrWhiteSpace(slug))
            return slug;

        counter++;
        return $"{prefix}-{counter:D3}";
    }

    [GeneratedRegex(@"(<a\b[^>]*?\bhref\s*=\s*[""'])([^""']*)([""'])", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NavigationHrefPattern();

    [GeneratedRegex(@"<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1Pattern();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FirstParagraphPattern();

    [GeneratedRegex(@"<(?:div|article)[^>]*(?:class\s*=\s*""[^""]*(?:card|item|entry)[^""]*"")[^>]*>.*?</(?:div|article)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CardPattern();

    [GeneratedRegex(@"<(?:div|section)[^>]*(?:class\s*=\s*""[^""]*(?:faq|accordion)[^""]*"")[^>]*>.*?</(?:div|section)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FaqItemPattern();

    [GeneratedRegex(@"<h([1-2])[^>]*>(.*?)</h[1-2]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SectionHeadingPattern();

    [GeneratedRegex(@"<a[^>]*href=[""']([^""']*)[""'][^>]*>.*?</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SectionLinkPattern();

    [GeneratedRegex(@"<h3[^>]*>(.*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CardHeadingPattern();

    [GeneratedRegex(@"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CardParagraphPattern();

    [GeneratedRegex(@"<a[^>]*href=[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex CardLinkPattern();

    [GeneratedRegex(@"<h[3-6][^>]*>(.*?)</h[3-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FaqHeadingPattern();

    [GeneratedRegex(@"<(?:section|div)[^>]*class\s*=\s*""[^""]*(?:hero|cta|stats|features|about)[^""]*""[^>]*>(.*?)</(?:section|div)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SectionOuterPattern();

    [GeneratedRegex(@"class\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex SectionClassPattern();
}
