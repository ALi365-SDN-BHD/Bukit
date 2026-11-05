using Xunit;

namespace Bukit.Importing.Tests;

public sealed class SiteIdentityTests
{
    private static DiscoveredPage Page(string path, PageType type, string? title, string? canonical = null)
        => new() { FilePath = path, RelativePath = path, Slug = "", Type = type, Title = title, CanonicalUrl = canonical };

    [Fact]
    public void Name_UsesTheTitleSegmentPagesShare()
    {
        var pages = new[]
        {
            Page("index.html", PageType.Home, "首页 - 华南精密"),
            Page("about.html", PageType.Page, "关于我们-华南精密"),
            Page("e.html", PageType.Page, "E-commerce | Acme Group"),
            Page("news.html", PageType.PostList, "新闻 - 华南精密")
        };

        Assert.Equal("华南精密", SiteIdentity.Name(pages));
    }

    [Fact]
    public void Name_SinglePageUsesItsTitle()
        => Assert.Equal("Acme", SiteIdentity.Name([Page("index.html", PageType.Home, "Acme")]));

    [Fact]
    public void Name_GenericHomeTitleIsNotASiteName()
        => Assert.Null(SiteIdentity.Name([Page("index.html", PageType.Home, "首页"), Page("a.html", PageType.Page, "关于我们")]));

    [Fact]
    public void Url_PrefersAbsoluteBaseUrlThenCanonical()
    {
        var pages = new[] { Page("index.html", PageType.Home, "Acme", "https://www.acme.cn/index.html") };

        Assert.Equal("https://www.acme.cn", SiteIdentity.Url(pages, "/corp/"));
        Assert.Equal("https://acme.org", SiteIdentity.Url(pages, "https://acme.org/corp/"));
        Assert.Null(SiteIdentity.Url([Page("index.html", PageType.Home, "Acme", "/relative/")], null));
    }

    [Fact]
    public void Name_HyphenatedWordsStayWhole()
    {
        var name = SiteIdentity.Name([
            Page("index.html", PageType.Home, "E-commerce Solutions"),
            Page("blog.html", PageType.PostList, "E-commerce Blog")]);

        Assert.Equal("E-commerce Solutions", name);
    }
}
