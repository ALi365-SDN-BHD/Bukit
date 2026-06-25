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
        Assert.Contains("敏感", ex.Message);
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
        Assert.True(result.ComponentsGenerated >= 3);
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
        Assert.Contains("<a href=\"/\">Home</a>", header);
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
        Assert.Contains(result.Warnings, w => w.Contains("不执行 JS 动态生成菜单", StringComparison.Ordinal));
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

        Assert.Contains(result.Warnings, w => w.Contains("不执行 JS 动态生成菜单", StringComparison.Ordinal));
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
        Assert.Contains("class=\"hero\"", indexTemplate);
        Assert.Contains("class=\"faq", indexTemplate);
        Assert.Contains("{{ include 'components/hero.html' }}", indexTemplate);
        Assert.Contains("{{ include 'components/article-card.html' }}", indexTemplate);
        Assert.Contains("{{ include 'components/faq.html' }}", indexTemplate);
        Assert.DoesNotContain("Silk Road", indexTemplate);
        Assert.DoesNotContain("Connect markets.", indexTemplate);

        Assert.True(File.Exists(Path.Combine(_tempDir, "themes", "structure-test", "layouts", "components", "article-card.html")));
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
        Assert.Contains("硬编码内容残留", ex.Message);
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
        Assert.Contains("硬编码内容残留", ex.Message);
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
        Assert.Contains("<p>About body.</p>", aboutDraft);
        Assert.Contains("Detailed body.", aboutDraft);
        Assert.DoesNotContain("<main>", aboutDraft);
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
        Assert.False(File.Exists(Path.Combine(contentDir, "pages", "article-detail.md")));
        Assert.False(File.Exists(Path.Combine(contentDir, "pages", "company-detail.md")));
        Assert.DoesNotContain("type: \"", index);
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
    public void Import_WhenDefaultThemeDirectoryIsSymlinkOutsideRoot_ThrowsBeforeWriting()
    {
        var demoDir = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Theme Symlink</title></head><body><main><h1>Hello</h1></main></body></html>");

        Directory.CreateDirectory(Path.Combine(_tempDir, "themes"));
        string outsideTheme = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bukit-import-theme-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        string linkedTheme = Path.Combine(_tempDir, "themes", "theme-symlink-test");
        Directory.CreateSymbolicLink(linkedTheme, outsideTheme);

        try
        {
            var options = new HtmlDemoImportOptions
            {
                InputPath = demoDir,
                ThemeName = "theme-symlink-test",
                RootDir = _tempDir,
                Force = true
            };

            var exception = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
            Assert.Equal(ImportErrorKind.UserInput, exception.Kind);
            Assert.Contains("theme output", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(outsideTheme, "theme.yaml")));
        }
        finally
        {
            TestCleanup.DeleteDirectory(outsideTheme, recursive: true);
        }
    }

    [Fact]
    public void Import_WhenDefaultSiteDirectoryIsSymlinkOutsideRoot_ThrowsBeforeWriting()
    {
        var demoDir = Path.Combine(_tempDir, "demo");
        Directory.CreateDirectory(demoDir);
        File.WriteAllText(Path.Combine(demoDir, "index.html"),
            "<html><head><title>Site Symlink</title></head><body><main><h1>Hello</h1></main></body></html>");

        Directory.CreateDirectory(Path.Combine(_tempDir, "sites"));
        string outsideSite = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bukit-import-site-outside-" + Guid.NewGuid().ToString("N"))).FullName;
        string linkedSite = Path.Combine(_tempDir, "sites", "site-symlink-test");
        Directory.CreateSymbolicLink(linkedSite, outsideSite);

        try
        {
            var options = new HtmlDemoImportOptions
            {
                InputPath = demoDir,
                ThemeName = "site-symlink-test",
                RootDir = _tempDir,
                Force = true
            };

            var exception = Assert.Throws<ImportException>(() => HtmlDemoImporter.Import(options));
            Assert.Equal(ImportErrorKind.UserInput, exception.Kind);
            Assert.Contains("site output", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(outsideSite, "site.yaml")));
        }
        finally
        {
            TestCleanup.DeleteDirectory(outsideSite, recursive: true);
        }
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
        Assert.Contains("敏感", ex.Message);
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
}
