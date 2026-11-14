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
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
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
        Assert.Contains("{{ page.fields.seo_title.value | html.escape }}", content);
        Assert.Contains("{{ page.fields.seo_desc.value | html.escape }}", content);
        Assert.Contains("base_url = site.base_url", content);
        Assert.Contains("if base_url == \"/\"", content);
        Assert.DoesNotContain("style.css", content);
        Assert.DoesNotContain("page.seo_title", content);
        Assert.DoesNotContain("page.seo_description", content);

        // The stylesheet belongs to the home page, so its own template loads it.
        var home = File.ReadAllText(Path.Combine(_tempDir, "themes", "base-layout-fields", "layouts", "pages", "index.html"));
        Assert.Contains("base_url = site.base_url", home);
        Assert.Contains("href=\"{{ base_url }}/assets/css/style.css\"", home);
        Assert.DoesNotContain("href=\"/assets/css/style.css\"", home);
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

        Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
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

        Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
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

        Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
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
        Assert.Contains("url: https://example.com", yaml);
        Assert.Contains("sources:", yaml);
        Assert.Contains("type: markdown", yaml);
        Assert.DoesNotContain("provider: markdown", yaml);
        // Core renders the home page from pages/index.html; a home draft would duplicate it at /index/.
        Assert.False(File.Exists(Path.Combine(_tempDir, "sites", "test-theme", "content", "index.md")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "sites", "test-theme", "content", "pages")));
        Assert.Contains("dir: sites/test-theme/content/pages", yaml);
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

        Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
    }

    [Theory]
    [InlineData(".env.local")]
    [InlineData("id_rsa")]
    public void Import_SensitiveNamePattern_Throws(string fileName)
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, fileName), "SECRET=xxx");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "sensitive-pattern-test",
            RootDir = _tempDir,
            Force = true
        };

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
        Assert.Contains("sensitive", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_DryRun_DoesNotWriteFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Test</title></head><body>
            <header>Nav</header>
            <main>
              <section class="hero"><h1>Hello</h1><p>Intro text.</p></section>
              <section class="cards"><article class="article-card"><h3>Guide</h3><p>Summary.</p></article></section>
              <section class="faq"><div class="faq-item"><h3>Question?</h3><p>Answer.</p></div></section>
            </main>
            <footer>End</footer>
            </body></html>
            """);

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
        Assert.True(result.ComponentsGenerated >= 1);
        Assert.True(result.RecordsExtracted >= 3);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "sites", "dry-test")));
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
            StrictMode = "fail",
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
        Directory.CreateDirectory(Path.Combine(_tempDir, "assets", "css"));
        File.WriteAllText(Path.Combine(_tempDir, "assets", "css", "site.css"), "body{}");

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
        Assert.True(File.Exists(Path.Combine(originalDir, "assets", "css", "site.css")));
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
        Assert.Contains("Hardcoded Content Residue", content);
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
            "<html><head><title>Test</title></head><body><main><article class=\"article-card\"><h3>Item</h3></article></main></body></html>");

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
    public void Import_HtmlLinks_AreNotCopiedAsStaticAssets()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><a href=\"about.html\">About</a></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "html-link-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "html-link-test", "static", "about.html")));
    }

    [Fact]
    public void Import_ComponentTemplates_UseValidHeadingTags()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            "<html><head><title>News</title></head><body><main><h1>News</h1>" +
            "<article class=\"article-card\"><h3>Post</h3><p>Summary.</p></article></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "component-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var card = File.ReadAllText(Path.Combine(_tempDir, "themes", "component-test", "layouts", "components", "article-card.html"));
        Assert.Contains("<h3>{{ item.title }}</h3>", card);
        Assert.DoesNotContain("<3>", card);
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "component-test", "layouts", "components", "hero.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "component-test", "layouts", "components", "faq.html")));
    }

    [Fact]
    public void Import_DoesNotDuplicateNav_WhenHeaderContainsNav()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><header><nav><a href=\"/\">Home</a></nav></header><main><h1>Home</h1></main><footer>End</footer></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "nav-dedupe-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var layout = File.ReadAllText(Path.Combine(_tempDir, "themes", "nav-dedupe-test", "layouts", "layouts", "base.html"));
        var header = File.ReadAllText(Path.Combine(_tempDir, "themes", "nav-dedupe-test", "layouts", "partials", "header.html"));
        var navigationSeed = File.ReadAllText(Path.Combine(_tempDir, "sites", "nav-dedupe-test", "notion-seed", "navigation.json"));
        var databaseMap = File.ReadAllText(Path.Combine(_tempDir, "sites", "nav-dedupe-test", "notion-seed", "notion-database-map.yaml"));
        Assert.Contains("{{ include 'partials/header.html' }}", layout);
        Assert.DoesNotContain("{{ include 'partials/nav.html' }}", layout);
        Assert.Contains("site.modules.navigation", header);
        Assert.Contains("<a href=\"{{ base_url }}/\">Home</a>", header);
        Assert.Contains("\"title\": \"Home\"", navigationSeed);
        Assert.Contains("\"link\": \"/\"", navigationSeed);
        Assert.Contains("navigation:", databaseMap);
        Assert.Contains("seed: navigation.json", databaseMap);
    }

    [Fact]
    public void Import_ExtractsNavigationFromMenuClassWithoutNavTag()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body>
            <header>
              <div class="navbar-menu">
                <a href="/">Home</a>
                <a href="/products.html">Products</a>
                <a href="/contact.html">Contact</a>
              </div>
            </header>
            <main><h1>Home</h1></main><footer>End</footer></body></html>
            """);

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "menu-class-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var layout = File.ReadAllText(Path.Combine(_tempDir, "themes", "menu-class-test", "layouts", "layouts", "base.html"));
        var header = File.ReadAllText(Path.Combine(_tempDir, "themes", "menu-class-test", "layouts", "partials", "header.html"));
        var navigationSeed = File.ReadAllText(Path.Combine(_tempDir, "sites", "menu-class-test", "notion-seed", "navigation.json"));

        Assert.Contains("{{ include 'partials/header.html' }}", layout);
        Assert.DoesNotContain("{{ include 'partials/nav.html' }}", layout);
        Assert.Contains("site.modules.navigation", header);
        Assert.Contains("<div class=\"navbar-menu\">", header);
        Assert.Contains("<a href=\"{{ nav_url }}\">{{ item.title }}</a>", header);
        Assert.Contains("\"title\": \"Products\"", navigationSeed);
        Assert.Contains("\"link\": \"/products.html\"", navigationSeed);
    }

    [Fact]
    public void Import_DynamicMenuShellWithoutStaticAnchors_WarnsUserToProvideAnchors()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body>
            <header>
              <button class="hamburger menu-toggle" aria-label="Open menu"></button>
              <div id="mobile-menu" class="drawer-menu"></div>
              <script>
                document.getElementById('mobile-menu').innerHTML =
                  '<a href="/">Home</a><a href="/contact/">Contact</a>';
              </script>
            </header>
            <main><h1>Home</h1></main><footer>End</footer></body></html>
            """);

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "dynamic-menu-warning-test",
            RootDir = _tempDir,
            Force = true
        };

        var result = HtmlDemoImporter.Import(options);

        var navigationSeed = File.ReadAllText(Path.Combine(_tempDir, "sites", "dynamic-menu-warning-test", "notion-seed", "navigation.json"));
        Assert.Single(result.Warnings, w => w.Contains("不执行 JS 动态生成菜单", StringComparison.Ordinal));
        Assert.DoesNotContain("\"title\":", navigationSeed);
    }

    [Fact]
    public void Import_ForceWithDynamicMenu_ClearsStaleNavigationSeed()
    {
        var demoDir = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demoDir);
        var indexPath = Path.Combine(demoDir, "index.html");
        File.WriteAllText(indexPath,
            """
            <html><head><title>Home</title></head><body>
            <header><nav><a href="/">Home</a><a href="/contact/">Contact</a></nav></header>
            <main><h1>Home</h1></main></body></html>
            """);

        var options = new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = "dynamic-menu-stale-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);
        var navigationPath = Path.Combine(_tempDir, "sites", "dynamic-menu-stale-test", "notion-seed", "navigation.json");
        Assert.Contains("\"title\": \"Home\"", File.ReadAllText(navigationPath));

        File.WriteAllText(indexPath,
            """
            <html><head><title>Home</title></head><body>
            <header>
              <button class="hamburger menu-toggle" aria-label="Open menu"></button>
              <div id="mobile-menu" class="drawer-menu"></div>
              <script>
                document.getElementById('mobile-menu').innerHTML =
                  '<a href="/">Home</a><a href="/contact/">Contact</a>';
              </script>
            </header>
            <main><h1>Home</h1></main></body></html>
            """);

        var result = HtmlDemoImporter.Import(options);
        var navigationSeed = File.ReadAllText(navigationPath);

        Assert.Single(result.Warnings, w => w.Contains("不执行 JS 动态生成菜单", StringComparison.Ordinal));
        Assert.DoesNotContain("\"title\":", navigationSeed);
    }

    [Fact]
    public void Import_PageTemplatesPreserveDemoSectionsAndReferenceComponents()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body>
            <main>
              <section class="hero"><h1>Silk Road</h1><p>Connect markets.</p><a href="contact.html">Talk</a></section>
              <section class="cards"><article class="article-card"><h3>Market Guide</h3><p>Card copy.</p><a href="article.html">Read</a></article></section>
              <section class="faq"><div class="faq-item"><h3>What?</h3><p>Answer.</p></div></section>
            </main>
            </body></html>
            """);

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "structure-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var indexTemplate = File.ReadAllText(Path.Combine(_tempDir, "themes", "structure-test", "layouts", "pages", "index.html"));
        // Section copy stays in the page; only the data-bound card list is a component.
        Assert.Contains("class=\"hero\"", indexTemplate);
        Assert.Contains("Silk Road", indexTemplate);
        Assert.Contains("Connect markets.", indexTemplate);
        Assert.Contains("What?", indexTemplate);
        Assert.DoesNotContain("{{ include 'components/article-card.html' }}", indexTemplate);
        Assert.Contains("Market Guide", indexTemplate);
        Assert.DoesNotContain("{{ include 'components/hero.html' }}", indexTemplate);
        Assert.DoesNotContain("{{ include 'components/faq.html' }}", indexTemplate);

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "structure-test", "layouts", "components", "article-card.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "structure-test", "layouts", "components", "hero.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "structure-test", "layouts", "components", "faq.html")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "themes", "structure-test", "layouts", "components", "card.html")));
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
            StrictMode = "fail"
        };

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "themes", "strict-script")));
    }

    [Fact]
    public void Import_StrictFailure_KeepsDraftsTheManifestWouldOtherwiseDelete()
    {
        var demo = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demo);
        File.WriteAllText(Path.Combine(demo, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><p>Intro.</p></main></body></html>");
        File.WriteAllText(Path.Combine(demo, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1><p>Body.</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = demo,
            ThemeName = "strict-order",
            RootDir = _tempDir
        };

        HtmlDemoImporter.Import(options);
        var staleDraft = Path.Combine(_tempDir, "sites", "strict-order", "content", "pages", "about.md");
        Assert.True(File.Exists(staleDraft));

        // The demo no longer has the About page, and the home page now carries residue.
        File.Delete(Path.Combine(demo, "about.html"));
        File.WriteAllText(Path.Combine(demo, "index.html"),
            "<html><head><title>Home</title></head><body><main><p>High Value Business Proposition For Buyers</p></main></body></html>");

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options with
        {
            Force = true,
            StrictMode = "fail"
        }));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("hardcoded content residue", ex.Message);
        // A failed strict run must not delete files the manifest still owns.
        Assert.True(File.Exists(staleDraft));
    }

    [Fact]
    public void Import_SecondConcurrentImport_IsRejected()
    {
        var demo = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demo);
        File.WriteAllText(Path.Combine(demo, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        var siteDir = Path.Combine(_tempDir, "sites", "locked-import");
        Directory.CreateDirectory(siteDir);
        using var held = new FileStream(
            Path.Combine(siteDir, ".bukit-import.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = demo,
            ThemeName = "locked-import",
            RootDir = _tempDir
        }));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("already running", ex.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "themes", "locked-import")));
    }

    [Fact]
    public void Import_SecondSiteUsingTheSameTheme_IsRejected()
    {
        var demo = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demo);
        File.WriteAllText(Path.Combine(demo, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        var themeDir = Path.Combine(_tempDir, "themes", "shared-theme");
        var themesRoot = Path.GetDirectoryName(themeDir)!;
        Directory.CreateDirectory(themesRoot);
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(themeDir))))[..16].ToLowerInvariant();
        using var held = new FileStream(
            Path.Combine(themesRoot, $".import-{key}.lock"),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None);

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = demo,
            ThemeName = "shared-theme",
            RootDir = _tempDir,
            SitePath = Path.Combine(_tempDir, "sites", "other-site")
        }));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("theme 'shared-theme'", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_PageWithoutMainOrArticle_KeepsBodyContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body>
            <header><a href="index.html">Home</a><a href="about.html">About</a></header>
            <div class="content"><h1>Welcome</h1><p>Body marker.</p></div>
            <footer>Footer marker.</footer>
            </body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            """
            <html><head><title>About</title></head><body>
            <header><a href="index.html">Home</a><a href="about.html">About</a></header>
            <div class="content"><h1>About Us</h1><p>About marker.</p></div>
            <footer>Footer marker.</footer>
            </body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "no-main",
            RootDir = _tempDir,
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "no-main");
        var home = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "index.html"));
        var about = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "about.html"));
        Assert.Contains("Body marker.", home, StringComparison.Ordinal);
        Assert.Contains("{{ page.content }}", about, StringComparison.Ordinal);
        var aboutDraft = File.ReadAllText(Path.Combine(_tempDir, "sites", "no-main", "content", "pages", "about.md"));
        Assert.Contains("About marker.", aboutDraft, StringComparison.Ordinal);
        Assert.Contains("Footer marker.", File.ReadAllText(Path.Combine(themeRoot, "layouts", "partials", "footer.html")));
        Assert.DoesNotContain("Body marker.", File.ReadAllText(Path.Combine(themeRoot, "layouts", "partials", "header.html")));
    }

    [Fact]
    public void Import_MultipleArticlesWithoutMain_KeepsEveryCard()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "companies.html"),
            """
            <html><head><title>Companies</title></head><body>
            <header>Header</header>
            <h1>Companies</h1>
            <article class="company-card"><h3>Alpha</h3></article>
            <article class="company-card"><h3>Beta</h3></article>
            <article class="company-card"><h3>Gamma</h3></article>
            <footer>Footer</footer>
            </body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "multi-article",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var companies = Path.Combine(_tempDir, "sites", "multi-article", "content", "companies");
        var names = Directory.GetFiles(companies, "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["alpha", "beta", "gamma"], names);
    }

    [Fact]
    public void Import_HomeCards_StayStatic()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body><main>
            <section class="services">
              <div class="card"><h3>Design</h3><p>Design copy.</p></div>
              <div class="card"><h3>Build</h3><p>Build copy.</p></div>
            </section>
            <section class="about"><h2>About</h2><p>About copy.</p></section>
            <section class="news">
              <h2>Latest news</h2>
              <div class="card"><h3>Launch</h3><p>Launch copy.</p></div>
            </section>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "home-static",
            RootDir = _tempDir,
            Force = true
        });

        var home = File.ReadAllText(Path.Combine(_tempDir, "themes", "home-static", "layouts", "pages", "index.html"));
        Assert.DoesNotContain("{{ for item in pages }}", home);
        Assert.Contains("Design", home, StringComparison.Ordinal);
        Assert.Contains("Launch", home, StringComparison.Ordinal);
        Assert.Contains("About copy.", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_PostCardList_UsesTheArticleCardComponent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main>
            <h1>News</h1>
            <article class="post-card"><h3>First</h3><p>One.</p></article>
            <article class="post-card"><h3>Second</h3><p>Two.</p></article>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "post-card",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "post-card");
        var list = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "news.html"));
        Assert.Contains("{{ for item in pages }}", list, StringComparison.Ordinal);
        Assert.Contains("components/article-card.html", list, StringComparison.Ordinal);
        Assert.DoesNotContain("First", list, StringComparison.Ordinal);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_tempDir, "sites", "post-card", "content", "posts"), "*.md").Length);
        Assert.DoesNotContain("for p in pages", list, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_TwoArticleCardDesigns_UseDifferentComponentFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body><main><h1>Home</h1>
            <article class="article-card image-card"><img src="img/home.jpg" alt=""><h3>Home card</h3><p>Home copy.</p></article>
            </main></body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main><h1>News</h1>
            <article class="article-card text-card"><h3>News card</h3><p>News copy.</p></article>
            <article class="article-card text-card"><h3>Other card</h3><p>Other copy.</p></article>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "two-designs",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "two-designs");
        var list = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "news.html"));
        Assert.Contains("components/article-card-2.html", list, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "components", "article-card.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "components", "article-card-2.html")));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_tempDir, "sites", "two-designs", "content", "posts"), "*.md").Length);
    }

    [Fact]
    public void Import_SeedPages_DoesNotDuplicateDetailPages()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "insights.html"),
            """
            <html><head><title>Insights</title></head><body><main><h1>Insights</h1>
            <article class="article-card"><h3>Detail</h3><a href="a.html">Read</a></article>
            </main></body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "a.html"),
            "<html><head><title>Detail</title></head><body><main><h1>Detail</h1><p>Body.</p></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "seed-detail",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var data = Path.Combine(_tempDir, "sites", "seed-detail", "data");
        var pages = File.ReadAllText(Path.Combine(data, "pages.json"));
        var posts = File.ReadAllText(Path.Combine(data, "posts.json"));
        Assert.DoesNotContain("\"slug\": \"a\"", pages, StringComparison.Ordinal);
        Assert.Contains("\"slug\": \"a\"", posts, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_HtmlLang_IsUsedWhenLanguageIsNotPassed()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html lang=\"en\"><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html lang=\"en\"><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "lang-en",
            RootDir = _tempDir,
            Force = true
        });

        var site = File.ReadAllText(Path.Combine(_tempDir, "sites", "lang-en", "site.yaml"));
        var seed = File.ReadAllText(Path.Combine(_tempDir, "sites", "lang-en", "notion-seed", "pages.json"));
        // The generated site config writes site.languages with one entry and no site.language.
        var config = Bukit.Config.ConfigLoader.Load(Path.Combine(_tempDir, "sites", "lang-en", "site.yaml"));
        Assert.Equal(["en"], config.Site.Languages);
        Assert.Equal("en", config.Site.Language);
        Assert.DoesNotMatch(@"(?m)^\s*language:", site);
        Assert.Contains("\"language\": \"en\"", seed, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_LazyAndSrcsetAssets_AreCopiedAndRewritten()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "lazy.jpg"), "lazy");
        File.WriteAllText(Path.Combine(_tempDir, "img", "one.jpg"), "one");
        File.WriteAllText(Path.Combine(_tempDir, "img", "two.jpg"), "two");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body><main>
            <h1>Home</h1>
            <img src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw=="
                 data-src="img/lazy.jpg" srcset="img/one.jpg 1x, img/two.jpg 2x" alt="Lazy">
            </main></body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            """
            <html><head><title>About</title></head><body><main>
            <h1>About</h1>
            <p><img src="img/placeholder.png" data-src="img/lazy.jpg" alt="Lazy content"></p>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "lazy-assets",
            RootDir = _tempDir,
            BaseUrl = "/corp/",
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "lazy-assets");
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "img", "lazy.jpg")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "img", "one.jpg")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "img", "two.jpg")));
        var home = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "index.html"));
        Assert.Contains("data-src=\"{{ base_url }}/img/lazy.jpg\"", home, StringComparison.Ordinal);
        Assert.Contains("{{ base_url }}/img/one.jpg 1x", home, StringComparison.Ordinal);
        var aboutDraft = File.ReadAllText(Path.Combine(_tempDir, "sites", "lazy-assets", "content", "pages", "about.md"));
        Assert.Contains("/corp/img/lazy.jpg", aboutDraft, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_InlineBodyCss_GetsBaseUrlPrefix()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "bg.png"), "bg");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body><main>
            <style>.hero{background:url(/img/bg.png)}</style>
            <h1>Home</h1>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "inline-body-css",
            RootDir = _tempDir,
            BaseUrl = "/corp/",
            Force = true
        });

        var home = File.ReadAllText(Path.Combine(_tempDir, "themes", "inline-body-css", "layouts", "pages", "index.html"));
        Assert.Contains("url({{ base_url }}/img/bg.png)", home, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "inline-body-css", "static", "img", "bg.png")));
    }

    [Fact]
    public void Import_ProjectRootReimport_DoesNotScanItsOwnOutput()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "root-reimport",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        };

        HtmlDemoImporter.Import(options);
        var second = HtmlDemoImporter.Import(options);

        Assert.Equal(2, second.PagesFound);
    }

    [Fact]
    public void Import_SitePathIsProjectRoot_StillScansAndPreservesTheDemo()
    {
        // --site-path . with the demo under the project root: the site folder
        // contains the demo, so it must not be treated as output to skip.
        var demoDir = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(demoDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = "root-site",
            RootDir = _tempDir,
            SitePath = _tempDir,
            Force = true,
            PreserveHtml = true,
            ContentSource = "json"
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.Equal(2, result.PagesFound);
        Assert.True(File.Exists(Path.Combine(_tempDir, "original-demo", "index.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "original-demo", "about.html")));
    }

    [Fact]
    public void Import_ListPageWithTwoCardGroups_DoesNotDeleteTheSecondGroup()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main>
            <h1>News</h1>
            <div class="card"><h3>First</h3></div>
            <div class="card"><h3>Second</h3></div>
            <h2>Archive</h2>
            <div class="card"><h3>Old</h3></div>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "two-groups",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var list = File.ReadAllText(Path.Combine(_tempDir, "themes", "two-groups", "layouts", "pages", "news.html"));
        Assert.Contains("{{ for item in pages }}", list, StringComparison.Ordinal);
        Assert.Contains("<h2>Archive</h2>", list, StringComparison.Ordinal);
        Assert.Contains("Old", list, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_RootRelativePageLinks_UseTheImportedRoute()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body><main><h1>Home</h1>
            <a href="/about.html">Root link</a>
            <a href="about.html">Relative link</a>
            </main></body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "root-links",
            RootDir = _tempDir,
            Force = true
        });

        var home = File.ReadAllText(Path.Combine(_tempDir, "themes", "root-links", "layouts", "pages", "index.html"));
        Assert.Contains("href=\"{{ base_url }}/about/\"", home, StringComparison.Ordinal);
        Assert.DoesNotContain("/about.html", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_NoExtractContent_DoesNotReferenceMissingComponents()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main><h1>News</h1>
            <article class="article-card"><h3>First</h3><p>One.</p></article>
            <article class="article-card"><h3>Second</h3><p>Two.</p></article>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "no-extract-components",
            RootDir = _tempDir,
            Force = true,
            ExtractContent = false
        });

        var list = File.ReadAllText(Path.Combine(_tempDir, "themes", "no-extract-components", "layouts", "pages", "news.html"));
        Assert.DoesNotContain("components/", list, StringComparison.Ordinal);
        Assert.Contains("First", list, StringComparison.Ordinal);
        Assert.Contains("Second", list, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_SingleQuotedCardClass_WritesTheComponent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main><h1>News</h1>
            <article class='article-card'><h3>First</h3><p>One.</p></article>
            <article class='article-card'><h3>Second</h3><p>Two.</p></article>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "single-quote-card",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "single-quote-card");
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "components", "article-card.html")));
        var list = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "news.html"));
        Assert.Contains("components/article-card.html", list, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_DetailWithoutMain_UsesPageContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main><h1>News</h1>
            <article class="article-card"><h3>Detail</h3><a href="a.html">Read</a></article>
            </main></body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "a.html"),
            "<html><head><title>Detail</title></head><body><article><h1>Detail</h1><p>Body marker.</p></article></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "detail-no-main",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "detail-no-main", "layouts", "pages", "a.html"));
        Assert.Contains("{{ page.content }}", template, StringComparison.Ordinal);
        Assert.DoesNotContain("Body marker.", template, StringComparison.Ordinal);
        Assert.Contains("</article>", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_PageWithoutMain_UsesPageContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><div class=\"content\"><h1>About</h1><p>About marker.</p></div></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "page-no-main",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "page-no-main", "layouts", "pages", "about.html"));
        Assert.Contains("{{ page.content }}", template, StringComparison.Ordinal);
        Assert.DoesNotContain("About marker.", template, StringComparison.Ordinal);
        Assert.Contains("</div>", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_MalformedSrcset_DoesNotAbortTheImport()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><img src=\"img/a.jpg\" srcset=\" , \"></main></body></html>");
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "a.jpg"), "a");

        var result = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "bad-srcset",
            RootDir = _tempDir,
            Force = true
        });

        Assert.True(result.PagesFound > 0);
    }

    [Fact]
    public void Import_HeaderAndFooterInsideOneWrapper_AreExtracted()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            """
            <html><head><title>Home</title></head><body><div class="page">
            <header><a href="index.html">Home</a><a href="about.html">About</a></header>
            <div class="content"><h1>Home</h1><p>Home marker.</p></div>
            <footer>Footer marker.</footer>
            </div></body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            """
            <html><head><title>About</title></head><body><div class="page">
            <header><a href="index.html">Home</a><a href="about.html">About</a></header>
            <div class="content"><h1>About</h1><p>About marker.</p></div>
            <footer>Footer marker.</footer>
            </div></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "wrapped-header",
            RootDir = _tempDir,
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "wrapped-header");
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "header.html")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "layouts", "partials", "footer.html")));
        var home = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "index.html"));
        Assert.DoesNotContain("Footer marker.", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ColumnWrappedCards_ReplaceTheWholeGroup()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main><h1>News</h1>
            <div class="grid">
              <div class="col"><article class="article-card"><h3>First</h3><p>One.</p></article></div>
              <div class="col"><article class="article-card"><h3>Second</h3><p>Two.</p></article></div>
            </div>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "column-cards",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var list = File.ReadAllText(Path.Combine(_tempDir, "themes", "column-cards", "layouts", "pages", "news.html"));
        Assert.Contains("{{ for item in pages }}", list, StringComparison.Ordinal);
        Assert.Contains("components/article-card.html", list, StringComparison.Ordinal);
        Assert.DoesNotContain("First", list, StringComparison.Ordinal);
        Assert.DoesNotContain("Second", list, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_CardImages_AreBoundPerItem()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "one.jpg"), "one");
        File.WriteAllText(Path.Combine(_tempDir, "img", "two.jpg"), "two");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main><h1>News</h1>
            <article class="article-card"><img src="img/one.jpg" alt="One"><h3>First</h3></article>
            <article class="article-card"><img src="img/two.jpg" alt="Two"><h3>Second</h3></article>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "card-images",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "card-images");
        var component = File.ReadAllText(Path.Combine(themeRoot, "layouts", "components", "article-card.html"));
        Assert.Contains("{{ if item.fields && item.fields.image && item.fields.image.value }}", component, StringComparison.Ordinal);
        Assert.Contains("<img src=\"{{ base_url }}{{ item.fields.image.value }}\" alt=\"One\">", component, StringComparison.Ordinal);
        var one = File.ReadAllText(Path.Combine(_tempDir, "sites", "card-images", "content", "posts", "first.md"));
        var two = File.ReadAllText(Path.Combine(_tempDir, "sites", "card-images", "content", "posts", "second.md"));
        Assert.Contains("image: /img/one.jpg", one, StringComparison.Ordinal);
        Assert.Contains("image: /img/two.jpg", two, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_MediaSeed_IncludesImagesReferencedFromCss()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "css"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "bg.png"), "bg");
        File.WriteAllText(Path.Combine(_tempDir, "css", "site.css"), "body{background:url(../img/bg.png)}");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"css/site.css\"></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "css-media",
            RootDir = _tempDir,
            Force = true
        });

        var media = File.ReadAllText(Path.Combine(_tempDir, "sites", "css-media", "notion-seed", "media.json"));
        Assert.Contains("img/bg.png", media, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_PageWithH2Only_UsesPageContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "contact.html"),
            "<html><head><title>Contact</title></head><body><div class=\"content\"><h2>Contact</h2><p>Contact marker.</p></div></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "h2-page",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "h2-page", "layouts", "pages", "contact.html"));
        Assert.Contains("{{ page.content }}", template, StringComparison.Ordinal);
        Assert.DoesNotContain("Contact marker.", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_CardImageSurvivesDetailMerge()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "one.jpg"), "one");
        File.WriteAllText(Path.Combine(_tempDir, "img", "two.jpg"), "two");
        File.WriteAllText(Path.Combine(_tempDir, "img", "detail.jpg"), "detail");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            """
            <html><head><title>News</title></head><body><main><h1>News</h1>
            <article class="article-card"><img src="img/one.jpg" alt="One"><h3><a href="a.html">First</a></h3></article>
            <article class="article-card"><img src="img/two.jpg" alt="Two"><h3><a href="b.html">Second</a></h3></article>
            </main></body></html>
            """);
        File.WriteAllText(Path.Combine(_tempDir, "a.html"),
            "<html><head><title>A</title></head><body><main><h1>First detail</h1><img src=\"img/detail.jpg\"></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "b.html"),
            "<html><head><title>B</title></head><body><main><h1>Second detail</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "card-image-merge",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var first = File.ReadAllText(Path.Combine(_tempDir, "sites", "card-image-merge", "content", "posts", "a.md"));
        var second = File.ReadAllText(Path.Combine(_tempDir, "sites", "card-image-merge", "content", "posts", "b.md"));
        Assert.Contains("image: /img/one.jpg", first, StringComparison.Ordinal);
        Assert.Contains("image: /img/two.jpg", second, StringComparison.Ordinal);
        Assert.Contains("detail.jpg", first, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_PageWithTwoSiblingBlocksAndNoH1_KeepsBoth()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "contact.html"),
            """
            <html><head><title>Contact</title></head><body>
            <div class="left"><h2>Left</h2><p>First block.</p></div>
            <div class="right"><h2>Right</h2><p>Second block.</p></div>
            </body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "two-blocks",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "two-blocks", "layouts", "pages", "contact.html"));
        var draft = File.ReadAllText(Path.Combine(_tempDir, "sites", "two-blocks", "content", "pages", "contact.md"));
        Assert.Contains("{{ page.content }}", template, StringComparison.Ordinal);
        Assert.DoesNotContain("First block.", template, StringComparison.Ordinal);
        Assert.DoesNotContain("Second block.", template, StringComparison.Ordinal);
        Assert.Contains("First block.", draft, StringComparison.Ordinal);
        Assert.Contains("Second block.", draft, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_DetailWithoutMain_KeepsFormAndIframeInTemplate()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "contact.html"),
            """
            <html><head><title>Contact</title></head><body>
            <div class="content"><h2>Contact</h2><p>Intro.</p>
              <form action="/contact" method="post"><input name="email"><button>Send</button></form>
              <iframe src="https://maps.example.com/embed"></iframe>
            </div>
            </body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "contact-form",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "contact-form", "layouts", "pages", "contact.html"));
        Assert.Contains("<form action=\"/contact\" method=\"post\">", template, StringComparison.Ordinal);
        Assert.Contains("<iframe src=\"https://maps.example.com/embed\"></iframe>", template, StringComparison.Ordinal);
        Assert.Contains("{{ page.content }}", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_MainDetail_KeepsFormAndIframeInTemplate()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "contact.html"),
            """
            <html><head><title>Contact</title></head><body><main>
            <h1>Contact</h1><p>Intro.</p>
            <form action="/contact" method="post"><input name="email"></form>
            <iframe src="https://maps.example.com/embed"></iframe>
            </main></body></html>
            """);

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "contact-form-main",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        });

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "contact-form-main", "layouts", "pages", "contact.html"));
        Assert.Contains("<form action=\"/contact\" method=\"post\">", template, StringComparison.Ordinal);
        Assert.Contains("<iframe src=\"https://maps.example.com/embed\"></iframe>", template, StringComparison.Ordinal);
        Assert.Contains("{{ page.content }}", template, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_Strict_HardcodedResidue_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><p>High Value Business Proposition For Buyers</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "strict-residue",
            RootDir = _tempDir,
            StrictMode = "fail"
        };

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("hardcoded content residue", ex.Message);
    }

    [Fact]
    public void Import_StrictWarn_HardcodedResidue_Succeeds()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><p>High Value Business Proposition For Buyers</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "strict-warn-residue",
            RootDir = _tempDir,
            Force = true,
            StrictMode = "warn"
        };

        var result = HtmlDemoImporter.Import(options);
        Assert.True(result.PagesFound > 0);
        var reportPath = Path.Combine(_tempDir, "sites", "strict-warn-residue", "import-report.md");
        Assert.True(File.Exists(reportPath));
        var report = File.ReadAllText(reportPath);
        Assert.Contains("Hardcoded Content Residue", report);
    }

    [Fact]
    public void Import_StrictWarn_DiagnosticsReported_ButSucceeds()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><p>Some suspiciously long and specific business phrase</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "strict-warn-diag",
            RootDir = _tempDir,
            Force = true,
            StrictMode = "warn"
        };

        var result = HtmlDemoImporter.Import(options);
        Assert.True(result.PagesFound > 0);
        var reportPath = Path.Combine(_tempDir, "sites", "strict-warn-diag", "import-report.md");
        Assert.True(File.Exists(reportPath));
        var report = File.ReadAllText(reportPath);
        Assert.Contains("Hardcoded Content Residue", report);
    }

    [Fact]
    public void Import_StrictFail_StillThrows()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><p>High Value Business Proposition For Buyers</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "strict-fail-backcompat",
            RootDir = _tempDir,
            StrictMode = "fail"
        };

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("hardcoded content residue", ex.Message);
    }

    [Fact]
    public void Import_WithAssets_GeneratedTemplatesHaveCorrectPaths()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title><link rel=\"stylesheet\" href=\"css/style.css\" /></head>" +
            "<body><header>Nav</header><main><h1>Test</h1><img src=\"img/logo.png\" /></main><footer>End</footer></body></html>");
        Directory.CreateDirectory(Path.Combine(_tempDir, "css"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "css", "style.css"), "body{}");
        File.WriteAllText(Path.Combine(_tempDir, "img", "logo.png"), "fake");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "asset-path-test",
            RootDir = _tempDir,
            Force = true
        };

        var result = HtmlDemoImporter.Import(options);

        var pagesDir = Path.Combine(_tempDir, "themes", "asset-path-test", "layouts", "pages");
        var indexContent = File.ReadAllText(Path.Combine(pagesDir, "index.html"));

        Assert.DoesNotContain("\"img/logo.png\"", indexContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"../", indexContent, StringComparison.OrdinalIgnoreCase);
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
    public void Import_ContentSeedAndMarkdownDraftUseSameExtractedContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Welcome</h1><p>Intro text.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About Us</h1><p>About body.</p><section><h2>Details</h2><p>Detailed body.</p></section></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "content-loop-test",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);

        var seed = File.ReadAllText(Path.Combine(_tempDir, "sites", "content-loop-test", "notion-seed", "pages.json"));
        var aboutDraft = File.ReadAllText(Path.Combine(_tempDir, "sites", "content-loop-test", "content", "pages", "about.md"));
        Assert.Contains("\"content\": \"<p>About body.</p>", seed);
        Assert.Contains("Detailed body.", seed);
        Assert.Contains("About body.", aboutDraft);
        Assert.Contains("Detailed body.", aboutDraft);
        Assert.DoesNotContain("<p>About body.</p>", aboutDraft);
        Assert.DoesNotContain("<main>", aboutDraft);
    }

    [Fact]
    public void Import_RouteMapSlugEscapingTheContentDirectory_ThrowsBeforeWriting()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "route-map.yaml"), """
pages:
  - source: about.html
    route: /about/
    type: Page
    template: about
    slug: ../../../escaped
""");

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "slug-escape",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = Path.Combine(_tempDir, "route-map.yaml")
        }));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.False(File.Exists(Path.Combine(_tempDir, "escaped.md")));
    }

    [Fact]
    public void Import_ValuesWithYamlSyntax_StayValidInSiteConfig()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "yaml-values",
            RootDir = _tempDir,
            Force = true,
            Language = "zh-CN",
            BaseUrl = "/base: #2"
        });

        var config = Bukit.Config.ConfigLoader.Load(Path.Combine(_tempDir, "sites", "yaml-values", "site.yaml"));

        // A generic "Home" title names no site, so the theme name is used.
        Assert.Equal("yaml-values", config.Site.Name);
        Assert.Equal("zh-CN", config.Site.Language);
        Assert.Equal("/base: #2", config.Site.BaseUrl);
        Assert.Equal("dist", config.Build.Output);
    }

    [Fact]
    public void Import_PageLanguageWithUnderscore_IsNormalizedToSafeTag()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html lang=\"en_US\"><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "lang-normalized",
            RootDir = _tempDir,
            Force = true
        });

        var config = Bukit.Config.ConfigLoader.Load(Path.Combine(_tempDir, "sites", "lang-normalized", "site.yaml"));
        Assert.Equal("en-US", config.Site.Language);
        Bukit.Config.ConfigValidator.Validate(config);
    }

    [Fact]
    public void Import_UnsafePageLanguage_FallsBackToDefault()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html lang=\"en us\"><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "lang-fallback",
            RootDir = _tempDir,
            Force = true
        });

        var config = Bukit.Config.ConfigLoader.Load(Path.Combine(_tempDir, "sites", "lang-fallback", "site.yaml"));
        Assert.Equal("zh", config.Site.Language);
    }

    [Theory]
    [InlineData("zh: #1", false)]
    [InlineData("en/US", false)]
    [InlineData("en/US", true)]
    public void Import_UnsafeLanguageOption_IsRejectedBeforeWriting(string language, bool dryRun)
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        var existingTheme = Path.Combine(_tempDir, "themes", "lang-rejected");
        Directory.CreateDirectory(existingTheme);
        File.WriteAllText(Path.Combine(existingTheme, "keep.txt"), "keep");

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "lang-rejected",
            RootDir = _tempDir,
            Force = true,
            DryRun = dryRun,
            Language = language
        }));

        Assert.Equal(ImportErrorKind.UserInput, ex.Kind);
        Assert.Contains("--language must use letters and digits", ex.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(existingTheme, "keep.txt")));
        Assert.Equal(new[] { "keep.txt" }, Directory.GetFiles(existingTheme).Select(Path.GetFileName).ToArray());
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "sites", "lang-rejected")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "content")));
    }

    [Fact]
    public void Import_ComponentTemplates_RewriteAssetsAndNavigationLinks()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "hero.png"), "fake image");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            "<html><head><title>News</title></head><body><main><h1>News</h1>" +
            "<article class=\"article-card\"><img src=\"img/hero.png\" alt=\"Hero\"><h3>Post</h3><a href=\"article.html\">Read</a></article>" +
            "</main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "component-assets",
            RootDir = _tempDir,
            Force = true
        });

        var card = File.ReadAllText(Path.Combine(_tempDir, "themes", "component-assets", "layouts", "components", "article-card.html"));
        Assert.Contains("src=\"{{ base_url }}{{ item.fields.image.value }}\"", card, StringComparison.Ordinal);
        Assert.Contains("href=\"{{ base_url }}{{ item.url }}\"", card, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "component-assets", "static", "img", "hero.png")));
    }

    [Fact]
    public void Import_DuplicateContentSlug_KeepsTheFirstRecord()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "a.html"),
            "<html><head><title>Alpha</title></head><body><main><h1>Alpha</h1><p>Body A.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "b.html"),
            "<html><head><title>Beta</title></head><body><main><h1>Beta</h1><p>Body B.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "route-map.yaml"), """
