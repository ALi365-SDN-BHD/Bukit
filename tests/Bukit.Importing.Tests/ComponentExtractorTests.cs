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
    public void Extract_HeroClass_FindsComponent()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<section class=\"hero\"><h1>Welcome</h1><p>Description</p><a href=\"/about/\">Learn</a></section>")
        };

        var components = ComponentExtractor.Extract(pages);

        Assert.Contains(components, c => c.Name == "hero");
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
    public void Extract_FaqItemClass_FindsComponent()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<div class=\"faq-item\"><h3>Question?</h3><p>Answer.</p></div>")
        };

        var components = ComponentExtractor.Extract(pages);

        Assert.Contains(components, c => c.Name == "faq");
    }

    [Fact]
    public void Extract_CtaClass_FindsComponent()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<section class=\"cta\"><h2>Get Started</h2><a href=\"/signup/\">Sign Up</a></section>")
        };

        var components = ComponentExtractor.Extract(pages);

        Assert.Contains(components, c => c.Name == "cta");
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
            MakePage("<section class=\"hero\"><h1>Welcome</h1><p>Description</p></section>")
        };

        var components = ComponentExtractor.Extract(pages);

        var hero = components.First(c => c.Name == "hero");
        Assert.NotNull(hero.NormalizedTemplate);
        Assert.Contains("{{ section.heading }}", hero.NormalizedTemplate);
    }

    [Fact]
    public void Extract_MultiplePages_SharesComponent()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<section class=\"hero\"><h1>Home</h1></section>", "index", PageType.Home),
            MakePage("<section class=\"hero\"><h1>About</h1></section>", "about", PageType.Page),
        };

        var components = ComponentExtractor.Extract(pages);

        var hero = components.First(c => c.Name == "hero");
        Assert.Equal(2, hero.UsedBy.Count);
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
    }
}
