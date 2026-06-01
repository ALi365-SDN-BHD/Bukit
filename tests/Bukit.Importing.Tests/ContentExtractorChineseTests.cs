using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class ContentExtractorChineseTests
{
    private static DiscoveredPage MakePage(string uniqueBody, string slug = "page",
        PageType type = PageType.Page)
    {
        return new DiscoveredPage
        {
            FilePath = $"/test/{slug}.html",
            RelativePath = $"{slug}.html",
            Slug = slug,
            Type = type,
            Title = "测试页面",
            FullHtml = $"<html><body>{uniqueBody}</body></html>",
            BodyContent = uniqueBody,
            UniqueBody = uniqueBody
        };
    }

    [Fact]
    public void ChineseArticleCards_DoNotProduceEmptySlug()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>资讯</h1>" +
                "<article class=\"article-card\"><h3>马来西亚投资指南</h3><p>投资介绍。</p></article>" +
                "<article class=\"article-card\"><h3>中马贸易机会</h3><p>贸易分析。</p></article>" +
                "<article class=\"article-card\"><h3>东盟市场</h3><p>分析报告。</p></article>" +
                "</main>", "insights", PageType.PostList)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Equal(3, content.Posts.Count);
        Assert.All(content.Posts, p => Assert.False(string.IsNullOrWhiteSpace(p.Slug),
            $"Post '{p.Title}' has empty slug"));
    }

    [Fact]
    public void ChineseCompanyCards_DoNotProduceEmptySlug()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>企业</h1>" +
                "<article class=\"company-card\"><h3>马中贸易控股</h3><p>贸易公司。</p></article>" +
                "<article class=\"company-card\"><h3>腾达科技</h3><p>IT公司。</p></article>" +
                "</main>", "companies", PageType.CompanyList)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Equal(2, content.Companies.Count);
        Assert.All(content.Companies, c => Assert.False(string.IsNullOrWhiteSpace(c.Slug),
            $"Company '{c.Title}' has empty slug"));
    }

    [Fact]
    public void ChineseServiceCards_DoNotProduceEmptySlug()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>服务</h1>" +
                "<article class=\"service-card\"><h3>企业设立</h3><p>服务介绍。</p></article>" +
                "<article class=\"service-card\"><h3>签证办理</h3><p>服务介绍。</p></article>" +
                "</main>", "services", PageType.ServiceList)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Equal(2, content.Services.Count);
        Assert.All(content.Services, s => Assert.False(string.IsNullOrWhiteSpace(s.Slug),
            $"Service '{s.Title}' has empty slug"));
    }

    [Fact]
    public void ChineseSlug_PostFallback_UsesSlugHelper()
    {
        var pages = new List<DiscoveredPage>
        {
            MakePage("<main><h1>Blog</h1>" +
                "<article class=\"article-card\"><h3>关于我们</h3><p>Desc</p></article>" +
                "</main>", "blog", PageType.PostList)
        };

        var content = ContentExtractor.Extract(pages);

        Assert.Single(content.Posts);
        Assert.NotEmpty(content.Posts[0].Slug);
    }
}