pages:
  - source: a.html
    route: /a/
    type: Page
    template: page
    slug: same-slug
  - source: b.html
    route: /b/
    type: Page
    template: page
    slug: same-slug
""");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "duplicate-slug",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = Path.Combine(_tempDir, "route-map.yaml")
        });

        var draft = File.ReadAllText(Path.Combine(_tempDir, "sites", "duplicate-slug", "content", "pages", "same-slug.md"));
        Assert.Contains("title: Alpha", draft, StringComparison.Ordinal);
        Assert.DoesNotContain("Body B.", draft, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_EveryPageOwnsItsTemplateAndAssets()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"https://cdn.example/home.css\"><style>.home{color:red}</style></head>" +
            "<body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title><link rel=\"stylesheet\" href=\"https://cdn.example/about.css\"><style>.about{color:blue}</style></head>" +
            "<body><main><h1>About</h1><p>About body.</p></main><script src=\"https://cdn.example/about.js\"></script></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "contact.html"),
            "<html><head><title>Contact</title><style>.contact{color:green}</style></head>" +
            "<body><main><h1>Contact</h1><p>Contact body.</p></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "own-templates",
            RootDir = _tempDir,
            Force = true
        });

        var layoutsDir = Path.Combine(_tempDir, "themes", "own-templates", "layouts");
        string Template(string name) => File.ReadAllText(Path.Combine(layoutsDir, "pages", name));
        string[] markers = ["home.css", ".home{", "about.css", ".about{", "about.js", ".contact{"];

        var baseLayout = File.ReadAllText(Path.Combine(layoutsDir, "layouts", "base.html"));
        Assert.All(markers, marker => Assert.DoesNotContain(marker, baseLayout, StringComparison.Ordinal));

        AssertOnly(Template("index.html"), markers, "home.css", ".home{");
        AssertOnly(Template("about.html"), markers, "about.css", ".about{", "about.js");
        AssertOnly(Template("contact.html"), markers, ".contact{");
        AssertOnly(Template("page.html"), markers);

        var aboutTemplate = Template("about.html");
        Assert.True(
            aboutTemplate.IndexOf("capture page_head", StringComparison.Ordinal) <
            aboutTemplate.IndexOf("about.css", StringComparison.Ordinal));
        Assert.True(
            aboutTemplate.IndexOf("capture page_scripts", StringComparison.Ordinal) <
            aboutTemplate.IndexOf("about.js", StringComparison.Ordinal));

        var contentDir = Path.Combine(_tempDir, "sites", "own-templates", "content");
        Assert.Contains("template: pages/about.html", File.ReadAllText(Path.Combine(contentDir, "pages", "about.md")), StringComparison.Ordinal);
        Assert.Contains("template: pages/contact.html", File.ReadAllText(Path.Combine(contentDir, "pages", "contact.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_SharedFooterScript_MovesIntoEachPageTemplate()
    {
        const string footer = "<footer><p>Shared footer text for every page</p></footer><script src=\"https://cdn.example/site.js\"></script>";
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            $"<html><head><title>Home</title></head><body><header><nav><a href=\"index.html\">Home</a></nav></header>\n<main><h1>Home</h1></main>\n{footer}</body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            $"<html><head><title>About</title></head><body><header><nav><a href=\"index.html\">Home</a></nav></header>\n<main><h1>About</h1></main>\n{footer}</body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "footer-script",
            RootDir = _tempDir,
            Force = true
        });

        var layoutsDir = Path.Combine(_tempDir, "themes", "footer-script", "layouts");
        var footerPartial = Path.Combine(layoutsDir, "partials", "footer.html");
        if (File.Exists(footerPartial))
            Assert.DoesNotContain("site.js", File.ReadAllText(footerPartial), StringComparison.Ordinal);
        Assert.DoesNotContain("site.js", File.ReadAllText(Path.Combine(layoutsDir, "layouts", "base.html")), StringComparison.Ordinal);
        Assert.Contains("site.js", File.ReadAllText(Path.Combine(layoutsDir, "pages", "index.html")), StringComparison.Ordinal);
        Assert.Contains("site.js", File.ReadAllText(Path.Combine(layoutsDir, "pages", "about.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_HeaderScript_StaysBeforeThePageContent()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body>" +
            "<header><nav><a href=\"/\">Home</a></nav><script src=\"menu.js\"></script></header>" +
            "<main><h1>Home</h1><script>menu.init()</script></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "menu.js"), "var menu = { init: function () {} };");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "header-script",
            RootDir = _tempDir,
            Force = true
        });

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "header-script", "layouts", "pages", "index.html"));
        var scriptIndex = template.IndexOf("menu.js", StringComparison.Ordinal);
        var mainIndex = template.IndexOf("<main", StringComparison.Ordinal);
        Assert.True(scriptIndex >= 0 && mainIndex > scriptIndex, template);

        var header = File.ReadAllText(Path.Combine(_tempDir, "themes", "header-script", "layouts", "partials", "header.html"));
        Assert.DoesNotContain("menu.js", header, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_LinkToDroppedHtmIndex_PointsAtTheHomePage()
    {
        const string header = "<header><nav><a href=\"index.htm\">Home</a><a href=\"about.html\">About</a></nav></header>";
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            $"<html><head><title>Home</title></head><body>{header}<main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "index.htm"),
            $"<html><head><title>Html Home</title></head><body>{header}<main><h1>Html Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            $"<html><head><title>About</title></head><body>{header}<main><h1>About</h1></main></body></html>");

        var result = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "alternate-index",
            RootDir = _tempDir,
            Force = true
        });

        var partial = File.ReadAllText(Path.Combine(_tempDir, "themes", "alternate-index", "layouts", "partials", "header.html"));
        Assert.Contains("href=\"{{ base_url }}/\"", partial, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics,
            diagnostic => diagnostic.Code == "INVALID_INTERNAL_LINK" && diagnostic.Message.Contains("index.htm", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_InlineStyle_RewritesUrlsAndEscapesScribanDelimiters()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub", "img"));
        File.WriteAllText(Path.Combine(_tempDir, "sub", "img", "bg.png"), "fake image");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "sub", "about.html"),
            "<html><head><title>About</title><style>.hero{background:url('img/bg.png')}/*{{x}}*/</style></head>" +
            "<body><main><h1>About</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "inline-css-urls",
            RootDir = _tempDir,
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "inline-css-urls");
        var about = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "sub-about.html"));
        Assert.Contains("url('{{ base_url }}/sub/img/bg.png')", about, StringComparison.Ordinal);
        Assert.Contains("/*{{ '{{' }}x{{ '}}' }}*/", about, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "sub", "img", "bg.png")) ||
                    File.Exists(Path.Combine(themeRoot, "assets", "sub", "img", "bg.png")));
    }

    private static void AssertOnly(string text, string[] markers, params string[] expected)
    {
        foreach (var marker in markers)
        {
            if (expected.Contains(marker))
                Assert.Contains(marker, text, StringComparison.Ordinal);
            else
                Assert.DoesNotContain(marker, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Import_IndexOnlyDemo_GeneratesPageTemplateReferencedBySiteConfig()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "index-only",
            RootDir = _tempDir,
            Force = true
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "index-only", "layouts", "pages", "page.html")));
        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "index-only", "site.yaml"));
        Assert.Contains("template: pages/page.html", siteYaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_NestedPageAssets_ResolveRelativeToThePageDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "posts", "images"));
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "posts", "article.html"),
            "<html><head><title>Article</title></head><body><main><h1>Article</h1><p>Body.</p><img src=\"images/photo.png\" alt=\"Photo\"></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "posts", "images", "photo.png"), "fake image");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "nested-assets",
            RootDir = _tempDir,
            Force = true
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "nested-assets", "static", "posts", "images", "photo.png")));
        var draft = File.ReadAllText(Path.Combine(_tempDir, "sites", "nested-assets", "content", "posts", "article.md"));
        Assert.Contains("![Photo](/posts/images/photo.png)", draft, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ExternalCssReferences_CopyImagesAndFonts()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "css"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "fonts"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "bg.png"), "fake image");
        File.WriteAllText(Path.Combine(_tempDir, "img", "root.png"), "fake root image");
        File.WriteAllText(Path.Combine(_tempDir, "fonts", "body.woff2"), "fake font");
        File.WriteAllText(Path.Combine(_tempDir, "css", "extra.css"),
            "@font-face{src:url('../fonts/body.woff2')}");
        File.WriteAllText(Path.Combine(_tempDir, "css", "site.css"),
            "@import \"/css/extra.css\";body{background:url(../img/bg.png) no-repeat,url(/img/root.png)}");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"css/site.css\"></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "css-references",
            RootDir = _tempDir,
            BaseUrl = "/corp/",
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "css-references");
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "css", "site.css")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "css", "extra.css")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "img", "bg.png")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "img", "root.png")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "fonts", "body.woff2")));
        var siteCss = File.ReadAllText(Path.Combine(themeRoot, "static", "css", "site.css"));
        Assert.Contains("/corp/img/root.png", siteCss, StringComparison.Ordinal);
        Assert.Contains("@import \"/corp/css/extra.css\"", siteCss, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_Utf8CssWithChineseAssetName_CopiesTheAsset()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "css"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "背景.png"), "fake image");
        File.WriteAllText(Path.Combine(_tempDir, "css", "site.css"),
            "body{background:url(../img/背景.png)}", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var originalBytes = File.ReadAllBytes(Path.Combine(_tempDir, "css", "site.css"));
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"css/site.css\"></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "utf8-css-name",
            RootDir = _tempDir,
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "utf8-css-name");
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "img", "背景.png")));
        Assert.Equal(originalBytes, File.ReadAllBytes(Path.Combine(themeRoot, "static", "css", "site.css")));
    }

    [Fact]
    public void Import_GbkCss_WithoutBaseUrl_IsConvertedToUtf8()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var gbk = System.Text.Encoding.GetEncoding("GB18030");
        Directory.CreateDirectory(Path.Combine(_tempDir, "css"));
        File.WriteAllBytes(Path.Combine(_tempDir, "css", "site.css"),
            gbk.GetBytes("@charset \"gbk\";body{font-family:\"微软雅黑\";}"));
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"css/site.css\"></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "gbk-css",
            RootDir = _tempDir,
            Force = true
        });

        var copied = File.ReadAllBytes(Path.Combine(_tempDir, "themes", "gbk-css", "static", "css", "site.css"));
        var text = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(copied);
        Assert.Contains("微软雅黑", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@charset", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Import_GbkCss_WithBaseUrl_KeepsTextAndRewritesRootUrls()
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var gbk = System.Text.Encoding.GetEncoding("GB18030");
        Directory.CreateDirectory(Path.Combine(_tempDir, "css"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "bg.png"), "fake image");
        File.WriteAllBytes(Path.Combine(_tempDir, "css", "site.css"),
            gbk.GetBytes("@charset \"gbk\";body{font-family:\"微软雅黑\";background:url(/img/bg.png)}"));
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"css/site.css\"></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "gbk-css-base",
            RootDir = _tempDir,
            BaseUrl = "/corp/",
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "gbk-css-base");
        var copied = File.ReadAllBytes(Path.Combine(themeRoot, "static", "css", "site.css"));
        var text = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(copied);
        Assert.Contains("微软雅黑", text, StringComparison.Ordinal);
        Assert.Contains("/corp/img/bg.png", text, StringComparison.Ordinal);
        Assert.DoesNotContain("@charset", text, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "img", "bg.png")));
    }

    [Fact]
    public void Import_CardLinkWithTheOtherHtmlExtension_MergesWithTheDetailPage()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "article.html"),
            "<html><head><title>Article</title></head><body><main><h1>First Post</h1><p>Body.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            "<html><head><title>News</title></head><body><main><h1>News</h1>" +
            "<article class=\"article-card\"><h3><a href=\"article.htm\">Other title</a></h3></article>" +
            "</main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "alternate-card",
            RootDir = _tempDir,
            Force = true
        });

        var posts = Path.Combine(_tempDir, "sites", "alternate-card", "content", "posts");
        var draft = Assert.Single(Directory.GetFiles(posts, "*.md"));
        Assert.Contains("title: First Post", File.ReadAllText(draft), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_CssAndReferencedAssets_KeepAResolvableRelativeLayout()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "assets", "css"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "assets", "img"));
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"assets/css/site.css\"></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "assets", "css", "site.css"), "body{background:url(../img/bg.png)}");
        File.WriteAllText(Path.Combine(_tempDir, "assets", "img", "bg.png"), "fake image");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "css-assets",
            RootDir = _tempDir,
            Force = true
        });

        var themeRoot = Path.Combine(_tempDir, "themes", "css-assets");
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "assets", "css", "site.css")));
        Assert.True(File.Exists(Path.Combine(themeRoot, "static", "assets", "img", "bg.png")));
        var home = File.ReadAllText(Path.Combine(themeRoot, "layouts", "pages", "index.html"));
        Assert.Contains("/assets/css/site.css", home, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_ReportFlagsResidualHardcodedHtml()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><section class=\"hero\"><h1>Home</h1><p>Intro.</p><a href=\"contact.html\">Talk</a></section></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "report-residual-test",
            RootDir = _tempDir,
            Force = true,
            GenerateReport = true
        };

        HtmlDemoImporter.Import(options);

        var report = File.ReadAllText(Path.Combine(_tempDir, "sites", "report-residual-test", "import-report.md"));
        Assert.Contains("Build/Data Source Relationship", report);
        Assert.Contains("Markdown draft", report);
        Assert.Contains("Hardcoded Content Residue", report);
        Assert.Contains("Extraction Coverage", report);
        Assert.Contains("Link Validation", report);
        Assert.Contains("Visual Verification", report);
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

        Assert.Contains("<h1>{{ page.title }}</h1>", insightsTemplate);
        Assert.Contains("<h1>{{ page.title }}</h1>", companiesTemplate);
        Assert.DoesNotContain("<h1>{{ this.title }}</h1>", insightsTemplate);
        Assert.DoesNotContain("<h1>{{ this.title }}</h1>", companiesTemplate);
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
        Assert.False(File.Exists(Path.Combine(contentDir, "index.md")));
        var about = File.ReadAllText(Path.Combine(contentDir, "pages", "about.md"));
        var post = File.ReadAllText(Path.Combine(contentDir, "posts", "article-detail.md"));
        var company = File.ReadAllText(Path.Combine(contentDir, "companies", "company-detail.md"));
        Assert.Contains("collection: page", about);
        Assert.Contains("collection: post", post);
        Assert.Contains("collection: company", company);
        Assert.Contains("draft: false", about);
        Assert.False(File.Exists(Path.Combine(contentDir, "pages", "article-detail.md")));
        Assert.False(File.Exists(Path.Combine(contentDir, "pages", "company-detail.md")));
        Assert.DoesNotContain("type: \"", about);
        Assert.DoesNotContain("type: \"", post);
        Assert.DoesNotContain("type: \"", company);
    }

    [Fact]
    public void RouteMap_ChangesPageTypeAndTemplate()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "china-companies.html"),
            "<html><head><title>China</title></head><body><main><h1>China Companies</h1></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "demo.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: china-companies.html
    route: /china-companies/
    type: CompanyList
    template: china-companies
""");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "routemap-type-test",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.Equal(2, result.ReportPages.Count);
        var chinaPage = result.ReportPages.First(p => p.Template.Contains("china-companies"));
        Assert.Contains("/china-companies/", chinaPage.Route);
        Assert.Contains("CompanyList", chinaPage.Type);

        var pageTemplate = Path.Combine(_tempDir, "themes", "routemap-type-test", "layouts", "pages", "china-companies.html");
        Assert.True(File.Exists(pageTemplate), "Expected template china-companies.html to be generated from route-map");
    }

    [Fact]
    public void RouteMap_TwoPagesWithTheSameTemplate_FailsAsUserInput()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "team.html"),
            "<html><head><title>Team</title></head><body><main><h1>Team</h1></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "shared.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: about.html
    route: /about/
    template: shared
  - source: team.html
    route: /team/
    template: shared
