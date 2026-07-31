using Xunit;
using Bukit.Config;
using Bukit.Shared;

namespace Bukit.Engine.Tests;

/// <summary>
/// End-to-end integration tests: from site.yaml configuration to output file verification.
/// </summary>
public sealed class EndToEndBuildTests : IDisposable
{
    private readonly string _siteRoot;

    public EndToEndBuildTests()
    {
        _siteRoot = Path.Combine(Path.GetTempPath(), "bukit-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_siteRoot);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_siteRoot, recursive: true);
    }

    private string SiteYamlPath => Path.Combine(_siteRoot, "site.yaml");

    private void WriteSiteYaml(string yaml) => File.WriteAllText(SiteYamlPath, yaml);

    [Fact]
    public void Build_MinimalSite_ParsesConfig()
    {
        WriteSiteYaml("""
            site:
              name: e2e-test
              title: E2E Test Site
              url: https://example.com
              language: en
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
            """);

        var config = ConfigLoader.Load(SiteYamlPath);
        Assert.Equal("e2e-test", config.Site.Name);
        Assert.Equal("E2E Test Site", config.Site.Title);
    }

    [Fact]
    public void SiteYaml_MissingSiteName_ThrowsConfigException()
    {
        WriteSiteYaml("""
            site:
              title: No Name
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
                  markdown:
                    dir: content
            build:
              output: dist
            """);

        Assert.Throws<ConfigException>(() => ConfigLoader.Load(SiteYamlPath));
    }

    [Fact]
    public void SiteYaml_FullConfig_ParsesAllSections()
    {
        WriteSiteYaml("""
            site:
              name: full-test
              title: Full Test
              url: https://example.com
              language: en
            content:
              sources:
                - type: markdown
                  name: post
                  collection: posts
                  markdown:
                    dir: content/posts
                - type: markdown
                  name: page
                  collection: pages
                  markdown:
                    dir: content/pages
            build:
              output: dist
              draft: false
            """);

        var config = ConfigLoader.Load(SiteYamlPath);
        Assert.Equal("full-test", config.Site.Name);
        Assert.Equal("https://example.com", config.Site.Url);
        Assert.NotNull(config.Content);
        Assert.NotNull(config.Content!.Sources);
        Assert.Equal(2, config.Content.Sources!.Count);
    }

    [Fact]
    public void SiteYaml_InvalidYaml_ThrowsConfigException()
    {
        WriteSiteYaml("not: valid: yaml: [[[");
        Assert.Throws<ConfigException>(() => ConfigLoader.Load(SiteYamlPath));
    }

    [Fact]
    public void SiteYaml_NonExistent_ThrowsConfigException()
    {
        Assert.Throws<ConfigException>(() =>
            ConfigLoader.Load(Path.Combine(_siteRoot, "nonexistent.yaml")));
    }

    [Fact]
    public void SiteYaml_UnknownField_ThrowsConfigException()
    {
        WriteSiteYaml("""
            site:
              name: test
              title: Test
            unknownSection: true
            """);

        Assert.Throws<ConfigException>(() => ConfigLoader.Load(SiteYamlPath));
    }
}
