using Bukit.Importing;
using Xunit;

namespace Bukit.Importing.Tests;

public sealed class SeedGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public SeedGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-seed-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    private static DiscoveredPage MakePage(string slug, List<string> assetPaths)
    {
        return new DiscoveredPage
        {
            FilePath = $"/test/{slug}.html",
            RelativePath = $"{slug}.html",
            Slug = slug,
            Type = PageType.Page,
            Title = "Test",
            AssetPaths = assetPaths
        };
    }

    [Fact]
    public void Generate_NotionSource_WritesToNotionSeed()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-seed",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var content = new ExtractedContent();
        var components = new List<DiscoveredComponent>();
        var pages = new List<DiscoveredPage>();

        var result = SeedGenerator.Generate(options, content, components, pages);

        Assert.True(result);
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "sites", "test-seed", "notion-seed")));
    }

    [Fact]
    public void Generate_NotionSource_WritesDefaultDatabaseMap()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-map",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var result = SeedGenerator.Generate(options, new ExtractedContent(), [], []);

        Assert.True(result);
        var mapPath = Path.Combine(_tempDir, "sites", "test-map", "notion-seed", "notion-database-map.yaml");
        Assert.True(File.Exists(mapPath), $"Expected default database map at {mapPath}");
        var map = File.ReadAllText(mapPath);
        Assert.Contains("databases:", map);
        Assert.Contains("  pages:", map);
        Assert.Contains("    seed: pages.json", map);
        Assert.Contains("    collection: page", map);
        Assert.Contains("    databaseId: \"\"", map);
        Assert.Contains("  posts:", map);
        Assert.Contains("    seed: posts.json", map);
        Assert.Contains("  companies:", map);
        Assert.Contains("    seed: companies.json", map);
        Assert.Contains("  services:", map);
        Assert.Contains("    seed: services.json", map);
        Assert.Contains("    uniqueField: Slug", map);
    }

    [Fact]
    public void Generate_JsonSource_WritesToData()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-json",
            RootDir = _tempDir,
            ContentSource = "json"
        };

        var content = new ExtractedContent();
        var components = new List<DiscoveredComponent>();
        var pages = new List<DiscoveredPage>();

        var result = SeedGenerator.Generate(options, content, components, pages);

        Assert.True(result);
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "sites", "test-json", "data")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "test-json", "data", "pages.json")));
        Assert.False(File.Exists(Path.Combine(_tempDir, "sites", "test-json", "data", "notion-database-map.yaml")));
    }

    [Fact]
    public void Generate_YamlSource_WritesYamlFilesToData()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-yaml",
            RootDir = _tempDir,
            ContentSource = "yaml"
        };

        var content = new ExtractedContent
        {
            Pages = [new PageRecord { Title = "Home", Slug = "", Type = "Home" }],
            Services = [new ServiceRecord { Title = "Consulting", Slug = "consulting" }]
        };

        var result = SeedGenerator.Generate(options, content, [], []);

        Assert.True(result);
        var dataDir = Path.Combine(_tempDir, "sites", "test-yaml", "data");
        Assert.True(File.Exists(Path.Combine(dataDir, "pages.yaml")));
        Assert.True(File.Exists(Path.Combine(dataDir, "services.yaml")));
        Assert.False(File.Exists(Path.Combine(dataDir, "pages.json")));
        var yaml = File.ReadAllText(Path.Combine(dataDir, "pages.yaml"));
        Assert.Contains("title: \"Home\"", yaml);
    }

    [Fact]
    public void Generate_ContentSource_IsCaseInsensitive()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-yaml-case",
            RootDir = _tempDir,
            ContentSource = "YAML"
        };

        SeedGenerator.Generate(options, new ExtractedContent(), [], []);

        Assert.True(File.Exists(Path.Combine(_tempDir, "sites", "test-yaml-case", "data", "pages.yaml")));
        Assert.False(Directory.Exists(Path.Combine(_tempDir, "sites", "test-yaml-case", "notion-seed")));
    }

    [Fact]
    public void Generate_WritesAllJsonFiles()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-all",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var content = new ExtractedContent
        {
            Pages = [new PageRecord { Title = "About", Slug = "about" }],
            Sections = [new SectionRecord { SectionType = "hero" }],
            Posts = [new PostRecord { Title = "Post", Slug = "post" }],
            Companies = [new CompanyRecord { Title = "Co", Slug = "co" }],
            Services = [new ServiceRecord { Title = "Svc", Slug = "svc" }],
            Faqs = [new FaqRecord { Question = "Q?", Answer = "A." }]
        };

        var pages = new List<DiscoveredPage>
        {
            MakePage("index", ["img/logo.png"])
        };

        var result = SeedGenerator.Generate(options, content, [], pages);

        Assert.True(result);
        var seedDir = Path.Combine(_tempDir, "sites", "test-all", "notion-seed");
        Assert.True(File.Exists(Path.Combine(seedDir, "pages.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "sections.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "posts.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "companies.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "services.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "faqs.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "media.json")));
        Assert.True(File.Exists(Path.Combine(seedDir, "components.json")));
    }

    [Fact]
    public void Generate_PagesJson_ContainsRecords()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-pages",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var content = new ExtractedContent
        {
            Pages =
            [
                new PageRecord { Title = "Home", Slug = "", Type = "Home", Template = "index", Summary = "Welcome" },
                new PageRecord { Title = "About", Slug = "about", Type = "Page", Template = "page", Summary = "About us" }
            ]
        };

        var result = SeedGenerator.Generate(options, content, [], []);

        Assert.True(result);
        var json = File.ReadAllText(Path.Combine(_tempDir, "sites", "test-pages", "notion-seed", "pages.json"));
        Assert.Contains("\"Home\"", json);
        Assert.Contains("\"About\"", json);
        Assert.Contains("\"about\"", json);
    }

    [Fact]
    public void Generate_MediaJson_ContainsImageRecords()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-media",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var pages = new List<DiscoveredPage>
        {
            MakePage("index", ["img/hero.jpg", "img/logo.png"])
        };

        var result = SeedGenerator.Generate(options, new ExtractedContent(), [], pages);

        Assert.True(result);
        var json = File.ReadAllText(Path.Combine(_tempDir, "sites", "test-media", "notion-seed", "media.json"));
        Assert.Contains("hero.jpg", json);
        Assert.Contains("logo.png", json);
    }

    [Fact]
    public void Generate_MediaJson_SkipsNonImages()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-media2",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var pages = new List<DiscoveredPage>
        {
            MakePage("index", ["css/style.css", "js/app.js", "img/hero.jpg"])
        };

        var result = SeedGenerator.Generate(options, new ExtractedContent(), [], pages);

        Assert.True(result);
        var json = File.ReadAllText(Path.Combine(_tempDir, "sites", "test-media2", "notion-seed", "media.json"));
        Assert.Contains("hero.jpg", json);
        Assert.DoesNotContain("style.css", json);
        Assert.DoesNotContain("app.js", json);
    }

    [Fact]
    public void Generate_EmptyContent_GeneratesEmptyArrays()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-empty",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var result = SeedGenerator.Generate(options, new ExtractedContent(), [], []);

        Assert.True(result);
        var json = File.ReadAllText(Path.Combine(_tempDir, "sites", "test-empty", "notion-seed", "pages.json"));
        Assert.Contains("[", json);
        Assert.Contains("]", json);
    }

    [Fact]
    public void Generate_JsonStr_EscapesSpecialChars()
    {
        var options = new HtmlDemoImportOptions
        {
            InputPath = _tempDir,
            ThemeName = "test-escape",
            RootDir = _tempDir,
            ContentSource = "notion"
        };

        var content = new ExtractedContent
        {
            Pages = [new PageRecord { Title = "He said: \"Hello\"", Slug = "test" }]
        };

        SeedGenerator.Generate(options, content, [], []);

        var json = File.ReadAllText(Path.Combine(_tempDir, "sites", "test-escape", "notion-seed", "pages.json"));
        Assert.Contains("He said: \\\"Hello\\\"", json);
    }
}
