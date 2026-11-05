using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class PageClassifierTests
{
    [Theory]
    [InlineData("index", PageType.Home)]
    [InlineData("about", PageType.Page)]
    [InlineData("contact", PageType.Page)]
    [InlineData("privacy", PageType.Page)]
    [InlineData("terms", PageType.Page)]
    [InlineData("insights", PageType.PostList)]
    [InlineData("blog", PageType.PostList)]
    [InlineData("news", PageType.PostList)]
    [InlineData("article", PageType.PostDetail)]
    [InlineData("article-detail", PageType.PostDetail)]
    [InlineData("post", PageType.PostDetail)]
    [InlineData("companies", PageType.CompanyList)]
    [InlineData("company", PageType.CompanyDetail)]
    [InlineData("company-detail", PageType.CompanyDetail)]
    [InlineData("services", PageType.ServiceList)]
    [InlineData("service-detail", PageType.ServiceDetail)]
    [InlineData("service", PageType.ServiceDetail)]
    public void Classify_ByFileName_ReturnsCorrectType(string fileName, PageType expected)
    {
        var result = PageClassifier.Classify(fileName, "");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Classify_UnknownFileName_ReturnsUnknown()
    {
        var result = PageClassifier.Classify("random-page", "");
        Assert.Equal(PageType.Unknown, result);
    }

    [Fact]
    public void Classify_MultipleArticleCards_ReturnsPostList()
    {
        var html = """
            <div class="article-card">...</div>
            <div class="article-card">...</div>
            """;
        var result = PageClassifier.Classify("custom-name", html);
        Assert.Equal(PageType.PostList, result);
    }

    [Fact]
    public void Classify_MultipleCompanyCards_ReturnsCompanyList()
    {
        var html = """
            <div class="company-card">...</div>
            <div class="company-card">...</div>
            """;
        var result = PageClassifier.Classify("custom-name", html);
        Assert.Equal(PageType.CompanyList, result);
    }

    [Fact]
    public void Classify_MultipleServiceCards_ReturnsServiceList()
    {
        var html = """
            <div class="service-card">...</div>
            <div class="service-card">...</div>
            """;
        var result = PageClassifier.Classify("custom-name", html);
        Assert.Equal(PageType.ServiceList, result);
    }

    [Theory]
    [InlineData("index.html", "index")]
    [InlineData("about.html", "about")]
    [InlineData("about/index.html", "about")]
    [InlineData("news/expo/index.html", "expo")]
    [InlineData("news/expo.html", "expo")]
    [InlineData("about", "about")]
    public void PageName_FolderIndexStandsForItsFolder(string path, string expected)
        => Assert.Equal(expected, PageClassifier.PageName(path));

    [Theory]
    [InlineData("news/expo.html", "news")]
    [InlineData("news/expo/index.html", "news")]
    [InlineData("about/index.html", "")]
    [InlineData("about.html", "")]
    public void PageFolder_IgnoresTheFolderOfAFolderPage(string path, string expected)
        => Assert.Equal(expected, PageClassifier.PageFolder(path));

    [Fact]
    public void Classify_NestedIndexIsNotTheHomePage()
    {
        Assert.Equal(PageType.Home, PageClassifier.Classify("index.html", "<main></main>"));
        Assert.Equal(PageType.Page, PageClassifier.Classify("about/index.html", "<main></main>"));
        Assert.Equal(PageType.PostList, PageClassifier.Classify("news/index.html", "<main></main>"));
    }

    [Fact]
    public void FindEntry_ExactPathWinsOverName()
    {
        var routeMap = new RouteMapConfig
        {
            Pages =
            [
                new RouteMapPage { Source = "about.html", Route = "/about/" },
                new RouteMapPage { Source = "en/about/index.html", Route = "/en/about/" }
            ]
        };

        Assert.Equal("/en/about/", PageClassifier.FindEntry(routeMap, "en/about/index.html")?.Route);
        Assert.Equal("/about/", PageClassifier.FindEntry(routeMap, "about/index.html")?.Route);
        Assert.Null(PageClassifier.FindEntry(routeMap, "index.html"));
    }

    [Fact]
    public void Classify_EntryWithoutTypeKeepsFileNameRules()
    {
        var routeMap = new RouteMapConfig { Pages = [new RouteMapPage { Source = "news.html", Route = "/news/" }] };

        Assert.Equal(PageType.PostList, PageClassifier.Classify("news.html", "<main></main>", routeMap));
    }

    [Fact]
    public void FindEntry_PathSourceMatchesOnlyThatFile()
    {
        var routeMap = new RouteMapConfig { Pages = [new RouteMapPage { Source = "about/index.html", Route = "/company/" }] };

        Assert.Equal("/company/", PageClassifier.FindEntry(routeMap, "about/index.html")?.Route);
        Assert.Null(PageClassifier.FindEntry(routeMap, "about.html"));
    }

    [Fact]
    public void PageTemplatePlan_PathEntryBelongsToItsOwnFileNotAnEarlierSameNamedPage()
    {
        var routeMap = new RouteMapConfig
        {
            Pages = [new RouteMapPage { Source = "about/index.html", Route = "/company/", Type = "page" }]
        };
        DiscoveredPage Make(string path) => new()
        {
            FilePath = path, RelativePath = path, Slug = "about",
            Type = PageClassifier.Classify(path, "<main></main>", routeMap)
        };
        var flat = Make("about.html");
        var folder = Make("about/index.html");

        var plan = PageTemplatePlan.Build([flat, folder], routeMap);

        Assert.Null(plan.RouteFor(flat));
        Assert.Equal("/company/", plan.RouteFor(folder));
    }

    [Fact]
    public void FindEntry_DotSlashSourceIsABareName()
    {
        var routeMap = new RouteMapConfig { Pages = [new RouteMapPage { Source = "./about.html", Route = "/company/" }] };

        Assert.Equal("/company/", PageClassifier.FindEntry(routeMap, "about.html")?.Route);
    }
}
