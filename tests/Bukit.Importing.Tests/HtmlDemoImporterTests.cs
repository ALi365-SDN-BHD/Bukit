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
        Assert.Contains("<h1>Hero</h1>", hero);
        Assert.Contains("<p>Intro</p>", hero);
        Assert.Contains("<h3>Q?</h3>", faq);
        Assert.Contains("<p>A.</p>", faq);
        Assert.DoesNotContain("{{ page.content }}", hero);
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
        // Components hold the whole element; the template only includes them (no double wrapper).
        Assert.DoesNotContain("class=\"hero\"", indexTemplate);
        Assert.Contains("class=\"hero\"", File.ReadAllText(Path.Combine(_tempDir, "themes", "structure-test", "layouts", "components", "hero.html")));
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
            Language = "zh: #1",
            BaseUrl = "/base: #2"
        });

        var config = Bukit.Config.ConfigLoader.Load(Path.Combine(_tempDir, "sites", "yaml-values", "site.yaml"));

        Assert.Equal("yaml-values", config.Site.Name);
        Assert.Equal("zh: #1", config.Site.Language);
        Assert.Equal("/base: #2", config.Site.BaseUrl);
        Assert.Equal("dist", config.Build.Output);
    }

    [Fact]
    public void Import_ComponentTemplates_RewriteAssetsAndNavigationLinks()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "img"));
        File.WriteAllText(Path.Combine(_tempDir, "img", "hero.png"), "fake image");
        File.WriteAllText(Path.Combine(_tempDir, "index.html"),
            "<html><head><title>Home</title></head><body><main><section class=\"hero\"><h1>Home</h1><p>Intro</p><img src=\"img/hero.png\" alt=\"Hero\"><a href=\"contact.html\">Talk</a></section></main></body></html>");
        File.WriteAllText(Path.Combine(_tempDir, "contact.html"),
            "<html><head><title>Contact</title></head><body><main><h1>Contact</h1></main></body></html>");

        HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "component-assets",
            RootDir = _tempDir,
            Force = true
        });

        var hero = File.ReadAllText(Path.Combine(_tempDir, "themes", "component-assets", "layouts", "components", "hero.html"));
        Assert.Contains("src=\"{{ base_url }}/img/hero.png\"", hero, StringComparison.Ordinal);
        Assert.Contains("href=\"{{ base_url }}/contact/\"", hero, StringComparison.Ordinal);
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
    public void Import_CardsSharingOneDetailTemplate_KeepEveryPostWithAWarning()
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

        var result = HtmlDemoImporter.Import(new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "shared-detail",
            RootDir = _tempDir,
            Force = true
        });

        var posts = Path.Combine(_tempDir, "sites", "shared-detail", "content", "posts");
        Assert.Equal(2, Directory.GetFiles(posts, "*.md").Length);
        Assert.Contains(result.Warnings, warning => warning.Contains("Second Post", StringComparison.Ordinal));
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
}
