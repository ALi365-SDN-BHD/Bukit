using System.Text;
using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class CloneYamlWriterTests : IDisposable
{
    private readonly string _testDir;

    public CloneYamlWriterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "bukit-yaml-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public void YamlScalar_SimpleValue_WrapsInSingleQuotes()
    {
        var result = CloneYamlWriter.YamlScalar("hello");
        Assert.Equal("'hello'", result);
    }

    [Fact]
    public void YamlScalar_WithSingleQuotes_EscapesByDoubling()
    {
        var result = CloneYamlWriter.YamlScalar("it's a test");
        Assert.Equal("'it''s a test'", result);
    }

    [Fact]
    public void YamlScalar_WithSpecialChars_QuotedCorrectly()
    {
        var result = CloneYamlWriter.YamlScalar("value: with colon");
        Assert.Equal("'value: with colon'", result);
    }

    [Fact]
    public void YamlScalar_EmptyString_QuotedEmpty()
    {
        var result = CloneYamlWriter.YamlScalar("");
        Assert.Equal("''", result);
    }

    [Fact]
    public void AppendBlockScalar_SingleLine_AppendsCorrectly()
    {
        var sb = new StringBuilder();
        CloneYamlWriter.AppendBlockScalar(sb, "content", "Hello, world!");

        var result = sb.ToString();
        Assert.Contains("content: |-", result);
        Assert.Contains("  Hello, world!", result);
    }

    [Fact]
    public void AppendBlockScalar_MultiLine_IndentsEachLine()
    {
        var sb = new StringBuilder();
        CloneYamlWriter.AppendBlockScalar(sb, "description", "Line 1\nLine 2\nLine 3");

        var result = sb.ToString();
        var lines = result.Split('\n');
        Assert.Contains("description: |-", lines[0]);
        Assert.Contains("  Line 1", lines[1]);
        Assert.Contains("  Line 2", lines[2]);
        Assert.Contains("  Line 3", lines[3]);
    }

    [Fact]
    public void AppendBlockScalar_EmptyValue_OutputsIndentedBlank()
    {
        var sb = new StringBuilder();
        CloneYamlWriter.AppendBlockScalar(sb, "empty", "");

        var result = sb.ToString();
        Assert.Contains("empty: |-", result);
    }

    [Fact]
    public void AppendBlockScalar_NullValue_OutputsIndentedBlank()
    {
        var sb = new StringBuilder();
        CloneYamlWriter.AppendBlockScalar(sb, "nullcontent", null!);

        var result = sb.ToString();
        Assert.Contains("nullcontent: |-", result);
    }

    [Fact]
    public void EnsureSourcesConfig_WithValidSiteYaml_ConfiguresContentSources()
    {
        var siteYamlPath = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYamlPath, """
        site:
          name: testsite
          title: Test Site
        """);

        var warnings = new List<string>();
        var tokens = new CloneTokens
        {
            Primary = "#00ff00",
            Accent = "#ff0000"
        };

        var result = CloneYamlWriter.EnsureSourcesConfig(_testDir, "my-theme", "MyBrand", tokens, warnings);

        Assert.True(result);
        Assert.Empty(warnings);

        var updatedContent = File.ReadAllText(siteYamlPath);
        Assert.Contains("provider: sources", updatedContent);
        Assert.Contains("my-theme", updatedContent);
        Assert.Contains("MyBrand", updatedContent);
        Assert.Contains("primary_color", updatedContent);
        Assert.Contains("accent_color", updatedContent);
    }

    [Fact]
    public void EnsureSourcesConfig_MissingSiteYaml_ReturnsFalseWithWarning()
    {
        var warnings = new List<string>();
        var tokens = new CloneTokens();

        var result = CloneYamlWriter.EnsureSourcesConfig(_testDir, "theme", null, tokens, warnings);

        Assert.False(result);
        Assert.Single(warnings);
        Assert.Contains("not found", warnings[0]);
    }

    [Fact]
    public void EnsureSourcesConfig_WithoutBrandOrColors_StillConfiguresTheme()
    {
        var siteYamlPath = Path.Combine(_testDir, "site.yaml");
        File.WriteAllText(siteYamlPath, """
        site:
          name: test
          title: Test
        """);

        var warnings = new List<string>();
        var tokens = new CloneTokens();

        var result = CloneYamlWriter.EnsureSourcesConfig(_testDir, "bare-theme", null, tokens, warnings);

        Assert.True(result);
        var content = File.ReadAllText(siteYamlPath);
        Assert.Contains("bare-theme", content);
    }
}
