using System.Text;
using Bukit.Labs.Cli.Commands;
using YamlDotNet.RepresentationModel;
using Xunit;

namespace Bukit.Labs.Cli.Tests;

public sealed class CloneYamlWriterTests : IDisposable
{
    private readonly string _tempDir;

    public CloneYamlWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-labs-clone-yaml-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void EnsureSourcesConfig_WhenSiteYamlMissing_ReturnsFalseAndWarning()
    {
        var warnings = new List<string>();

        var result = CloneYamlWriter.EnsureSourcesConfig(
            _tempDir,
            "clone-kit",
            "Acme",
            new CloneTokens { Primary = "#123456", Accent = "#abcdef" },
            warnings);

        Assert.False(result);
        Assert.Contains("site.yaml not found; skipped content source configuration.", warnings);
    }

    [Fact]
    public void EnsureSourcesConfig_UpdatesThemeParamsAndMarkdownSources()
    {
        File.WriteAllText(Path.Combine(_tempDir, "site.yaml"), """
content:
  provider: notion
  sources:
    - name: content
      type: notion
    - type: markdown
      name: modules
      mode: data
      collection: legacy-modules
theme:
  name: old-theme
""");

        var warnings = new List<string>();

        var result = CloneYamlWriter.EnsureSourcesConfig(
            _tempDir,
            "clone-kit",
            "Acme",
            new CloneTokens { Primary = "#123456", Accent = "#abcdef" },
            warnings);

        Assert.True(result);
        Assert.Empty(warnings);

        var yaml = File.ReadAllText(Path.Combine(_tempDir, "site.yaml"));
        Assert.Contains("provider: sources", yaml, StringComparison.Ordinal);
        Assert.Contains("- type: markdown", yaml, StringComparison.Ordinal);
        Assert.Contains("name: content", yaml, StringComparison.Ordinal);
        Assert.Contains("name: modules", yaml, StringComparison.Ordinal);
        Assert.Contains("dir: content", yaml, StringComparison.Ordinal);
        Assert.Contains("dir: data", yaml, StringComparison.Ordinal);
        Assert.Contains("defaultType: page", yaml, StringComparison.Ordinal);
        Assert.Contains("defaultType: module", yaml, StringComparison.Ordinal);
        Assert.Contains("name: clone-kit", yaml, StringComparison.Ordinal);
        Assert.Contains("brand: Acme", yaml, StringComparison.Ordinal);
        Assert.Contains("footer_text: Acme", yaml, StringComparison.Ordinal);
        Assert.Contains("primary_color: '#123456'", yaml, StringComparison.Ordinal);
        Assert.Contains("accent_color: '#abcdef'", yaml, StringComparison.Ordinal);

        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);
        var content = Assert.IsType<YamlMappingNode>(root.Children[new YamlScalarNode("content")]);
        var sources = Assert.IsType<YamlSequenceNode>(content.Children[new YamlScalarNode("sources")]);
        var contentSource = Assert.Single(sources.Children.OfType<YamlMappingNode>(), source =>
            CloneYamlWriter.GetScalar(source, "name") == "content");
        var dataSource = Assert.Single(sources.Children.OfType<YamlMappingNode>(), source =>
            CloneYamlWriter.GetScalar(source, "name") == "modules");

        Assert.Equal("page", CloneYamlWriter.GetScalar(contentSource, "collection"));
        Assert.False(dataSource.Children.ContainsKey(new YamlScalarNode("collection")));
    }

    [Fact]
    public void AppendBlockScalar_AndYamlScalar_FormatValuesForYaml()
    {
        var sb = new StringBuilder();

        CloneYamlWriter.AppendBlockScalar(sb, "body", "line one\nline two");

        Assert.Equal("'Bob''s'", CloneYamlWriter.YamlScalar("Bob's"));
        Assert.Contains("body: |-", sb.ToString(), StringComparison.Ordinal);
        Assert.Contains("  line one", sb.ToString(), StringComparison.Ordinal);
        Assert.Contains("  line two", sb.ToString(), StringComparison.Ordinal);
    }
}