""");

        var error = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "shared-template",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        }));

        Assert.Equal(ImportErrorKind.UserInput, error.Kind);
        Assert.Contains("shared", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "themes", "shared-template")));
    }

    [Fact]
    public void RouteMap_ExplicitListTemplates_AreUsedBySiteYamlListRoutes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "blog.html"),
            "<html><head><title>Blog</title></head><body><main><h1>Blog</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "article.html"),
            "<html><head><title>Article</title></head><body><main><h1>Article</h1><p>Body.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "china-companies.html"),
            "<html><head><title>China</title></head><body><main><h1>China</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "company.html"),
            "<html><head><title>Company</title></head><body><main><h1>Company</h1><p>Body.</p></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "lists.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: blog.html
    route: /insights/
    type: PostList
    template: news
  - source: china-companies.html
    route: /companies/
    type: CompanyList
    template: china-companies
""");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "list-templates",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        });

        var pagesDir = Path.Combine(_tempDir, "themes", "list-templates", "layouts", "pages");
        Assert.True(File.Exists(Path.Combine(pagesDir, "news.html")));
        Assert.True(File.Exists(Path.Combine(pagesDir, "china-companies.html")));

        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "list-templates", "site.yaml"));
        Assert.Contains("listTemplate: pages/news.html", siteYaml, StringComparison.Ordinal);
        Assert.Contains("listTemplate: pages/china-companies.html", siteYaml, StringComparison.Ordinal);
        Assert.DoesNotContain("pages/blog.html", siteYaml, StringComparison.Ordinal);
        Assert.DoesNotContain("pages/companies.html", siteYaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_UnrecognisedPage_IsDraftedAndRenderedWithItsOwnTemplate()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "pricing.html"),
            "<html><head><title>Pricing</title><style>.pricing{color:red}</style></head>" +
            "<body><main><h1>Pricing</h1><p>Plans and prices.</p></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "unknown-page",
            RootDir = _tempDir,
            Force = true
        });

        var draft = Path.Combine(_tempDir, "sites", "unknown-page", "content", "pages", "pricing.md");
        Assert.True(File.Exists(draft), "An unrecognised page must still become a page.");
        Assert.Contains("template: pages/pricing.html", File.ReadAllText(draft), StringComparison.Ordinal);
        Assert.Contains(".pricing{", File.ReadAllText(
            Path.Combine(_tempDir, "themes", "unknown-page", "layouts", "pages", "pricing.html")), StringComparison.Ordinal);

        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "unknown-page", "site.yaml"));
        Assert.Contains("template: pages/page.html", siteYaml, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteMap_SameFileNameInTwoFolders_SharesTheEntryWithoutFailing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        Directory.CreateDirectory(Path.Combine(_tempDir, "en"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "zh"));
        File.WriteAllText(Path.Combine(_tempDir, "en", "about.html"),
            "<html><head><title>About</title><style>.en{}</style></head><body><main><h1>About</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "zh", "about.html"),
            "<html><head><title>关于</title><style>.zh{}</style></head><body><main><h1>关于</h1></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "about.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: about.html
    route: /about/
    type: Page
    template: about
""");

        var result = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "same-name",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        });

        var pagesDir = Path.Combine(_tempDir, "themes", "same-name", "layouts", "pages");
        var templates = Directory.GetFiles(pagesDir).Select(path => Path.GetFileName(path)!).ToArray();
        Assert.Contains("about.html", templates);
        Assert.Contains(templates, name => name.EndsWith("about.html", StringComparison.Ordinal) && name != "about.html");
        var styles = templates.Where(name => name.Contains("about", StringComparison.Ordinal))
            .Select(name => File.ReadAllText(Path.Combine(pagesDir, name))).ToArray();
        Assert.Single(styles, text => text.Contains(".en{", StringComparison.Ordinal));
        Assert.Single(styles, text => text.Contains(".zh{", StringComparison.Ordinal));

        // Both pages are imported: the entry's owner takes its route, the other gets its own slug.
        var pagesContent = Path.Combine(_tempDir, "sites", "same-name", "content", "pages");
        var owner = File.ReadAllText(Path.Combine(pagesContent, "about.md"));
        Assert.Contains("url: /about/", owner, StringComparison.Ordinal);
        var other = Assert.Single(Directory.GetFiles(pagesContent, "*.md"),
            path => Path.GetFileName(path) != "about.md");
        Assert.DoesNotContain("url:", File.ReadAllText(other), StringComparison.Ordinal);
        Assert.Contains(result.Warnings, warning => warning.Contains("already has", StringComparison.Ordinal));
    }

    [Fact]
    public void RouteMap_SameListFileInTwoFolders_SharesTheEntryWithoutFailing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        Directory.CreateDirectory(Path.Combine(_tempDir, "en"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "zh"));
        File.WriteAllText(Path.Combine(_tempDir, "en", "blog.html"),
            "<html><head><title>Blog</title></head><body><main><h1>Blog</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "zh", "blog.html"),
            "<html><head><title>博客</title></head><body><main><h1>博客</h1></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "blog.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: blog.html
    route: /insights/
    type: PostList
    template: news
""");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "same-list",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        });

        var pagesDir = Path.Combine(_tempDir, "themes", "same-list", "layouts", "pages");
        Assert.True(File.Exists(Path.Combine(pagesDir, "news.html")));
        // The second file does not own the entry, so it is named from its slug instead.
        Assert.True(File.Exists(Path.Combine(pagesDir, "insights.html")));
    }

    [Fact]
    public void RouteMap_CustomRoute_GoesIntoFrontMatterNotSiteYaml()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.html"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "custom.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: about.html
    route: /company/about/
    type: Page
    template: about-us
