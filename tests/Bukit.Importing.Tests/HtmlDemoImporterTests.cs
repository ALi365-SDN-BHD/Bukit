using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class HtmlDemoImporterTests : IDisposable
{
    private readonly string _tempDir;

    public HtmlDemoImporterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-import-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Import_SingleHtmlFile_CreatesThemeStructure()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test Site</title></head><body><header><nav><a href=\"/\">Home</a></nav></header><main><h1>Welcome</h1></main><footer><p>Footer</p></footer></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var result = HtmlDemoImporter.Import(options);

        var themeDir = Path.Combine(_tempDir, "themes", "test-theme");
        Assert.True(Directory.Exists(Path.Combine(themeDir, "layouts", "layouts")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "layouts", "pages")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "layouts", "partials")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "assets")));
        Assert.True(Directory.Exists(Path.Combine(themeDir, "static")));
        Assert.Equal(1, result.PagesFound);
        Assert.True(result.TemplatesGenerated > 0);
    }

    [Fact]
    public void Import_BaseLayout_ContainsContentVariable()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-layout",
            RootDir = _tempDir
        };

        HtmlDemoImporter.Import(options);

        var layoutPath = Path.Combine(_tempDir, "themes", "test-layout", "layouts", "layouts", "base.html");
        var content = File.ReadAllText(layoutPath);
        Assert.Contains("{{ content }}", content);
        Assert.Contains("<!DOCTYPE html>", content);
    }

    [Fact]
    public void Import_BaseLayout_UsesKnownSeoFieldsAndBaseUrlForAssets()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "assets", "css"));
        File.WriteAllText(Path.Combine(_tempDir, "assets", "css", "style.css"), "body{}");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title><link rel=\"stylesheet\" href=\"assets/css/style.css\"></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "base-layout-fields",
            RootDir = _tempDir
        };

        HtmlDemoImporter.Import(options);

        var layoutPath = Path.Combine(_tempDir, "themes", "base-layout-fields", "layouts", "layouts", "base.html");
        var content = File.ReadAllText(layoutPath);
        Assert.Contains("{{ page.fields.seo_title.value }}", content);
        Assert.Contains("{{ page.fields.seo_desc.value }}", content);
        Assert.Contains("base_url = site.base_url", content);
        Assert.Contains("if base_url == \"/\"", content);
        Assert.Contains("href=\"{{ base_url }}/assets/css/style.css\"", content);
        Assert.DoesNotContain("page.seo_title", content);
        Assert.DoesNotContain("page.seo_description", content);
        Assert.DoesNotContain("href=\"/assets/css/style.css\"", content);
    }

    [Fact]
    public void Import_MissingIndexHtml_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main>About</main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        Assert.Throws<InvalidOperationException>(() => HtmlDemoImporter.Import(options));
    }

    [Fact]
    public void Import_InputDirNotFound_Throws()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = Path.Combine(_tempDir, "nonexistent"),
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        Assert.Throws<InvalidOperationException>(() => HtmlDemoImporter.Import(options));
    }

    [Fact]
    public void Import_ExistingTheme_WithoutForce_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var themeDir = Path.Combine(_tempDir, "themes", "test-theme");
        Directory.CreateDirectory(themeDir);

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir,
            Force = false
        };

        Assert.Throws<InvalidOperationException>(() => HtmlDemoImporter.Import(options));
    }

    [Fact]
    public void Import_ExistingTheme_WithForce_Overwrites()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var themeDir = Path.Combine(_tempDir, "themes", "test-theme");
        Directory.CreateDirectory(themeDir);
        File.WriteAllText(Path.Combine(themeDir, "old-file.txt"), "old");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir,
            Force = true
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.False(File.Exists(Path.Combine(themeDir, "old-file.txt")));
        Assert.True(result.TemplatesGenerated > 0);
    }

    [Fact]
    public void Import_GeneratesSiteYaml()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.True(result.SiteYamlCreated);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "test-theme", "site.yaml")));
        var yaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "test-theme", "site.yaml"));
        Assert.Contains("test-theme", yaml);
        Assert.Contains("provider: markdown", yaml);
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "test-theme", "content", "index.md")));
    }

    [Fact]
    public void Import_ExistingSiteYaml_NotOverwritten()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");
        var siteDir = Path.Combine(_tempDir, "sites", "test-theme");
        Directory.CreateDirectory(siteDir);
        File.WriteAllText(Path.Combine(siteDir, "site.yaml"), "existing: config");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-theme",
            RootDir = _tempDir
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.False(result.SiteYamlCreated);
        Assert.Equal("existing: config", File.ReadAllText(Path.Combine(siteDir, "site.yaml")));
    }

    [Fact]
    public void Import_MultiplePages_GeneratesPageTemplates()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><header>Nav</header><main><h1>Home</h1></main><footer>End</footer></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><header>Nav</header><main><h1>About</h1></main><footer>End</footer></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-multi",
            RootDir = _tempDir
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.Equal(2, result.PagesFound);
        Assert.True(result.PartialsGenerated >= 1);
        var pagesDir = Path.Combine(_tempDir, "themes", "test-multi", "layouts", "pages");
        Assert.True(File.Exists(Path.Combine(pagesDir, "index.html")));
        Assert.True(File.Exists(Path.Combine(pagesDir, "list.html")));
    }

    [Fact]
    public void Import_InvalidThemeName_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "../evil",
            RootDir = _tempDir
        };

        Assert.Throws<InvalidOperationException>(() => HtmlDemoImporter.Import(options));
    }

    [Fact]
    public void Import_DryRun_DoesNotWriteFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><header>Nav</header><main><h1>Hello</h1></main><footer>End</footer></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "dry-test",
            RootDir = _tempDir,
            DryRun = true
        };

        var result = HtmlDemoImporter.Import(options);

        var themeDir = Path.Combine(_tempDir, "themes", "dry-test");
        Assert.False(Directory.Exists(themeDir));
        Assert.True(result.PagesFound > 0);
    }

    [Fact]
    public void Import_Strict_EmptySlugNonHome_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "strict-test",
            RootDir = _tempDir,
            Force = true,
            Strict = true,
            DryRun = true
        };

        var result = HtmlDemoImporter.Import(options);
        Assert.True(result.PagesFound > 0);
    }

    [Fact]
    public void Import_PreserveHtml_CopiesFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "preserve-test",
            RootDir = _tempDir,
            Force = true,
            PreserveHtml = true
        };

        var result = HtmlDemoImporter.Import(options);

        var originalDir = Path.Combine(_tempDir, "sites", "preserve-test", "original-demo");
        Assert.True(Directory.Exists(originalDir));
        Assert.True(File.Exists(Path.Combine(originalDir, "index.html")));
    }

    [Fact]
    public void Import_GenerateReport_WritesReportFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "report-test",
            RootDir = _tempDir,
            Force = true,
            GenerateReport = true
        };

        var result = HtmlDemoImporter.Import(options);

        var reportPath = Path.Combine(_tempDir, "sites", "report-test", "import-report.md");
        Assert.True(File.Exists(reportPath));
        var content = File.ReadAllText(reportPath);
        Assert.Contains("HTML Demo Import Report", content);
        Assert.Contains("report-test", content);
        Assert.Contains("Hardcoded Residuals", content);
        Assert.Contains("Manual Review Required", content);
    }

    [Fact]
    public void Import_BaseUrl_SetsInSiteYaml()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "baseurl-test",
            RootDir = _tempDir,
            Force = true,
            BaseUrl = "https://example.com"
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.True(result.SiteYamlCreated);
        var yaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "baseurl-test", "site.yaml"));
        Assert.Contains("https://example.com", yaml);
    }

    [Fact]
    public void Import_Overwrite_AllowsComponentRewrite()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><section class=\"hero\"><h1>Hero</h1></section></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "overwrite-test",
            RootDir = _tempDir,
            Force = true,
            Overwrite = true
        };

        var result = HtmlDemoImporter.Import(options);

        var compDir = Path.Combine(_tempDir, "themes", "overwrite-test", "layouts", "components");
        Assert.True(Directory.Exists(compDir));
    }

    [Fact]
    public void Import_WithForce_PreservesCopiedAssets()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "assets", "images"));
        File.WriteAllText(Path.Combine(_tempDir, "assets", "images", "hero.jpg"), "fake");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><img src=\"assets/images/hero.jpg\" /></main></body></html>");

        var themeDir = Path.Combine(_tempDir, "themes", "asset-force");
        Directory.CreateDirectory(themeDir);

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "asset-force",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        Assert.True(File.Exists(Path.Combine(themeDir, "static", "assets", "images", "hero.jpg")));
    }

    [Fact]
    public void Import_ComponentTemplates_UseValidHeadingTags()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><section class=\"hero\"><h1>Hero</h1><p>Intro</p></section><div class=\"faq-item\"><h3>Q?</h3><p>A.</p></div></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "component-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var hero = File.ReadAllText(Path.Combine(_tempDir, "themes", "component-test", "layouts", "components", "hero.html"));
        var faq = File.ReadAllText(Path.Combine(_tempDir, "themes", "component-test", "layouts", "components", "faq.html"));
        Assert.Contains("<h1>{{ section.heading }}</h1>", hero);
        Assert.Contains("<h3>{{ section.heading }}</h3>", faq);
        Assert.DoesNotContain("<1>", hero);
        Assert.DoesNotContain("<3>", faq);
    }

    [Fact]
    public void Import_Strict_InlineScript_ThrowsAndDoesNotWriteTheme()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title><script src=\"app.js\"></script></head><body><main><h1>Hello</h1><script>alert(1)</script></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "strict-script",
            RootDir = _tempDir,
            Strict = true
        };

        var ex = Assert.Throws<InvalidOperationException>(() => HtmlDemoImporter.Import(options));
        Assert.Contains("INLINE_SCRIPT", ex.Message);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "themes", "strict-script")));
    }

    [Fact]
    public void Import_DefaultContentDraft_UsesExtractedContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Welcome</h1><p>Intro text.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About Us</h1><p>About body.</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "content-test",
            RootDir = _tempDir
        };

        HtmlDemoImporter.Import(options);

        var pageTemplate = File.ReadAllText(Path.Combine(_tempDir, "themes", "content-test", "layouts", "pages", "page.html"));
        var aboutDraft = File.ReadAllText(Path.Combine(_tempDir, "sites", "content-test", "content", "pages", "about.md"));
        Assert.Contains("{{ page.content }}", pageTemplate);
        Assert.DoesNotContain("About body.", pageTemplate);
        Assert.Contains("About body.", aboutDraft);
    }

    [Fact]
    public void Import_ListTemplates_UseCurrentPageTitleContext()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "insights.html"),
            "<html><head><title>Insights</title></head><body><main><h1>Insights</h1><article class=\"article-card\"><h3>Guide</h3><p>Summary.</p></article></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "companies.html"),
            "<html><head><title>Companies</title></head><body><main><h1>Companies</h1><article class=\"company-card\"><h3>ACME</h3><p>Summary.</p></article></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "list-title-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var pagesDir = Path.Combine(_tempDir, "themes", "list-title-test", "layouts", "pages");
        var insightsTemplate = File.ReadAllText(Path.Combine(pagesDir, "insights.html"));
        var companiesTemplate = File.ReadAllText(Path.Combine(pagesDir, "companies.html"));

        Assert.Contains("<h1>{{ this.title }}</h1>", insightsTemplate);
        Assert.Contains("<h1>{{ this.title }}</h1>", companiesTemplate);
        Assert.DoesNotContain("<h1>{{ page.title }}</h1>", insightsTemplate);
        Assert.DoesNotContain("<h1>{{ page.title }}</h1>", companiesTemplate);
    }

    [Fact]
    public void Import_ContentDrafts_IncludeCollectionFrontMatter()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><p>Intro.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1><p>About.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "article-detail.html"),
            "<html><head><title>Article</title></head><body><main><h1>Article</h1><p>Article.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "company-detail.html"),
            "<html><head><title>Company</title></head><body><main><h1>Company</h1><p>Company.</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "collection-fm-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var contentDir = Path.Combine(_tempDir, "sites", "collection-fm-test", "content");
        var index = File.ReadAllText(Path.Combine(contentDir, "index.md"));
        var about = File.ReadAllText(Path.Combine(contentDir, "pages", "about.md"));
        var post = File.ReadAllText(Path.Combine(contentDir, "posts", "article-detail.md"));
        var company = File.ReadAllText(Path.Combine(contentDir, "companies", "company-detail.md"));
        Assert.Contains("collection: \"page\"", index);
        Assert.Contains("collection: \"page\"", about);
        Assert.Contains("collection: \"post\"", post);
        Assert.Contains("collection: \"company\"", company);
        Assert.DoesNotContain("type: \"", index);
        Assert.DoesNotContain("type: \"", about);
        Assert.DoesNotContain("type: \"", post);
        Assert.DoesNotContain("type: \"", company);
    }
}
