using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneResearchWriterTests : IDisposable
{
    private readonly string _testDir;

    public CloneResearchWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-research-writer-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_testDir, recursive: true);
    }

    [Fact]
    public void WriteTo_CreatesBaseResearchFiles()
    {
        var tokens = new CloneTokens();
        var page = new ClonePageInfo { Title = "Test Page" };
        var sections = Array.Empty<CloneSectionInfo>();
        var assets = Array.Empty<CloneAsset>();
        var behaviors = (CloneBehaviors?)null;
        var assetMap = new Dictionary<string, string>();

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors, assetMap);

        Assert.True(File.Exists(Path.Combine(_testDir, "docs", "research", "DESIGN_TOKENS.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "docs", "research", "PAGE_TOPOLOGY.md")));
        Assert.True(File.Exists(Path.Combine(_testDir, "docs", "research", "BEHAVIORS.md")));
    }

    [Fact]
    public void WriteTo_DesignTokensFile_ContainsTokenHeaders()
    {
        var tokens = new CloneTokens { Bg = "#ffffff", Primary = "#0b5fff" };
        var page = new ClonePageInfo { Title = "Page" };
        var sections = Array.Empty<CloneSectionInfo>();
        var assets = Array.Empty<CloneAsset>();
        var assetMap = new Dictionary<string, string>();

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors: null, assetMap);

        var content = File.ReadAllText(Path.Combine(_testDir, "docs", "research", "DESIGN_TOKENS.md"));
        Assert.Contains("# Design Tokens", content);
        Assert.Contains("`#ffffff`", content);
        Assert.Contains("`#0b5fff`", content);
    }

    [Fact]
    public void WriteTo_PageTopologyFile_ContainsPageTitle()
    {
        var tokens = new CloneTokens();
        var page = new ClonePageInfo { Title = "My Cloned Page", Url = "https://example.com" };
        var sections = Array.Empty<CloneSectionInfo>();
        var assets = Array.Empty<CloneAsset>();
        var assetMap = new Dictionary<string, string>();

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors: null, assetMap);

        var content = File.ReadAllText(Path.Combine(_testDir, "docs", "research", "PAGE_TOPOLOGY.md"));
        Assert.Contains("# Page Topology", content);
        Assert.Contains("My Cloned Page", content);
        Assert.Contains("https://example.com", content);
    }

    [Fact]
    public void WriteTo_BehaviorsFile_ExistsEvenWithNoBehaviors()
    {
        var tokens = new CloneTokens();
        var page = new ClonePageInfo { Title = "Test" };
        var sections = Array.Empty<CloneSectionInfo>();
        var assets = Array.Empty<CloneAsset>();
        var assetMap = new Dictionary<string, string>();

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors: null, assetMap);

        var content = File.ReadAllText(Path.Combine(_testDir, "docs", "research", "BEHAVIORS.md"));
        Assert.Contains("# Behaviors", content);
    }

    [Fact]
    public void WriteTo_WithBehaviors_ContainsBehaviorJson()
    {
        var tokens = new CloneTokens();
        var page = new ClonePageInfo { Title = "Test" };
        var sections = Array.Empty<CloneSectionInfo>();
        var assets = Array.Empty<CloneAsset>();
        var behaviors = new CloneBehaviors { StickyHeader = true, DarkModeToggle = true };
        var assetMap = new Dictionary<string, string>();

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors, assetMap);

        var content = File.ReadAllText(Path.Combine(_testDir, "docs", "research", "BEHAVIORS.md"));
        Assert.Contains("stickyHeader", content);
        Assert.Contains("darkModeToggle", content);
    }

    [Fact]
    public void WriteTo_WithSections_CreatesSectionSpecFiles()
    {
        var tokens = new CloneTokens();
        var page = new ClonePageInfo { Title = "Page with sections" };
        var sections = new CloneSectionInfo[]
        {
            new() { Id = "hero", Type = "hero", Title = "Hero Section" },
            new() { Id = "features", Type = "features", Title = "Features" }
        };
        var assets = Array.Empty<CloneAsset>();
        var assetMap = new Dictionary<string, string>();

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors: null, assetMap);

        var componentsDir = Path.Combine(_testDir, "docs", "research", "components");
        Assert.True(Directory.Exists(componentsDir));
        var files = Directory.GetFiles(componentsDir, "*.spec.md");
        Assert.Equal(2, files.Length);
    }

    [Fact]
    public void WriteTo_SectionSpecFiles_ContainSectionDetails()
    {
        var tokens = new CloneTokens();
        var page = new ClonePageInfo { Title = "Page" };
        var sections = new CloneSectionInfo[]
        {
            new() { Id = "hero", Type = "hero", Title = "Hero Section", Heading = "Welcome" }
        };
        var assets = Array.Empty<CloneAsset>();
        var assetMap = new Dictionary<string, string>();

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors: null, assetMap);

        var specPath = Path.Combine(_testDir, "docs", "research", "components", "001-hero.spec.md");
        Assert.True(File.Exists(specPath));
        var content = File.ReadAllText(specPath);
        Assert.Contains("# Section 001:", content);
        Assert.Contains("Hero Section", content);
        Assert.Contains("`hero`", content);
    }

    [Fact]
    public void WriteTo_AssetMapReflectedInTopology()
    {
        var tokens = new CloneTokens();
        var page = new ClonePageInfo { Title = "Test" };
        var sections = Array.Empty<CloneSectionInfo>();
        var assets = new CloneAsset[]
        {
            new() { Type = "image", Src = "https://example.com/img.png" }
        };
        var assetMap = new Dictionary<string, string>
        {
            ["https://example.com/img.png"] = "/assets/images/img.png"
        };

        CloneResearchWriter.WriteTo(_testDir, tokens, page, sections, assets, behaviors: null, assetMap);

        var content = File.ReadAllText(Path.Combine(_testDir, "docs", "research", "PAGE_TOPOLOGY.md"));
        Assert.Contains("/assets/images/img.png", content);
    }
}
