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
}
