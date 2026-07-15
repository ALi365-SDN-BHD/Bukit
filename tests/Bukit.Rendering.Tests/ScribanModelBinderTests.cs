using Xunit;
using Bukit.Engine.Abstractions.Content;
using Bukit.Rendering.Scriban;
using Scriban.Runtime;

namespace Bukit.Rendering.Tests;

public sealed class ScribanModelBinderTests
{
    private static SiteModel CreateFullSite()
    {
        return new SiteModel
        {
            Name = "my-site",
            Title = "My Site",
            Url = "https://example.com",
            Description = "A test site",
            BaseUrl = "/en/",
            Language = "en",
            BuildYear = 2026,
            Params = new Dictionary<string, object>
            {
                ["theme_color"] = "#ff0000",
                ["enable_comments"] = true
            },
            Modules = new Dictionary<string, IReadOnlyList<ModuleInfo>>
            {
                ["header"] = new[]
                {
                    new ModuleInfo
                    {
                        Id = "nav-main",
                        Title = "Main Navigation",
                        Slug = "nav-main",
                        Content = "<nav>links</nav>",
                        Fields = new Dictionary<string, ContentField>
                        {
                            ["position"] = new("text", "top")
                        }
                    }
                },
                ["footer"] = new[]
                {
                    new ModuleInfo
                    {
                        Id = "footer-copyright",
                        Title = "Copyright",
                        Slug = "footer-copyright",
                        Content = "<p>&copy; 2025</p>"
                    }
                }
            },
            Data = new Dictionary<string, object>
            {
                ["social_links"] = new Dictionary<string, object>
                {
                    ["github"] = "https://github.com/example",
                    ["twitter"] = "https://twitter.com/example"
                },
                ["version"] = 2
            },
            DataIndex = new Dictionary<string, object>
            {
                ["settings"] = new Dictionary<string, object>
                {
                    ["contact"] = new Dictionary<string, object>
                    {
                        ["email"] = "contact@example.com"
                    }
                }
            }
        };
    }

    private static SiteModel CreateMinimalSite()
    {
        return new SiteModel
        {
            Name = "minimal",
            Title = "Minimal Site",
            BaseUrl = "/",
            Language = "zh-CN"
        };
    }

    private static PageInfo CreateFullPage()
    {
        return new PageInfo
        {
            Title = "Test Page",
            Url = "/test-page/",
            Content = "<h1>Hello World</h1>",
            Summary = "A test summary",
            PublishDate = new DateTimeOffset(2025, 5, 15, 12, 0, 0, TimeSpan.Zero),
            UpdatedAt = new DateTimeOffset(2025, 5, 16, 13, 30, 0, TimeSpan.Zero),
            Fields = new Dictionary<string, ContentField>
            {
                ["category"] = new("text", "technology"),
                ["rating"] = new("number", 4.5)
            },
            Seo = new SeoModel
            {
                Title = "SEO Page Title",
                DocumentTitle = "SEO Page Title | My Site",
                Description = "SEO description here",
                Canonical = "https://example.com/test-page/",
                Prev = "https://example.com/test-page/prev/",
                Next = "https://example.com/test-page/next/",
                Robots = "index,follow",
                Og = new SeoOpenGraphModel
                {
                    Title = "OG Page Title",
                    Description = "OG description",
                    Url = "https://example.com/test-page/",
                    Image = "https://example.com/og-image.png",
                    Type = "article",
                    SiteName = "My Site OG",
                    Locale = "en_US"
                },
                Twitter = new SeoTwitterModel
                {
                    Card = "summary_large_image",
                    Title = "Twitter Page Title",
                    Description = "Twitter description",
                    Image = "https://example.com/twitter-image.png",
                    Site = "@mysite",
                    Creator = "@author"
                },
                Article = new SeoArticleModel
                {
                    PublishedTime = new DateTimeOffset(2025, 5, 15, 10, 0, 0, TimeSpan.Zero),
                    ModifiedTime = new DateTimeOffset(2025, 5, 15, 11, 30, 0, TimeSpan.Zero),
                    Author = "John Doe",
                    AuthorType = "Organization",
                    Tags = new[] { "tech", "dotnet", "bukit" }
                },
                Alternates = new[]
                {
                    new SeoAlternateModel("en", "https://example.com/en/test-page/"),
                    new SeoAlternateModel("zh-CN", "https://example.com/zh-CN/test-page/")
                },
                JsonLd = new[]
                {
                    "{\"@type\":\"Article\"}",
                    "{\"@type\":\"WebPage\"}"
                }
            }
        };
    }

