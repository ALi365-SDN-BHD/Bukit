using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ComponentExtractorTests
{
    private static DiscoveredPage MakePage(string html, string slug = "page", PageType type = PageType.Page)
    {
        return new DiscoveredPage
        {
            FilePath = $"/test/{slug}.html",
            RelativePath = $"{slug}.html",
            Slug = slug,
            Type = type,
            Title = "Test",
            FullHtml = html,
            BodyContent = html,
            BodyOpening = "",
            UniqueBody = html,
            BodyClosing = ""
        };
    }

    [Fact]
    public void Extract_SectionClasses_AreNotExtracted()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage(
                "<section class=\"hero\"><h1>Welcome</h1></section>" +
                "<section class=\"cta\"><h2>Get Started</h2></section>" +
                "<div class=\"faq-item\"><h3>Question?</h3></div>")
        };

        var components = ComponentExtractor.Extract(pages);

        Assert.Empty(components);
    }

    [Fact]
    public void Extract_ArticleCardClass_FindsComponent()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<div class=\"article-card\"><h3>Title</h3><p>Summary</p></div>", "insights", PageType.PostList)
        };

        var components = ComponentExtractor.Extract(pages);

        Assert.Contains(components, c => c.Name == "article-card");
    }

    [Fact]
    public void Extract_CompanyCardClass_FindsComponent()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<div class=\"company-card\"><h3>ACME</h3><p>Company</p></div>", "companies", PageType.CompanyList)
        };

        var components = ComponentExtractor.Extract(pages);

        Assert.Contains(components, c => c.Name == "company-card");
    }


    [Fact]
    public void Extract_NoMatchingClass_ReturnsEmpty()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<div class=\"custom-class\"><p>Nothing</p></div>")
        };

        var components = ComponentExtractor.Extract(pages);

        Assert.Empty(components);
    }

    [Fact]
    public void Extract_ComponentHasNormalizedTemplate()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<article class=\"article-card\"><h3>Title</h3><p>Summary</p></article>", "insights", PageType.PostList)
        };

        var components = ComponentExtractor.Extract(pages);

        var card = components.First(c => c.Name == "article-card");
        Assert.NotNull(card.NormalizedTemplate);
        Assert.Contains("{{ item.title }}", card.NormalizedTemplate);
        Assert.Contains("{{ item.summary }}", card.NormalizedTemplate);
        Assert.DoesNotContain("Title", card.NormalizedTemplate);
    }

    [Fact]
    public void Extract_MultiplePages_SharesComponent()
    {
        const string card = "<article class=\"article-card\"><h3>Title</h3><p>Summary</p></article>";
        var pages = new List<DiscoveredPage>
        {
            MakePage(card, "news", PageType.PostList),
            MakePage(card, "blog", PageType.PostList),
        };

        var components = ComponentExtractor.Extract(pages);

        var articleCard = components.First(c => c.Name == "article-card");
        Assert.Equal(2, articleCard.UsedBy.Count);
    }

    [Fact]
    public void Extract_CardComponent_GeneratesListTemplate()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<div class=\"article-card\"><h3>Item</h3><p>Desc</p><a href=\"/item\">Link</a></div>", "insights", PageType.PostList)
        };

        var components = ComponentExtractor.Extract(pages);

        var card = components.First(c => c.Name == "article-card");
        Assert.Contains("{{ item.url }}", card.NormalizedTemplate);
        Assert.Contains("{{ item.title }}", card.NormalizedTemplate);
        Assert.Contains("{{ item.summary }}", card.NormalizedTemplate);
        Assert.DoesNotContain("Desc", card.NormalizedTemplate);
    }

    [Fact]
    public void Extract_PaginationComponent_GeneratesGenericTemplate()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<div class=\"pagination\"><a href=\"page-1.html\">← 上一页</a><span class=\"page-label\">第 2 / 3 页</span><a href=\"page-3.html\">下一页 →</a></div>")
        };

        var components = ComponentExtractor.Extract(pages);

        var pagination = components.First(c => c.Name == "pagination");
        Assert.Contains("{{ pagination.page }}", pagination.NormalizedTemplate);
        Assert.Contains("{{ pagination.total_pages }}", pagination.NormalizedTemplate);
        Assert.DoesNotContain("上一页", pagination.NormalizedTemplate);
        Assert.DoesNotContain("下一页", pagination.NormalizedTemplate);
        Assert.DoesNotContain("第 2 / 3 页", pagination.NormalizedTemplate);
    }
}
