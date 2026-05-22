using Xunit;
using Bukit.Theme;

namespace Bukit.Theme.Tests;

public sealed class ThemeManifestLoaderTests : IDisposable
{
    private readonly string _testDir;

    public ThemeManifestLoaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-theme-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
    }

    [Fact]
    public void Load_NoThemeYaml_ReturnsNull()
    {
        var result = ThemeManifestLoader.Load(_testDir);
        Assert.Null(result);
    }

    [Fact]
    public void Load_ValidThemeYaml_ReturnsManifest()
    {
        File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), """
            name: test-theme
            version: 1.0.0
            description: A test theme
            capabilities:
              seo: true
            """);

        var result = ThemeManifestLoader.Load(_testDir);
        Assert.NotNull(result);
        Assert.Equal("test-theme", result.Name);
        Assert.Equal("1.0.0", result.Version);
        Assert.Equal("A test theme", result.Description);
    }

    [Fact]
    public void Load_WithSections_ParsesCorrectly()
    {
        File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), """
            name: test-theme
            version: 1.0.0
            sections:
              hero:
                template: sections/hero/hero.html
                description: Hero section
              cta:
                template: sections/cta/cta.html
                description: Call to action
            """);

        var result = ThemeManifestLoader.Load(_testDir);
        Assert.NotNull(result);
        Assert.NotNull(result.Sections);
        Assert.Equal(2, result.Sections.Count);
        Assert.True(result.Sections.ContainsKey("hero"));
        Assert.Equal("Hero section", result.Sections["hero"].Description);
    }

    [Fact]
    public void Load_WithComponents_ParsesCorrectly()
    {
        File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), """
            name: test-theme
            version: 1.0.0
            components:
              card:
                template: components/cards/card.html
                props:
                  title: string
                  url: string
            """);

        var result = ThemeManifestLoader.Load(_testDir);
        Assert.NotNull(result);
        Assert.NotNull(result.Components);
        Assert.Single(result.Components);
        Assert.True(result.Components.ContainsKey("card"));
    }

    [Fact]
    public void Load_WithExtends_ParsesCorrectly()
    {
        File.WriteAllText(Path.Combine(_testDir, "theme.yaml"), """
            name: test-theme
            version: 1.0.0
            extends: parent-theme
            """);

        var result = ThemeManifestLoader.Load(_testDir);
        Assert.NotNull(result);
        Assert.Equal("parent-theme", result.Extends);
    }
}