    private static PageInfo CreateMinimalPage()
    {
        return new PageInfo
        {
            Title = "Minimal Page",
            Url = "/minimal/",
            Content = "<p>minimal</p>"
        };
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsSiteCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);

        Assert.Equal("my-site", site["name"]);
        Assert.Equal("My Site", site["title"]);
        Assert.Equal("https://example.com", site["url"]);
        Assert.Equal("A test site", site["description"]);
        Assert.Equal("/en/", site["base_url"]);
        Assert.Equal("en", site["language"]);
        Assert.Equal(2026, site["build_year"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BaseUrlSlash_ReturnsSlash()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);

        Assert.Equal("/", site["base_url"]);
        Assert.Equal("/", site["base_path"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_DoesNotExposeAnalytics()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);
        Assert.False(site.TryGetValue("analytics", out _));
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsCanonicalContentHelpers()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = CreateMinimalPage() with
            {
                ContentRecord = new ContentRecord(
                    new ContentIdentity("page-1", "minimal", "minimal", "page", "published"),
                    new ContentPresentation("Minimal Page", "summary", "<p>minimal</p>", "zh-CN", []),
                    new ContentClassification("page", "page", [], ["docs"]),
                    new ContentOwnership("Ali", "Bukit", null, null),
                    new ContentLifecycle(DateTimeOffset.Parse("2026-06-05T00:00:00Z"), null, null, null),
                    new ProvenanceRecord("markdown", "https://example.com/original", [], [], "synced"),
                    new TrustMetadata(0.9, "approved", []),
                    [new EntityRecord("company", "Bukit")],
                    [],
                    []),
                Entities = [new EntityRecord("company", "Bukit")],
                Provenance = new ProvenanceRecord("markdown", "https://example.com/original", [], [], "synced"),
                Trust = new TrustMetadata(0.9, "approved", []),
                Representations = ["html", "json"]
            }
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var content = Assert.IsType<ScriptObject>(page["content_model"]);
        Assert.Same(content, page["content_record"]);
        var provenance = Assert.IsType<ScriptObject>(page["provenance"]);
        var trust = Assert.IsType<ScriptObject>(page["trust"]);
        var entities = Assert.IsType<ScriptArray>(page["entities"]);

        Assert.Equal("page", content["content_type"]);
        Assert.Equal("markdown", provenance["source"]);
        Assert.Equal("approved", trust["review_status"]);
        Assert.Equal("Bukit", Assert.IsType<ScriptObject>(entities[0])["name"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsParamsCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);
        var parms = Assert.IsType<ScriptObject>(site["params"]);

        Assert.Equal("#ff0000", parms["theme_color"]);
        Assert.True((bool)parms["enable_comments"]!);
    }

    [Fact]
    public void ToScriptObject_PageModel_NoParamsWhenNull()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);

        Assert.False(site.TryGetValue("params", out var _));
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsModulesCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);
        var modules = Assert.IsType<ScriptObject>(site["modules"]);

        var headerModules = Assert.IsType<ScriptArray>(modules["header"]);
        Assert.Single(headerModules);
        var navModule = Assert.IsType<ScriptObject>(headerModules[0]);
        Assert.Equal("nav-main", navModule["id"]);
        Assert.Equal("Main Navigation", navModule["title"]);
        Assert.Equal("nav-main", navModule["slug"]);
        Assert.Equal("<nav>links</nav>", navModule["content"]);

        var navFields = Assert.IsType<ScriptObject>(navModule["fields"]);
        var positionField = Assert.IsType<ScriptObject>(navFields["position"]);
        Assert.Equal("text", positionField["type"]);
        Assert.Equal("top", positionField["value"]);

        var footerModules = Assert.IsType<ScriptArray>(modules["footer"]);
        Assert.Single(footerModules);
        var footerMod = Assert.IsType<ScriptObject>(footerModules[0]);
        Assert.Equal("footer-copyright", footerMod["id"]);
        Assert.Equal("Copyright", footerMod["title"]);
        Assert.Equal("footer-copyright", footerMod["slug"]);
        Assert.Equal("<p>&copy; 2025</p>", footerMod["content"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_NoModulesWhenEmpty()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);

        Assert.False(site.TryGetValue("modules", out var _));
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsDataCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);
        var data = Assert.IsType<ScriptObject>(site["data"]);

