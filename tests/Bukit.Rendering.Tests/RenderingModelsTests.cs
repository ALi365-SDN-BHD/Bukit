using Bukit.Rendering;
using Xunit;

namespace Bukit.Rendering.Tests;

public sealed class RenderingModelsTests
{
    [Fact]
    public void RenderingSurface_DoesNotExposeAnalyticsModelOrSiteProperty()
    {
        Assert.Null(typeof(SiteModel).Assembly.GetType("Bukit.Rendering.AnalyticsModel"));
        Assert.Null(typeof(SiteModel).GetProperty("Analytics"));
    }

    [Fact]
    public void GeoModels_PreserveConfiguredValues()
    {
        var faq = new GeoFaqModel
        {
            Question = "What is Bukit?",
            Answer = "A static site generator."
        };
        var step = new GeoHowToStepModel
        {
            Name = "Build",
            Text = "Run the build.",
            Image = "/img/build.png",
            Url = "https://example.com/build"
        };
        var citation = new GeoCitationModel
        {
            Title = "Reference",
            Url = "https://example.com/reference"
        };
        var author = new GeoAuthorModel
        {
            Name = "Ali",
            Url = "https://example.com/authors/ali",
            SameAs = ["https://x.example/ali", "https://github.com/ali"]
        };

        Assert.Equal("What is Bukit?", faq.Question);
        Assert.Equal("A static site generator.", faq.Answer);
        Assert.Equal("Build", step.Name);
        Assert.Equal("Run the build.", step.Text);
        Assert.Equal("/img/build.png", step.Image);
        Assert.Equal("https://example.com/build", step.Url);
        Assert.Equal("Reference", citation.Title);
        Assert.Equal("https://example.com/reference", citation.Url);
        Assert.Equal("Ali", author.Name);
        Assert.Equal("https://example.com/authors/ali", author.Url);
        Assert.Equal(2, author.SameAs.Count);
    }

    [Fact]
    public void SeoAndPageModels_PreserveConfiguredValuesAndDefaults()
    {
        var seo = new SeoModel
        {
            Title = "Bukit",
            Description = "Docs",
            Canonical = "https://example.com/docs",
            Robots = "index,follow",
            SchemaType = "Article",
            SpeakableXPath = "/html/body/main",
            SameAs = ["https://github.com/example/bukit"],
            Og = new SeoOpenGraphModel
            {
                Title = "OG Bukit",
                Description = "OG Docs",
                Url = "https://example.com/og",
                Image = "/img/og.png",
                SiteName = "Bukit",
                Locale = "en-US"
            },
            Twitter = new SeoTwitterModel
            {
                Title = "Twitter Bukit",
                Description = "Twitter Docs",
                Image = "/img/twitter.png",
                Site = "@bukit",
                Creator = "@ali"
            },
            Article = new SeoArticleModel
            {
                PublishedTime = new DateTimeOffset(2026, 6, 14, 0, 0, 0, TimeSpan.Zero),
                ModifiedTime = new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero),
                Author = "Ali",
                Tags = ["coverage", "rendering"]
            },
            Alternates = [new SeoAlternateModel("zh-CN", "https://example.com/zh/docs")],
            JsonLd = ["{\"@type\":\"Article\"}"],
            FaqItems =
            [
                new GeoFaqModel { Question = "Q1", Answer = "A1" }
            ],
            HowToSteps =
            [
                new GeoHowToStepModel { Name = "Step 1", Text = "Do it" }
            ],
            Citations =
            [
                new GeoCitationModel { Title = "Ref", Url = "https://example.com/ref" }
            ],
            GeoAuthor = new GeoAuthorModel { Name = "Ali" }
        };

        var site = new SiteModel
        {
            Name = "bukit",
            Title = "Bukit",
            Url = "https://example.com",
            Description = "Example site",
            BaseUrl = "/",
            Language = "en",
            Params = new Dictionary<string, object> { ["brand"] = "Bukit" },
            Modules = new Dictionary<string, IReadOnlyList<ModuleInfo>>
            {
                ["guides"] =
                [
                    new ModuleInfo
                    {
                        Id = "getting-started",
                        Title = "Getting Started",
                        Slug = "getting-started",
                        Content = "Hello"
                    }
                ]
            },
            Data = new Dictionary<string, object> { ["featureFlag"] = true }
        };

        var page = new PageInfo
        {
            Title = "Coverage",
            Url = "/coverage/",
            Content = "<p>Coverage</p>",
            Summary = "Coverage summary",
            PublishDate = new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero),
            Seo = seo
        };

        var pageModel = new PageModel
        {
            Site = site,
            Page = page
        };

        var listPageModel = new ListPageModel
        {
            Site = site,
            Page = page,
            Pages = [page]
        };

        Assert.Equal("Bukit", seo.Title);
        Assert.Equal("Docs", seo.Description);
        Assert.Equal("https://example.com/docs", seo.Canonical);
        Assert.Equal("index,follow", seo.Robots);
        Assert.Equal("website", new SeoOpenGraphModel().Type);
        Assert.Equal("summary", new SeoTwitterModel().Card);
        Assert.Equal("Ali", seo.Article.Author);
        Assert.Equal(2, seo.Article.Tags.Count);
        Assert.Equal("Article", seo.SchemaType);
        Assert.Single(seo.Alternates);
        Assert.Single(seo.JsonLd);
        Assert.Single(seo.FaqItems!);
        Assert.Single(seo.HowToSteps!);
        Assert.Single(seo.Citations!);
        Assert.Equal("Ali", seo.GeoAuthor!.Name);
        Assert.Equal("/html/body/main", seo.SpeakableXPath);
        Assert.Single(seo.SameAs!);

        Assert.Equal("bukit", site.Name);
        Assert.Equal("Bukit", site.Title);
        Assert.Equal("https://example.com", site.Url);
        Assert.Equal("Example site", site.Description);
        Assert.Equal("/", site.BaseUrl);
        Assert.Equal("en", site.Language);
        Assert.Equal("Bukit", site.Params!["brand"]);
        Assert.Single(site.Modules!["guides"]);
        Assert.True((bool)site.Data!["featureFlag"]);

        var module = Assert.Single(site.Modules["guides"]);
        Assert.Equal("getting-started", module.Id);
        Assert.Equal("Getting Started", module.Title);
        Assert.Equal("getting-started", module.Slug);
        Assert.Equal("Hello", module.Content);
        Assert.Null(module.Fields);

        Assert.Equal("Coverage", page.Title);
        Assert.Equal("/coverage/", page.Url);
        Assert.Equal("<p>Coverage</p>", page.Content);
        Assert.Equal("Coverage summary", page.Summary);
        Assert.Null(page.TableOfContents);
        Assert.Equal(new DateTimeOffset(2026, 6, 14, 8, 0, 0, TimeSpan.Zero), page.PublishDate);
        Assert.Null(page.Fields);
        Assert.Same(seo, page.Seo);
        Assert.Null(page.ContentRecord);
        Assert.Null(page.Route);
        Assert.Null(page.Publish);
        Assert.Null(page.Entities);
        Assert.Null(page.Provenance);
        Assert.Null(page.Trust);
        Assert.Null(page.Representations);

        Assert.Same(site, pageModel.Site);
        Assert.Same(page, pageModel.Page);
        Assert.Same(site, listPageModel.Site);
        Assert.Same(page, listPageModel.Page);
        Assert.Single(listPageModel.Pages);
    }
}
