using Bukit.Cli.Commands.DocsCheck;
using Xunit;

namespace Bukit.Cli.Tests.Commands.DocsCheck;

public class ConfigFieldExtractorTests
{
    [Fact]
    public void ExtractYamlReferencesFromDoc_ShouldNotExtractFromProse()
    {
        var text = """
            The site.name field is used to set the site name.
            You can configure content.sources to add markdown sources.
            """;

        var refs = ConfigFieldExtractor.ExtractYamlReferencesFromDoc(text);

        Assert.Empty(refs);
    }

    [Fact]
    public void ExtractYamlReferencesFromDoc_ShouldNotExtractRemovedFieldsFromProse()
    {
        var text = """
            ## Migration Notes

            The old `legacy content provider field` field is removed in Bukit 1.0.
            Use `content.sources` instead.

            ```yaml
            content:
              sources:
                - type: markdown
                  name: page
                  collection: page
            ```
            """;

        var refs = ConfigFieldExtractor.ExtractYamlReferencesFromDoc(text);

        // Should not extract legacy content provider field from prose — YAML keys are multiline w/o dots
        Assert.DoesNotContain("legacy content provider field", refs);
    }

    [Fact]
    public void ExtractYamlReferencesFromDoc_ShouldExtractFromConfigFieldTables()
    {
        var text = """
            | Field | Description |
            |---|---|
            | `site.name` | Site name |
            | `content.sources` | Content sources |
            | `build.output` | Output directory |
            """;

        var refs = ConfigFieldExtractor.ExtractYamlReferencesFromDoc(text);

        Assert.Contains("site.name", refs);
        Assert.Contains("content.sources", refs);
        Assert.Contains("build.output", refs);
    }

    [Fact]
    public void ExtractYamlReferencesFromDoc_ShouldExtractFromYamlBlocksWithInlinePaths()
    {
        var text = """
            ```yaml
            site.name: Override
            build.output: site
            ```
            """;

        var refs = ConfigFieldExtractor.ExtractYamlReferencesFromDoc(text);

        Assert.Contains("site.name", refs);
        Assert.Contains("build.output", refs);
    }

    [Fact]
    public void IsDynamicMapChild_ShouldReturnTrue_ForSiteMenusChild()
    {
        Assert.True(ConfigFieldExtractor.IsDynamicMapChild("site.menus.main"));
        Assert.True(ConfigFieldExtractor.IsDynamicMapChild("site.menus.footer"));
    }

    [Fact]
    public void IsDynamicMapChild_ShouldReturnTrue_ForThemeParamsChild()
    {
        Assert.True(ConfigFieldExtractor.IsDynamicMapChild("theme.params.brand"));
        Assert.True(ConfigFieldExtractor.IsDynamicMapChild("theme.params.color"));
    }

    [Fact]
    public void IsDynamicMapChild_ShouldReturnTrue_ForSitePluginsChild()
    {
        Assert.True(ConfigFieldExtractor.IsDynamicMapChild("site.plugins.feed"));
        Assert.True(ConfigFieldExtractor.IsDynamicMapChild("site.plugins.sitemap"));
    }

    [Fact]
    public void IsDynamicMapChild_ShouldReturnFalse_ForNonDynamicPath()
    {
        Assert.False(ConfigFieldExtractor.IsDynamicMapChild("site.name"));
        Assert.False(ConfigFieldExtractor.IsDynamicMapChild("build.output"));
        Assert.False(ConfigFieldExtractor.IsDynamicMapChild("content.sources"));
    }

    [Fact]
    public void ExtractYamlReferencesFromDoc_ShouldHandleMixedContent()
    {
        var text = """
            ```yaml
            site:
              name: test
            ```

            Some prose here.

            | Field | Description |
            |---|---|
            | `site.name` | Site name |
            | `build.output` | Output directory |
            """;

        var refs = ConfigFieldExtractor.ExtractYamlReferencesFromDoc(text);

        // Should extract from table even when YAML block has no inline dotted paths
        Assert.Contains("site.name", refs);
        Assert.Contains("build.output", refs);
    }

    [Fact]
    public void ExtractYamlReferencesFromDoc_ShouldHandleEmptyInput()
    {
        var text = "";

        var refs = ConfigFieldExtractor.ExtractYamlReferencesFromDoc(text);

        Assert.Empty(refs);
    }
}
