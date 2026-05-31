using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ContentExtractorTests
{
    private static DiscoveredPage MakePage(string uniqueBody, string slug = "page",
        PageType type = PageType.Page, string title = "Test Title")
    {
        return new DiscoveredPage
        {
            FilePath = $"/test/{slug}.html",
            RelativePath = $"{slug}.html",
            Slug = slug,
            Type = type,
            Title = title,
            FullHtml = $"<html><head><title>{title}</title></head><body>{uniqueBody}</body></html>",
            BodyContent = uniqueBody,
            UniqueBody = uniqueBody
        };
    }

    [Fact]
    public void Extract_PageContent_ExtractsH1AndSummary()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>About Us</h1><p>We are a company.</p></main>", "about")
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Single(content.Pages);
        Assert.Equal("About Us", content.Pages[0].Title);
        Assert.Equal("We are a company.", content.Pages[0].Summary);
    }

    [Fact]
    public void Extract_HomePage_SetsTypeHome()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Home</h1><p>Welcome.</p></main>", "", PageType.Home)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Single(content.Pages);
        Assert.Equal("Home", content.Pages[0].Type);
        Assert.Equal("index", content.Pages[0].Template);
    }

    [Fact]
    public void Extract_PostDetail_PageRecordTypeArticle()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Post Title</h1><p>Content.</p></main>", "my-post", PageType.PostDetail)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Single(content.Pages);
        Assert.Equal("article", content.Pages[0].Template);
    }

    [Fact]
    public void Extract_ListPage_NotInPages()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Insights</h1><div class=\"article-card\"><h3>A</h3><p>B</p></div></main>", "insights", PageType.PostList)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Empty(content.Pages);
    }

    [Fact]
    public void Extract_ArticleCards_ExtractsPosts()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Blog</h1>" +
                "<article class=\"article-card\"><h3>Guide</h3><p>Summary text.</p><a href=\"/insights/guide/\">Read</a></article>" +
                "<article class=\"article-card\"><h3>Trade</h3><p>Another one.</p><a href=\"/insights/trade/\">Read</a></article>" +
                "</main>", "insights", PageType.PostList)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Equal(2, content.Posts.Count);
        Assert.Contains(content.Posts, p => p.Title == "Guide");
        Assert.Contains(content.Posts, p => p.Title == "Trade");
    }

    [Fact]
    public void Extract_Posts_DeduplicatesByTitle()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Blog</h1>" +
                "<article class=\"article-card\"><h3>Guide</h3><p>Same.</p></article>" +
                "</main>", "insights", PageType.PostList),
            MakePage("<main><h1>Blog</h1>" +
                "<article class=\"article-card\"><h3>Guide</h3><p>Same.</p></article>" +
                "</main>", "blog", PageType.PostList),
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Single(content.Posts);
    }

    [Fact]
    public void Extract_CompanyCards_ExtractsCompanies()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Directory</h1>" +
                "<article class=\"company-card\"><h3>ACME Corp</h3><p>Trading company.</p></article>" +
                "</main>", "companies", PageType.CompanyList)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Single(content.Companies);
        Assert.Equal("ACME Corp", content.Companies[0].Title);
    }

    [Fact]
    public void Extract_FaqItems_ExtractsFaqs()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>FAQ</h1>" +
                "<div class=\"faq-item\"><h3>What is this?</h3><p>This is a service.</p></div>" +
                "<div class=\"faq-item\"><h3>How much?</h3><p>Free.</p></div>" +
                "</main>", "faq")
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Equal(2, content.Faqs.Count);
        Assert.Contains(content.Faqs, f => f.Question == "What is this?");
        Assert.Contains(content.Faqs, f => f.Answer == "Free.");
    }

    [Fact]
    public void Extract_HomePageSections_ExtractsHeroAndCta()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<section class=\"hero\"><h1>Welcome</h1><a href=\"/about/\">Learn</a></section>" +
                "<section class=\"cta\"><h2>Start</h2><a href=\"/signup/\">Sign Up</a></section>",
                "", PageType.Home)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Equal(2, content.Sections.Count);
        Assert.Contains(content.Sections, s => s.SectionType == "hero");
        Assert.Contains(content.Sections, s => s.SectionType == "cta");
    }

    [Fact]
    public void Extract_EmptyInput_ReturnsEmptyContent()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Page</h1></main>", "test")
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Empty(content.Posts);
        Assert.Empty(content.Companies);
        Assert.Empty(content.Faqs);
    }

    [Fact]
    public void Extract_SummaryTruncatesLongText()
    {
        var longText = new string('A', 300);
        var pages = new List<DiscoveredPage>
        {
            MakePage($"<main><h1>About</h1><p>{longText}</p></main>", "about")
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Single(content.Pages);
        Assert.EndsWith("...", content.Pages[0].Summary);
        Assert.True(content.Pages[0].Summary!.Length <= 203);
    }
}