""");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "custom-route",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        });

        var siteDir = Path.Combine(_tempDir, "sites", "custom-route");
        Assert.DoesNotContain("appPages", File.ReadAllText(Path.Combine(siteDir, "site.yaml")), StringComparison.Ordinal);
        var draft = File.ReadAllText(Path.Combine(siteDir, "content", "pages", "about.md"));
        Assert.Contains("url: /company/about/", draft, StringComparison.Ordinal);
        Assert.Contains("template: pages/about-us.html", draft, StringComparison.Ordinal);
    }

    [Fact]
    public void RouteMap_IndexFileTypedAsPage_DoesNotTakeTheHomeTemplate()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Landing</title><style>.landing{color:red}</style></head>" +
            "<body><main><h1>Landing</h1><p>Landing body.</p></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "index.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: index.html
    route: /landing/
    type: Page
""");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "index-as-page",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        });

        var pagesDir = Path.Combine(_tempDir, "themes", "index-as-page", "layouts", "pages");
        Assert.DoesNotContain(".landing{", File.ReadAllText(Path.Combine(pagesDir, "index.html")), StringComparison.Ordinal);
        Assert.Contains(".landing{", File.ReadAllText(Path.Combine(pagesDir, "index-2.html")), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_CardsSharingOneDetailPage_KeepOnePost()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "article.html"),
            "<html><head><title>Article</title></head><body><main><article><h1>First Post</h1><p>Body.</p></article></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            "<html><head><title>News</title></head><body><main><h1>News</h1>" +
            "<article class=\"article-card\"><h3><a href=\"article.html\">First Post</a></h3><p>One.</p></article>" +
            "<article class=\"article-card\"><h3><a href=\"article.html\">Second Post</a></h3><p>Two.</p></article>" +
            "</main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "shared-detail",
            RootDir = _tempDir,
            Force = true
        });

        var posts = Path.Combine(_tempDir, "sites", "shared-detail", "content", "posts");
        var draft = Assert.Single(Directory.GetFiles(posts, "*.md"));
        Assert.Contains("title: First Post", File.ReadAllText(draft), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_CardsLinkingToNonDetailPages_KeepEveryCard()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "contact.html"),
            "<html><head><title>Contact</title></head><body><main><h1>Contact</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "services.html"),
            "<html><head><title>Services</title></head><body><main><h1>Services</h1>" +
            "<div class=\"service-card\"><h3>Design</h3><a href=\"contact.html\">More</a></div>" +
            "<div class=\"service-card\"><h3>Build</h3><a href=\"contact.html\">More</a></div>" +
            "<div class=\"service-card\"><h3>Run</h3><a href=\"contact.html\">More</a></div>" +
            "</main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "news.html"),
            "<html><head><title>News</title></head><body><main><h1>News</h1>" +
            "<article class=\"article-card\"><h3>Partner One</h3><a href=\"https://partner.example.com/\">Visit</a></article>" +
            "<article class=\"article-card\"><h3>Partner Two</h3><a href=\"https://partner.example.com/\">Visit</a></article>" +
            "</main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "non-detail-links",
            RootDir = _tempDir,
            Force = true
        });

        var siteDir = Path.Combine(_tempDir, "sites", "non-detail-links", "content");
        var services = Directory.GetFiles(Path.Combine(siteDir, "services"), "*.md")
            .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name, StringComparer.Ordinal).ToList();
        var posts = Directory.GetFiles(Path.Combine(siteDir, "posts"), "*.md")
            .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.Equal(["build", "design", "run"], services);
        Assert.Equal(["partner-one", "partner-two"], posts);
    }

    [Fact]
    public void Import_SameTitleOnDifferentDetailPages_KeepsBoth()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "article.html"),
            "<html><head><title>Expo 2026</title></head><body><main><h1>Annual Expo</h1><p>2026.</p></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "article-detail.html"),
            "<html><head><title>Expo 2027</title></head><body><main><h1>Annual Expo</h1><p>2027.</p></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "same-title",
            RootDir = _tempDir,
            Force = true
        });

        var posts = Path.Combine(_tempDir, "sites", "same-title", "content", "posts");
        Assert.Equal(2, Directory.GetFiles(posts, "*.md").Length);
    }

    [Fact]
    public void Import_PostsWithoutTheirOwnPage_WarnAboutTheFallbackTemplate()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "insights.html"),
            "<html><head><title>Insights</title></head><body><main><h1>Insights</h1>" +
            "<article class=\"article-card\"><h3>First Post</h3><p>One.</p></article>" +
            "<article class=\"article-card\"><h3>Second Post</h3><p>Two.</p></article>" +
            "</main></body></html>");

        var result = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "fallback-warning",
            RootDir = _tempDir,
            Force = true
        });

        Assert.Contains(result.Warnings, warning =>
            warning.Contains("pages/post.html", StringComparison.Ordinal) &&
            warning.Contains("first-post", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_WithBuildSourceNotion_SkipsMarkdownDraft()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Notion Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "notion-md-skip-test",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "notion",
            BuildSource = "notion"
        };

        var result = HtmlDemoImporter.Import(options);

        Assert.False(Directory.Exists(Path.Combine(_tempDir, "sites", "notion-md-skip-test", "content")),
            "content/ directory must not exist when using --content-source notion");
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "sites", "notion-md-skip-test", "notion-seed")),
            "notion-seed/ directory must exist when using --content-source notion");
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "notion-md-skip-test", "notion-seed", "notion-database-map.yaml")),
            "default notion database map must be generated with notion seed files");

        var config = Bukit.Config.ConfigLoader.Load(Path.Combine(_tempDir, "sites", "notion-md-skip-test", "site.yaml"));
        Assert.True(config.Content.Sources is { Count: >= 2 });

        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "notion-md-skip-test", "site.yaml"));
        Assert.Contains("sources:", siteYaml);
        Assert.Contains("name: pages", siteYaml);
        Assert.Contains("collection: page", siteYaml);
        Assert.Contains("databaseId: ${NOTION_PAGES_DATABASE_ID}", siteYaml);
        Assert.Contains("name: navigation", siteYaml);
        Assert.Contains("mode: data", siteYaml);
        Assert.Contains("collection: navigation", siteYaml);
        Assert.Contains("databaseId: ${NOTION_NAVIGATION_DATABASE_ID}", siteYaml);
        Assert.DoesNotContain("provider: markdown", siteYaml);
    }

    [Fact]
    public void Import_WithBuildSourceNotionAndSingleDatabaseId_UsesSingleNotionProvider()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Notion Test</title></head><body><main><h1>Hello</h1></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "notion-single-db-test",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "notion",
            BuildSource = "notion",
            NotionDatabaseId = "single-db"
        };

        HtmlDemoImporter.Import(options);

        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "notion-single-db-test", "site.yaml"));
        Assert.Contains("sources:", siteYaml);
        Assert.Contains("type: notion", siteYaml);
        Assert.Contains("databaseId: single-db", siteYaml);
        Assert.DoesNotContain("provider: notion", siteYaml);
    }

    [Fact]
    public void RouteMap_DynamicRouteWithBrace_NotUsedAsSlug()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "insights.html"),
            "<html><head><title>Insights</title></head><body><main><h1>Insights</h1></main></body></html>");
        var routeMapPath = Path.Combine(_tempDir, "dynamic.routes.yaml");
        File.WriteAllText(routeMapPath, """
pages:
  - source: insights.html
    route: /insights/{slug}/
    type: PostList
    template: insights
""");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "dynamic-slug-test",
            RootDir = _tempDir,
            Force = true,
            RouteMapPath = routeMapPath
        };

        var result = HtmlDemoImporter.Import(options);

        var insightPage = result.ReportPages.First(p => p.Template.Contains("insights"));
        Assert.Contains("/insights/{slug}/", insightPage.Route);
    }

    [Fact]
    public void Import_AbsoluteSitePath_GeneratesBuildableMarkdownDir()
    {
        var rootDir = Path.Combine(_tempDir, "root");
        var demoDir = Path.Combine(rootDir, "demo");
        var siteDir = Path.Combine(_tempDir, "external-site");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1><p>Intro.</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = demoDir,
            ThemeName = "absolute-site-test",
            RootDir = rootDir,
            SitePath = siteDir,
            Force = true,
            ContentSource = "json"
        };

        HtmlDemoImporter.Import(options);

        var yaml = File.ReadAllText(Path.Combine(siteDir, "site.yaml"));
        Assert.Contains("sources:", yaml);
        Assert.Contains("type: markdown", yaml);
        Assert.Contains("dir: content", yaml.Replace('\\', '/'));
        Assert.DoesNotContain("provider: markdown", yaml);
        Assert.DoesNotContain("..", yaml);
        Assert.True(File.Exists(Path.Combine(siteDir, "themes", "absolute-site-test", "layouts", "pages", "index.html")));
        Assert.False(Directory.Exists(Path.Combine(rootDir, "themes", "absolute-site-test")));
    }

    [Fact]
    public void Import_BuiltOutputPages_AreNotImportedButTheirAssetsAreCopied()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "dist", "css"));
        File.WriteAllText(Path.Combine(_tempDir, "dist", "css", "app.css"), "body{margin:0}");
        File.WriteAllText(Path.Combine(_tempDir, "dist", "about.html"),
            "<html><head><title>Built About</title></head><body><main><h1>Built About</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title><link rel=\"stylesheet\" href=\"dist/css/app.css\"></head><body><main><h1>Home</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "built-output",
            RootDir = _tempDir,
            Force = true
        });

        Assert.False(File.Exists(Path.Combine(_tempDir, "sites", "built-output", "content", "pages", "dist-about.md")));
        Assert.DoesNotContain("Built About", File.ReadAllText(Path.Combine(_tempDir, "sites", "built-output", "import-report.md")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "built-output", "static", "dist", "css", "app.css")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "sites", "built-output", "original-demo", "dist")));
    }

    [Fact]
    public void Import_HtmPage_IsImported()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "about.htm"),
            "<html><head><title>About</title></head><body><main><h1>About</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "htm-page",
            RootDir = _tempDir,
            Force = true
        });

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "htm-page", "layouts", "pages", "about.html")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "htm-page", "content", "pages", "about.md")));
    }

    [Fact]
    public void Import_Force_KeepsAnEditedNotionSeed()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "company-detail.html"),
            "<html><head><title>Company</title></head><body><main><h1>Company</h1><p>Body.</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "seed-manifest",
            RootDir = _tempDir,
            Force = true
        };

        HtmlDemoImporter.Import(options);
        var seedPath = Path.Combine(_tempDir, "sites", "seed-manifest", "notion-seed", "companies.json");
        Assert.True(File.Exists(seedPath));
        File.AppendAllText(seedPath, "\n\n");
        var edited = File.ReadAllText(seedPath);

        HtmlDemoImporter.Import(options);

        Assert.Equal(edited, File.ReadAllText(seedPath));
    }

    [Fact]
    public void Import_Force_KeepsDatabaseIdsWrittenIntoTheNotionMap()
    {
        var demo = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demo);
        File.WriteAllText(Path.Combine(demo, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(demo, "company-detail.html"),
            "<html><head><title>Company</title></head><body><main><h1>Company</h1><p>Body.</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = demo,
            ThemeName = "map-ids",
            RootDir = _tempDir
        };

        HtmlDemoImporter.Import(options);
        var mapPath = Path.Combine(_tempDir, "sites", "map-ids", "notion-seed", "notion-database-map.yaml");
        Assert.True(File.Exists(mapPath));

        // The Notion push writes the created database ids back into this file.
        File.WriteAllText(mapPath,
            File.ReadAllText(mapPath).Replace("databaseId: \"\"", "databaseId: \"db-123\""));

        HtmlDemoImporter.Import(options with { Force = true });

        Assert.Contains("db-123", File.ReadAllText(mapPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Import_SensitiveNestedDirectory_ThrowsBeforePreservingHtml()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Test</title></head><body><main><h1>Hello</h1></main></body></html>");
        var gitDir = Path.Combine(_tempDir, "nested", ".git");
        Directory.CreateDirectory(gitDir);
        File.WriteAllText(Path.Combine(gitDir, "HEAD"), "ref: refs/heads/main");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "sensitive-dir-test",
            RootDir = _tempDir,
            Force = true
        };

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
        Assert.Contains("sensitive", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_tempDir, "sites", "sensitive-dir-test", "original-demo", "nested", ".git", "HEAD")));
    }

    [Fact]
    public void Import_ListTemplate_ReplacesCardGroupWithSingleLoop()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "insights.html"),
            """
            <html><head><title>Insights</title></head><body><main>
              <h1>Insights</h1>
              <section class="article-grid">
                <article class="article-card"><h3>A</h3><p>Summary A.</p><a href="a.html">Read</a></article>
                <article class="article-card"><h3>B</h3><p>Summary B.</p><a href="b.html">Read</a></article>
                <article class="article-card"><h3>C</h3><p>Summary C.</p><a href="c.html">Read</a></article>
              </section>
            </main></body></html>
            """);

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "single-loop-test",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "json"
        };

        HtmlDemoImporter.Import(options);

        var template = File.ReadAllText(Path.Combine(_tempDir, "themes", "single-loop-test", "layouts", "pages", "insights.html"));
        Assert.Equal(1, CountOccurrences(template, "{{ for item in pages }}"));
        Assert.DoesNotContain("Summary A.", template);
        Assert.DoesNotContain("Summary B.", template);
        Assert.DoesNotContain("Summary C.", template);
    }

    [Fact]
    public void Import_WithContentSourceNotion_StillBuildsFromMarkdownDraftByDefault()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Notion Test</title></head><body><main><h1>Hello</h1><p>Intro.</p></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "notion-review-test",
            RootDir = _tempDir,
            Force = true,
            ContentSource = "notion"
        };

        HtmlDemoImporter.Import(options);

        Assert.True(Directory.Exists(Path.Combine(_tempDir, "sites", "notion-review-test", "content")));
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "sites", "notion-review-test", "notion-seed")));

        var siteYaml = File.ReadAllText(Path.Combine(_tempDir, "sites", "notion-review-test", "site.yaml"));
        Assert.Contains("sources:", siteYaml);
        Assert.Contains("type: markdown", siteYaml);
        Assert.DoesNotContain("provider: markdown", siteYaml);
        Assert.DoesNotContain("provider: notion", siteYaml);
    }

    [Fact]
    public void Import_Strict_InvalidInternalLink_Throws()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><h1>Home</h1><a href=\"missing.html\">Missing</a></main></body></html>");

        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "bad-link-test",
            RootDir = _tempDir,
            Force = true,
            StrictMode = "fail"
        };

        var ex = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
        Assert.Contains("INVALID_INTERNAL_LINK", ex.Message);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    [Fact]
    public void Import_WarnsAboutUnmatchedRouteMapEntriesAndOnlyAWrittenPlaceholderUrl()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Acme</title></head><body><main><h1>Home</h1></main></body></html>");
        var routeMap = Path.Combine(_tempDir, "routes.yaml");
        File.WriteAllText(routeMap, "pages:\n  - source: page/about.html\n    route: /company/\n");
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir, ThemeName = "warn", RootDir = _tempDir, Force = true, RouteMapPath = routeMap
        };

        var first = HtmlDemoImporter.Import(options);
        Assert.Contains(first.Warnings, w => w.Contains("'page/about.html' matches no page", StringComparison.Ordinal));
        Assert.Contains(first.Warnings, w => w.Contains("site.yaml uses 'https://example.com'", StringComparison.Ordinal));

        // A site.yaml the user fixed is kept, and no longer reported as a placeholder.
        var siteYaml = Path.Combine(_tempDir, "sites", "warn", "site.yaml");
        File.WriteAllText(siteYaml, File.ReadAllText(siteYaml).Replace("https://example.com", "https://acme.example", StringComparison.Ordinal));
        var second = HtmlDemoImporter.Import(options);
        Assert.DoesNotContain(second.Warnings, w => w.Contains("site.yaml uses", StringComparison.Ordinal));
    }
}