        var socialLinks = Assert.IsType<ScriptObject>(data["social_links"]);
        Assert.Equal("https://github.com/example", socialLinks["github"]);
        Assert.Equal("https://twitter.com/example", socialLinks["twitter"]);

        Assert.Equal(2, data["version"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_NoDataWhenEmpty()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);

        Assert.False(site.TryGetValue("data", out var _));
        Assert.False(site.TryGetValue("data_index", out var _));
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsDataIndexCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);
        var dataIndex = Assert.IsType<ScriptObject>(site["data_index"]);
        var settings = Assert.IsType<ScriptObject>(dataIndex["settings"]);
        var contact = Assert.IsType<ScriptObject>(settings["contact"]);

        Assert.Equal("contact@example.com", contact["email"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsPageCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);

        Assert.Equal("Test Page", page["title"]);
        Assert.Equal("/test-page/", page["url"]);
        Assert.Equal("<h1>Hello World</h1>", page["content"]);
        Assert.Equal("A test summary", page["summary"]);
        Assert.Equal(new DateTime(2025, 5, 15, 12, 0, 0, DateTimeKind.Utc), page["publish_date"]);
        Assert.Equal(new DateTime(2025, 5, 16, 13, 30, 0, DateTimeKind.Utc), page["updated_at"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_PagePublishDateNull()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);

        Assert.Null(page["publish_date"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsPageFieldsCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var fields = Assert.IsType<ScriptObject>(page["fields"]);

        var category = Assert.IsType<ScriptObject>(fields["category"]);
        Assert.Equal("text", category["type"]);
        Assert.Equal("technology", category["value"]);

        var rating = Assert.IsType<ScriptObject>(fields["rating"]);
        Assert.Equal("number", rating["type"]);
        Assert.Equal(4.5, rating["value"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_NoSeoWhenNull()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateMinimalPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);

        Assert.False(page.TryGetValue("seo", out var _));
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsSeoCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);

        Assert.Equal("SEO Page Title", seo["title"]);
        Assert.Equal("SEO Page Title | My Site", seo["document_title"]);
        Assert.Equal("SEO description here", seo["description"]);
        Assert.Equal("https://example.com/test-page/", seo["canonical"]);
        Assert.Equal("https://example.com/test-page/prev/", seo["prev"]);
        Assert.Equal("https://example.com/test-page/next/", seo["next"]);
        Assert.Equal("index,follow", seo["robots"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsSeoOgCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var og = Assert.IsType<ScriptObject>(seo["og"]);

        Assert.Equal("OG Page Title", og["title"]);
        Assert.Equal("OG description", og["description"]);
        Assert.Equal("https://example.com/test-page/", og["url"]);
        Assert.Equal("https://example.com/og-image.png", og["image"]);
        Assert.Equal("article", og["type"]);
        Assert.Equal("My Site OG", og["site_name"]);
        Assert.Equal("en_US", og["locale"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsSeoTwitterCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var twitter = Assert.IsType<ScriptObject>(seo["twitter"]);

        Assert.Equal("summary_large_image", twitter["card"]);
        Assert.Equal("Twitter Page Title", twitter["title"]);
        Assert.Equal("Twitter description", twitter["description"]);
        Assert.Equal("https://example.com/twitter-image.png", twitter["image"]);
        Assert.Equal("@mysite", twitter["site"]);
        Assert.Equal("@author", twitter["creator"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsSeoArticleCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var article = Assert.IsType<ScriptObject>(seo["article"]);

        Assert.Equal("2025-05-15T10:00:00.0000000+00:00", article["published_time"]);
        Assert.Equal("2025-05-15T11:30:00.0000000+00:00", article["modified_time"]);
        Assert.Equal("John Doe", article["author"]);
        Assert.Equal("Organization", article["author_type"]);

        var tags = Assert.IsType<ScriptArray>(article["tags"]);
        Assert.Equal(3, tags.Count);
        Assert.Equal("tech", tags[0]);
        Assert.Equal("dotnet", tags[1]);
        Assert.Equal("bukit", tags[2]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsSeoAlternatesCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var alternates = Assert.IsType<ScriptArray>(seo["alternates"]);

        Assert.Equal(2, alternates.Count);

        var enAlt = Assert.IsType<ScriptObject>(alternates[0]);
        Assert.Equal("en", enAlt["hreflang"]);
        Assert.Equal("https://example.com/en/test-page/", enAlt["href"]);

        var zhAlt = Assert.IsType<ScriptObject>(alternates[1]);
        Assert.Equal("zh-CN", zhAlt["hreflang"]);
        Assert.Equal("https://example.com/zh-CN/test-page/", zhAlt["href"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_BindsSeoJsonLdCorrectly()
    {
        var model = new PageModel
        {
            Site = CreateFullSite(),
            Page = CreateFullPage()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var jsonLd = Assert.IsType<ScriptArray>(seo["json_ld"]);

        Assert.Equal(2, jsonLd.Count);
        Assert.Equal("{\"@type\":\"Article\"}", jsonLd[0]);
        Assert.Equal("{\"@type\":\"WebPage\"}", jsonLd[1]);
    }

    [Fact]
    public void ToScriptObject_PageModel_SeoOgDefaults()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = new PageInfo
            {
                Title = "P",
                Url = "/p/",
                Content = "",
                Seo = new SeoModel
                {
                    Title = "SEO",
                    Canonical = "/p/"
                }
            }
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var og = Assert.IsType<ScriptObject>(seo["og"]);

        Assert.Equal("website", og["type"]);
        Assert.Null(og["title"]);
        Assert.Null(og["description"]);
        Assert.Null(og["image"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_SeoTwitterDefaults()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = new PageInfo
            {
                Title = "P",
                Url = "/p/",
                Content = "",
                Seo = new SeoModel
                {
                    Title = "SEO",
                    Canonical = "/p/"
                }
            }
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var twitter = Assert.IsType<ScriptObject>(seo["twitter"]);

        Assert.Equal("summary", twitter["card"]);
        Assert.Null(twitter["title"]);
        Assert.Null(twitter["description"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_SeoArticleDefaults()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = new PageInfo
            {
                Title = "P",
                Url = "/p/",
                Content = "",
                Seo = new SeoModel
                {
                    Title = "SEO",
                    Canonical = "/p/"
                }
            }
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);
        var seo = Assert.IsType<ScriptObject>(page["seo"]);
        var article = Assert.IsType<ScriptObject>(seo["article"]);

        Assert.Null(article["published_time"]);
        Assert.Null(article["modified_time"]);
        Assert.Null(article["author"]);
        Assert.Null(article["author_type"]);

        var tags = Assert.IsType<ScriptArray>(article["tags"]);
        Assert.Empty(tags);
    }

    [Fact]
    public void ToScriptObject_ListPageModel_PageNull_CreatesDefaultPage()
    {
        var model = new ListPageModel
        {
            Site = CreateFullSite(),
            Page = null,
            Pages = Array.Empty<PageInfo>()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);

        Assert.Equal("My Site", page["title"]);
        Assert.Equal("/", page["url"]);
        Assert.Equal("", page["content"]);
    }

    [Fact]
    public void ToScriptObject_ListPageModel_PageSet_UsesProvidedPage()
    {
        var model = new ListPageModel
        {
            Site = CreateFullSite(),
            Page = new PageInfo
            {
                Title = "List Page",
                Url = "/list/",
                Content = "<p>list content</p>"
            },
            Pages = Array.Empty<PageInfo>()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);

        Assert.Equal("List Page", page["title"]);
        Assert.Equal("/list/", page["url"]);
        Assert.Equal("<p>list content</p>", page["content"]);
    }

    [Fact]
    public void ToScriptObject_ListPageModel_EmptyPages_ReturnsEmptyArray()
    {
        var model = new ListPageModel
        {
            Site = CreateMinimalSite(),
            Pages = Array.Empty<PageInfo>()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var pages = Assert.IsType<ScriptArray>(obj["pages"]);

        Assert.Empty(pages);
    }

    [Fact]
    public void ToScriptObject_ListPageModel_BindsPagesArrayCorrectly()
    {
        var dt = new DateTimeOffset(2025, 6, 1, 8, 0, 0, TimeSpan.FromHours(8));
        var model = new ListPageModel
        {
            Site = CreateFullSite(),
            Page = null,
            Pages = new[]
            {
                new PageInfo
                {
                    Title = "Page A",
                    Url = "/a/",
                    Content = "<p>A</p>",
                    Summary = "Summary A",
                    PublishDate = dt,
                    Seo = new SeoModel
                    {
                        Title = "SEO A",
                        Canonical = "/a/"
                    }
                },
                new PageInfo
                {
                    Title = "Page B",
                    Url = "/b/",
                    Content = "<p>B</p>",
                    Summary = "Summary B",
                    PublishDate = null,
                    Seo = null
                },
                new PageInfo
                {
                    Title = "Page C",
                    Url = "/c/",
                    Content = "<p>C</p>"
                }
            }
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var pages = Assert.IsType<ScriptArray>(obj["pages"]);

        Assert.Equal(3, pages.Count);

        var pageA = Assert.IsType<ScriptObject>(pages[0]);
        Assert.Equal("Page A", pageA["title"]);
        Assert.Equal("/a/", pageA["url"]);
        Assert.Equal("<p>A</p>", pageA["content"]);
        Assert.Equal("Summary A", pageA["summary"]);
        Assert.Equal(new DateTime(2025, 6, 1, 8, 0, 0), pageA["publish_date"]);
        var seoA = Assert.IsType<ScriptObject>(pageA["seo"]);
        Assert.Equal("SEO A", seoA["title"]);
        Assert.Equal("/a/", seoA["canonical"]);

        var pageB = Assert.IsType<ScriptObject>(pages[1]);
        Assert.Equal("Page B", pageB["title"]);
        Assert.Null(pageB["publish_date"]);
        Assert.False(pageB.TryGetValue("seo", out var _));

        var pageC = Assert.IsType<ScriptObject>(pages[2]);
        Assert.Equal("Page C", pageC["title"]);
        Assert.Null(pageC["summary"]);
        Assert.Null(pageC["publish_date"]);
        Assert.False(pageC.TryGetValue("seo", out var _));
    }

    [Fact]
    public void ToScriptObject_ListPageModel_ExposesStableListFields()
    {
        var seo = new SeoModel
        {
            Title = "Market list",
            Canonical = "https://example.com/market/"
        };
        var first = new PageInfo { Title = "A", Url = "/a/", Content = "" };
        var second = new PageInfo { Title = "B", Url = "/b/", Content = "" };
        var model = new ListPageModel
        {
            Site = CreateFullSite(),
            Page = new PageInfo
            {
                Title = "Market",
                Url = "/market/",
                Content = "",
                Seo = seo
            },
            Pages = new[] { first, second },
            Items = new[] { first, second },
            Pagination = new ListPaginationModel
            {
                Page = 2,
                PageSize = 10,
                TotalPages = 3,
                TotalItems = 25,
                HasPrev = true,
                HasNext = true,
                PrevUrl = "/market/",
                NextUrl = "/market/page/3/"
            },
            Collection = new ListCollectionModel { Key = "posts" },
            Taxonomy = new ListTaxonomyModel
            {
                Kind = "category",
                Term = "市场观察",
                Slug = "market",
                RoutePrefix = "/insights/category",
                Url = "/insights/category/market/",
                IsIndex = false
            },
            Filter = new ListFilterModel
            {
                Field = "category",
                Operator = "in",
                Value = "市场观察",
                Values = new[] { "市场观察", "政策动态" }
            },
            Seo = seo
        };

        var obj = ScribanModelBinder.ToScriptObject(model);

        Assert.Equal(2, Assert.IsType<ScriptArray>(obj["pages"]).Count);
        Assert.Equal(2, Assert.IsType<ScriptArray>(obj["items"]).Count);

        var pagination = Assert.IsType<ScriptObject>(obj["pagination"]);
        Assert.Equal(2, pagination["page"]);
        Assert.Equal(10, pagination["page_size"]);
        Assert.Equal(3, pagination["total_pages"]);
        Assert.Equal(25, pagination["total_items"]);
        Assert.Equal(25, pagination["total"]);
        Assert.Equal(true, pagination["has_prev"]);
        Assert.Equal(true, pagination["has_next"]);
        Assert.Equal("/market/", pagination["prev_url"]);
        Assert.Equal("/market/page/3/", pagination["next_url"]);

        var collection = Assert.IsType<ScriptObject>(obj["collection"]);
        Assert.Equal("posts", collection["key"]);

        var taxonomy = Assert.IsType<ScriptObject>(obj["taxonomy"]);
        Assert.Equal("category", taxonomy["kind"]);
        Assert.Equal("市场观察", taxonomy["term"]);
        Assert.Equal("market", taxonomy["slug"]);
        Assert.Equal("/insights/category", taxonomy["route_prefix"]);
        Assert.Equal("/insights/category", taxonomy["routePrefix"]);
        Assert.Equal("/insights/category/market/", taxonomy["url"]);
        Assert.Equal(false, taxonomy["is_index"]);

        var filter = Assert.IsType<ScriptObject>(obj["filter"]);
        Assert.Equal("category", filter["field"]);
        Assert.Equal("in", filter["operator"]);
        Assert.Equal("市场观察", filter["value"]);
        var filterValues = Assert.IsType<ScriptArray>(filter["values"]);
        Assert.Equal(new[] { "市场观察", "政策动态" }, filterValues.Cast<string>().ToArray());

        var seoObject = Assert.IsType<ScriptObject>(obj["seo"]);
        Assert.Equal("Market list", seoObject["title"]);
        Assert.Equal("https://example.com/market/", seoObject["canonical"]);
    }

    [Fact]
    public void ToScriptObject_PageModel_DerivedListAliasesExposeStableFields()
    {
        var model = new PageModel
        {
            Site = CreateMinimalSite(),
            Page = new PageInfo
            {
                Title = "Taxonomy",
                Url = "/category/market/",
                Content = "",
                Seo = new SeoModel
                {
                    Title = "Taxonomy SEO",
                    Canonical = "https://example.com/category/market/"
                },
                Fields = new Dictionary<string, ContentField>(StringComparer.OrdinalIgnoreCase)
                {
                    ["type"] = new("text", "derived"),
                    ["collection"] = new("text", "posts"),
                    ["items"] = new("list", new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["title"] = "A",
                            ["url"] = "/a/"
                        }
                    }),
                    ["pagination"] = new("object", new Dictionary<string, object?>
                    {
                        ["page"] = 1,
                        ["page_size"] = 10,
                        ["total"] = 1,
                        ["total_items"] = 1,
                        ["total_pages"] = 1
                    }),
                    ["taxonomy"] = new("object", new Dictionary<string, object?>
                    {
                        ["kind"] = "category",
                        ["term"] = "Market",
                        ["slug"] = "market"
                    })
                }
            }
        };

        var obj = ScribanModelBinder.ToScriptObject(model);

        Assert.Single(Assert.IsType<ScriptArray>(obj["items"]));
        Assert.Single(Assert.IsType<ScriptArray>(obj["pages"]));
        var pagination = Assert.IsType<ScriptObject>(obj["pagination"]);
        Assert.Equal(1, pagination["total_items"]);
        var collection = Assert.IsType<ScriptObject>(obj["collection"]);
        Assert.Equal("posts", collection["key"]);
        var taxonomy = Assert.IsType<ScriptObject>(obj["taxonomy"]);
        Assert.Equal("category", taxonomy["kind"]);
        var seo = Assert.IsType<ScriptObject>(obj["seo"]);
        Assert.Equal("Taxonomy SEO", seo["title"]);
    }

    [Fact]
    public void ToScriptObject_ListPageModel_SiteIsBoundCorrectly()
    {
        var model = new ListPageModel
        {
            Site = CreateFullSite(),
            Page = null,
            Pages = Array.Empty<PageInfo>()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var site = Assert.IsType<ScriptObject>(obj["site"]);

        Assert.Equal("my-site", site["name"]);
        Assert.Equal("My Site", site["title"]);
        Assert.Equal("https://example.com", site["url"]);
        Assert.Equal("en", site["language"]);
        Assert.Equal(2026, site["build_year"]);
    }

    [Fact]
    public void ToScriptObject_ListPageModel_PageNull_UsesSiteTitleForDefaultPage()
    {
        var site = new SiteModel
        {
            Name = "other",
            Title = "Other Site Title",
            BaseUrl = "/",
            Language = "en"
        };
        var model = new ListPageModel
        {
            Site = site,
            Page = null,
            Pages = Array.Empty<PageInfo>()
        };

        var obj = ScribanModelBinder.ToScriptObject(model);
        var page = Assert.IsType<ScriptObject>(obj["page"]);

        Assert.Equal("Other Site Title", page["title"]);
    }
}
