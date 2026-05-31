using System.Text.RegularExpressions;

namespace Bukit.Importing;

internal static partial class ContentExtractor
{
    private static readonly Regex H1Regex = H1Pattern();
    private static readonly Regex FirstParagraphRegex = FirstParagraphPattern();
    private static readonly Regex CardRegex = CardPattern();
    private static readonly Regex FaqItemRegex = FaqItemPattern();
    private static readonly Regex SectionHeadingRegex = SectionHeadingPattern();
    private static readonly Regex SectionLinkRegex = SectionLinkPattern();

    internal static ExtractedContent Extract(List<DiscoveredPage> pages)
    {
        var content = new ExtractedContent();

        foreach (var page in pages)
        {
            ExtractPageContent(page, content);
            ExtractCollectionItems(page, content);
            ExtractSections(page, content);
            ExtractFaqs(page, content);
        }

        content.Posts = content.Posts
            .GroupBy(p => p.Title)
            .Select(g => g.First())
            .ToList();

        content.Companies = content.Companies
            .GroupBy(c => c.Title)
            .Select(g => g.First())
            .ToList();

        content.Services = content.Services
            .GroupBy(s => s.Title)
            .Select(g => g.First())
            .ToList();

        content.Faqs = content.Faqs
            .GroupBy(f => f.Question)
            .Select(g => g.First())
            .ToList();

        return content;
    }

    private static void ExtractPageContent(DiscoveredPage page, ExtractedContent content)
    {
        if (page.Type is PageType.PostList or PageType.CompanyList or PageType.ServiceList or PageType.Unknown)
            return;

        var h1 = H1Regex.Match(page.UniqueBody);
        var title = h1.Success ? h1.Groups[1].Value.Trim() : page.Title ?? page.Slug;
        var summary = ExtractSummary(page.UniqueBody);

        content.Pages.Add(new PageRecord
        {
            Title = title,
            Slug = page.Slug,
            Type = page.Type == PageType.Home ? "Home" : "Page",
            Template = page.Type switch
            {
                PageType.Home => "index",
                PageType.PostDetail => "article",
                PageType.CompanyDetail => "company",
                _ => "page"
            },
            Summary = summary,
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
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = Regex.Match(cardHtml, @"<h3[^>]*>(.*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var p = Regex.Match(cardHtml, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var link = Regex.Match(cardHtml, @"<a[^>]*href=[""']([^""']*)[""']", RegexOptions.IgnoreCase);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            var slugFromLink = link.Success
                ? Path.GetFileNameWithoutExtension(link.Groups[1].Value.TrimEnd('/'))
                : Slugify(title);

            content.Posts.Add(new PostRecord
            {
                Title = title,
                Slug = slugFromLink,
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
            content.Posts.Add(new PostRecord
            {
                Title = title,
                Slug = string.IsNullOrWhiteSpace(page.Slug) ? Slugify(title) : page.Slug,
                Summary = ExtractSummary(page.UniqueBody),
                Content = page.UniqueBody,
                SeoTitle = title,
                SeoDescription = ExtractSummary(page.UniqueBody)
            });
        }
    }

    private static void ExtractCompanies(DiscoveredPage page, ExtractedContent content)
    {
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = Regex.Match(cardHtml, @"<h3[^>]*>(.*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var p = Regex.Match(cardHtml, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            content.Companies.Add(new CompanyRecord
            {
                Title = title,
                Slug = Slugify(title),
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
        content.Companies.Add(new CompanyRecord
        {
            Title = title,
            Slug = string.IsNullOrWhiteSpace(page.Slug) ? Slugify(title) : page.Slug,
            Summary = ExtractSummary(page.UniqueBody),
            Content = page.UniqueBody,
            SeoTitle = title,
            SeoDescription = ExtractSummary(page.UniqueBody)
        });
    }

    private static void ExtractServices(DiscoveredPage page, ExtractedContent content)
    {
        foreach (Match match in CardRegex.Matches(page.UniqueBody))
        {
            var cardHtml = match.Value;
            var h3 = Regex.Match(cardHtml, @"<h3[^>]*>(.*?)</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var p = Regex.Match(cardHtml, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            var title = h3.Success ? StripHtml(h3.Groups[1].Value).Trim() : "";
            if (string.IsNullOrWhiteSpace(title)) continue;

            content.Services.Add(new ServiceRecord
            {
                Title = title,
                Slug = Slugify(title),
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
        content.Services.Add(new ServiceRecord
        {
            Title = title,
            Slug = string.IsNullOrWhiteSpace(page.Slug) ? Slugify(title) : page.Slug,
            Summary = ExtractSummary(page.UniqueBody),
            Content = page.UniqueBody,
            SeoTitle = title,
            SeoDescription = ExtractSummary(page.UniqueBody)
        });
    }

    private static void ExtractSections(DiscoveredPage page, ExtractedContent content)
    {
        if (page.Type != PageType.Home) return;

        var sections = Regex.Matches(page.UniqueBody,
            @"<(?:section|div)[^>]*class\s*=\s*""[^""]*(?:hero|cta|stats|features|about)[^""]*""[^>]*>(.*?)</(?:section|div)>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var sortOrder = 10;
        foreach (Match section in sections)
        {
            var sectionHtml = section.Value;
            var classMatch = Regex.Match(sectionHtml, @"class\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
            var className = classMatch.Success ? classMatch.Groups[1].Value : "unknown";
            var sectionType = DetermineSectionType(className);

            var heading = SectionHeadingRegex.Match(sectionHtml);
            var link = SectionLinkRegex.Match(sectionHtml);

            content.Sections.Add(new SectionRecord
            {
                PageSlug = page.Slug,
                SectionType = sectionType,
                Heading = heading.Success ? StripHtml(heading.Groups[2].Value).Trim() : null,
                Subheading = null,
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
            var h3 = Regex.Match(itemHtml, @"<h[3-6][^>]*>(.*?)</h[3-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var p = Regex.Match(itemHtml, @"<p[^>]*>(.*?)</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

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

    private static string Slugify(string text)
    {
        var slug = text.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        return slug.Trim('-');
    }

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
}
