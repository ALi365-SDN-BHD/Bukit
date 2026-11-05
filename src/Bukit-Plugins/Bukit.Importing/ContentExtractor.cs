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
    private static readonly Regex ImgTagRegex = ImgTagPattern();
    private static readonly Regex ImgSourceRegex = ImgSourcePattern();
    private static readonly Regex FaqHeadingRegex = FaqHeadingPattern();
    private static readonly Regex SectionOuterRegex = SectionOuterPattern();
    private static readonly Regex SectionClassRegex = SectionClassPattern();

    internal static ExtractedContent Extract(
        List<DiscoveredPage> pages,
        RouteMapConfig? routeMap = null,
        PageTemplatePlan? plan = null)
    {
        var content = new ExtractedContent();
        ExtractNavigation(pages, content, routeMap, plan);

        var pagesByPath = new Dictionary<string, DiscoveredPage>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in pages)
            pagesByPath.TryAdd(DemoLinks.Normalize(page.RelativePath), page);

        foreach (var page in pages)
        {
            var marks = (Pages: content.Pages.Count, Posts: content.Posts.Count,
                Companies: content.Companies.Count, Services: content.Services.Count);
            // Links to other demo pages become the site URLs those pages are published at.
            var source = plan is null
                ? page
                : page with { UniqueBody = InternalLinkRewriter.RewriteContentLinks(page.UniqueBody, page.RelativePath, plan) };
            ExtractPageContent(source, content);
            ExtractCollectionItems(source, page, content, pagesByPath);
            if (plan is not null)
            {
                AssignTemplate(content, page, plan.PathFor(page), marks);
                AssignPageRoute(content, page, plan, marks.Pages);
            }
            ExtractSections(source, content);
            ExtractFaqs(source, content);
        }

        // The list page yields a summary-only card; the detail page has the body.
        // Keep the richer record instead of whichever page happened to sort first.
        content.Posts = PreferDetail(content.Posts, p => p.Slug, p => p.Content, p => p.TemplatePath is not null).ToList();
        content.Companies = PreferDetail(content.Companies, c => c.Slug, c => c.Content, c => c.TemplatePath is not null).ToList();
        content.Services = PreferDetail(content.Services, s => s.Slug, s => s.Content, s => s.TemplatePath is not null).ToList();

        content.Faqs = content.Faqs
            .GroupBy(f => f.Question)
            .Select(g => g.First())
            .ToList();

        return content;
    }

    // Records a page produced are rendered by that page's own template. Cards read
    // from a list page are not that page, so they keep the collection fallback.
    private static void AssignTemplate(
        ExtractedContent content,
        DiscoveredPage page,
        string templatePath,
        (int Pages, int Posts, int Companies, int Services) marks)
    {
        for (var i = marks.Pages; i < content.Pages.Count; i++)
            content.Pages[i] = content.Pages[i] with { TemplatePath = templatePath };

        if (page.Type == PageType.PostDetail)
            for (var i = marks.Posts; i < content.Posts.Count; i++)
                content.Posts[i] = content.Posts[i] with { TemplatePath = templatePath };
        if (page.Type == PageType.CompanyDetail)
            for (var i = marks.Companies; i < content.Companies.Count; i++)
                content.Companies[i] = content.Companies[i] with { TemplatePath = templatePath };
        if (page.Type == PageType.ServiceDetail)
            for (var i = marks.Services; i < content.Services.Count; i++)
                content.Services[i] = content.Services[i] with { TemplatePath = templatePath };
    }

    // The plan holds each ordinary page's unique slug and, when the page owns its
    // route-map entry, the route that goes into the draft's own `url:`.
    private static void AssignPageRoute(ExtractedContent content, DiscoveredPage page, PageTemplatePlan plan, int firstPage)
    {
        for (var i = firstPage; i < content.Pages.Count; i++)
        {
            var record = content.Pages[i];
            if (record.Type.Equals("Home", StringComparison.OrdinalIgnoreCase) ||
                !record.Template.Equals("page", StringComparison.OrdinalIgnoreCase))
                continue;

            content.Pages[i] = record with { Slug = plan.SlugFor(page), Url = plan.RouteFor(page) };
        }
    }

    /// <summary>
    /// Collection drafts are written to {collection}/{slug}.md, so slugs must be unique.
    /// Records from their own detail page keep their slug (it matches that page's URL);
    /// list cards that collide are renamed from their title, with a warning.
    /// </summary>
    internal static void EnsureUniqueCollectionSlugs(ExtractedContent content, List<string> warnings)
    {
        content.Posts = Unique(content.Posts, p => p.Slug, p => p.Title, p => p.TemplatePath is not null,
            (p, slug) => p with { Slug = slug }, "post", warnings);
        content.Companies = Unique(content.Companies, c => c.Slug, c => c.Title, c => c.TemplatePath is not null,
            (c, slug) => c with { Slug = slug }, "company", warnings);
        content.Services = Unique(content.Services, s => s.Slug, s => s.Title, s => s.TemplatePath is not null,
            (s, slug) => s with { Slug = slug }, "service", warnings);
    }

    private static List<T> Unique<T>(
        List<T> items,
        Func<T, string> slugOf,
        Func<T, string> titleOf,
        Func<T, bool> ownsPage,
        Func<T, string, T> withSlug,
        string kind,
        List<string> warnings)
    {
        var reserved = new HashSet<string>(items.Where(ownsPage).Select(slugOf), StringComparer.OrdinalIgnoreCase);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<T>(items.Count);

        foreach (var item in items)
        {
            var slug = slugOf(item);
            var owns = ownsPage(item);
            if (slug.Length > 0 && (owns || !reserved.Contains(slug)) && used.Add(slug))
            {
                result.Add(item);
                continue;
            }

            var baseSlug = SlugHelper.Slugify(titleOf(item));
            if (string.IsNullOrWhiteSpace(baseSlug))
                baseSlug = kind;
            var unique = baseSlug;
            for (var n = 2; reserved.Contains(unique) || !used.Add(unique); n++)
                unique = $"{baseSlug}-{n}";

            warnings.Add($"{kind} '{titleOf(item)}' uses slug '{slug}', which another {kind} already has; it was imported as '{unique}'.");
            result.Add(withSlug(item, unique));
        }

        return result;
    }

    // A list card and its detail page share a slug; keeping one record per slug
    // merges them without dropping two different pages that happen to share a title.
    private static IEnumerable<T> PreferDetail<T>(
        IEnumerable<T> records,
        Func<T, string> slug,
        Func<T, string?> content,
        Func<T, bool> ownsPage)
        => records
            .GroupBy(slug, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(ownsPage)
                .ThenByDescending(record => !string.IsNullOrWhiteSpace(content(record)))
                .First());

    private static void ExtractNavigation(
        List<DiscoveredPage> pages, ExtractedContent content, RouteMapConfig? routeMap, PageTemplatePlan? plan)
    {
        var best = pages
            .Select(page => (Page: page, Candidate: NavigationMarkupExtractor.ExtractBest(page.BodyContent)))
            .Where(item => item.Candidate is not null)
            .OrderByDescending(item => item.Candidate!.Score)
            .FirstOrDefault();

        if (best.Candidate is not { } candidate)
            return;

        var order = 10;
        foreach (var link in candidate.Links)
        {
            content.Navigation.Add(new NavigationRecord
            {
                Title = link.Title,
                Slug = link.Slug,
                // Hrefs are relative to the page the menu came from.
                Link = (plan is null ? null : InternalLinkRewriter.PageUrl(link.Href, best.Page.RelativePath, plan))
                       ?? ResolveNavigationHref(link.Href, pages, routeMap),
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

        var mapped = PageClassifier.GetRoute(routeMap, page.RelativePath);
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
        if (page.Type is PageType.PostList or PageType.CompanyList or PageType.ServiceList)
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
                // A page whose kind is not recognised is still a page: it gets a draft
                // and renders with its own template instead of disappearing.
                PageType.Page or PageType.Unknown => "page",
                _ => "generic"
            },
            Summary = summary,
            Content = ExtractContentBody(page.UniqueBody),
            SeoTitle = title,
            // The demo's own description is what search engines show today.
            SeoDescription = page.MetaDescription ?? summary
        });
    }

    // Only a link to a real detail page may supply the card's slug. External links,
    // "#" and links to pages/lists keep the title-derived slug instead of merging.
    private static string? ResolveDetailSlug(
        string href,
        DiscoveredPage sourcePage,
        IReadOnlyDictionary<string, DiscoveredPage> pagesByPath,
        PageType detailType)
    {
        var target = DemoLinks.ResolvePath(sourcePage.RelativePath, href);
        if (target is null)
            return null;

        if (!pagesByPath.TryGetValue(target, out var detail) &&
            DemoLinks.AlternatePageExtension(target) is { } alternate)
        {
            pagesByPath.TryGetValue(alternate, out detail);
        }

        if (detail is null ||
            detail.Type != detailType ||
            string.IsNullOrWhiteSpace(detail.Slug))
        {
            return null;
        }

        return detail.Slug;
    }

    private static string? ExtractCardImage(string html)
    {
        var tag = ImgTagRegex.Match(html);
        if (!tag.Success)
            return null;

        var value = ReadImageAttribute(tag.Value, "data-src")
                    ?? ReadImageAttribute(tag.Value, "data-original")
                    ?? ReadImageAttribute(tag.Value, "src");
        return string.IsNullOrWhiteSpace(value) ||
               value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("//", StringComparison.Ordinal)
            ? null
            : value;
    }

    private static string? ReadImageAttribute(string tag, string name)
        => ImgSourceRegex.Matches(tag)
            .Cast<Match>()
            .FirstOrDefault(match => match.Groups[1].Value.Equals(name, StringComparison.OrdinalIgnoreCase))?
            .Groups[2].Value.Trim();

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

    private static void ExtractCollectionItems(
        DiscoveredPage source,
        DiscoveredPage original,
        ExtractedContent content,
        IReadOnlyDictionary<string, DiscoveredPage> pagesByPath)
    {
        // List cards read the original hrefs (demo paths); detail bodies read the
        // rewritten page whose links already point at the site URLs.
        if (original.Type is PageType.PostList)
        {
            ExtractPosts(original, content, pagesByPath);
        }
        else if (original.Type is PageType.PostDetail)
        {
            ExtractPostDetail(source, content);
        }
        else if (original.Type is PageType.CompanyList)
        {
            ExtractCompanies(original, content, pagesByPath);
        }
        else if (original.Type is PageType.CompanyDetail)
        {
            ExtractCompanyDetail(source, content);
        }
        else if (original.Type is PageType.ServiceList)
        {
            ExtractServices(original, content, pagesByPath);
        }
        else if (original.Type is PageType.ServiceDetail)
        {
            ExtractServiceDetail(source, content);
        }
    }

    private static void ExtractPosts(
        DiscoveredPage page,
        ExtractedContent content,
        IReadOnlyDictionary<string, DiscoveredPage> pagesByPath)
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
                ? ResolveDetailSlug(link.Groups[1].Value, page, pagesByPath, PageType.PostDetail)
                : null;

            var slug = !string.IsNullOrWhiteSpace(slugFromLink)
                ? slugFromLink
                : GetSlugWithFallback(title, "post", ref counter);

            content.Posts.Add(new PostRecord
            {
                Title = title,
                Slug = slug,
                Summary = p.Success ? StripHtml(p.Groups[1].Value).Trim() : null,
                Image = ExtractCardImage(cardHtml),
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
                Image = ExtractCardImage(page.UniqueBody),
                SeoTitle = title,
                SeoDescription = page.MetaDescription ?? ExtractSummary(page.UniqueBody)
            });
        }
    }

    private static void ExtractCompanies(
        DiscoveredPage page,
        ExtractedContent content,
        IReadOnlyDictionary<string, DiscoveredPage> pagesByPath)
    {
        var counter = 0;
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = CardHeadingRegex.Match(cardHtml);
            var p = CardParagraphRegex.Match(cardHtml);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            var link = CardLinkRegex.Match(cardHtml);
            var slugFromLink = link.Success
                ? ResolveDetailSlug(link.Groups[1].Value, page, pagesByPath, PageType.CompanyDetail)
                : null;

            content.Companies.Add(new CompanyRecord
            {
                Title = title,
                Slug = !string.IsNullOrWhiteSpace(slugFromLink)
                    ? slugFromLink
                    : GetSlugWithFallback(title, "company", ref counter),
                Summary = p.Success ? StripHtml(p.Groups[1].Value).Trim() : null,
                Image = ExtractCardImage(cardHtml),
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
            SeoDescription = page.MetaDescription ?? ExtractSummary(page.UniqueBody)
        });
    }

    private static void ExtractServices(
        DiscoveredPage page,
        ExtractedContent content,
        IReadOnlyDictionary<string, DiscoveredPage> pagesByPath)
    {
        var counter = 0;
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = CardHeadingRegex.Match(cardHtml);
            var p = CardParagraphRegex.Match(cardHtml);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            var link = CardLinkRegex.Match(cardHtml);
            var slugFromLink = link.Success
                ? ResolveDetailSlug(link.Groups[1].Value, page, pagesByPath, PageType.ServiceDetail)
                : null;

            content.Services.Add(new ServiceRecord
            {
                Title = title,
                Slug = !string.IsNullOrWhiteSpace(slugFromLink)
                    ? slugFromLink
                    : GetSlugWithFallback(title, "service", ref counter),
                Summary = p.Success ? StripHtml(p.Groups[1].Value).Trim() : null,
                Image = ExtractCardImage(cardHtml),
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
            SeoDescription = page.MetaDescription ?? ExtractSummary(page.UniqueBody)
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

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ImgTagPattern();

    [GeneratedRegex(@"\b(data-src|data-original|src)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex ImgSourcePattern();

    [GeneratedRegex(@"<a[^>]*href=[""']([^""']*)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex CardLinkPattern();

    [GeneratedRegex(@"<h[3-6][^>]*>(.*?)</h[3-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FaqHeadingPattern();

    [GeneratedRegex(@"<(?:section|div)[^>]*class\s*=\s*""[^""]*(?:hero|cta|stats|features|about)[^""]*""[^>]*>(.*?)</(?:section|div)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SectionOuterPattern();

    [GeneratedRegex(@"class\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex SectionClassPattern();
}
