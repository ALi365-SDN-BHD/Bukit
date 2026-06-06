using Bukit.Cli.Intent;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class IntentLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public IntentLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "bukit-loader-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_tempDir, recursive: true);
    }

    [Fact]
    public void Load_ValidMinimalIntent_ReturnsSiteIntent()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test-site
  title: Test Site
  base_url: /
content:
  provider: markdown
  markdown:
    dir: content
theme:
  name: starter
""");

        var intent = IntentLoader.Load(path);

        Assert.Equal("test-site", intent.Site.Name);
        Assert.Equal("Test Site", intent.Site.Title);
        Assert.Equal("/", intent.Site.BaseUrl);
        Assert.Equal("markdown", intent.Content.Provider);
        Assert.Equal("content", intent.Content.Markdown?.Dir);
        Assert.Equal("starter", intent.Theme.Name);
    }

    [Fact]
    public void Load_ValidNotionIntent_ReturnsSiteIntent()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: notion-site
  title: Notion Site
  base_url: /
content:
  provider: notion
  notion:
    database_id: abc-123
    field_policy:
      mode: whitelist
      allowed:
        - cover
        - tags
theme:
  name: starter
""");

        var intent = IntentLoader.Load(path);

        Assert.Equal("notion", intent.Content.Provider);
        Assert.Equal("abc-123", intent.Content.Notion?.DatabaseId);
        Assert.Equal("whitelist", intent.Content.Notion?.FieldPolicy.Mode);
        Assert.Equal(2, intent.Content.Notion?.FieldPolicy.Allowed?.Count);
    }

    [Fact]
    public void Load_WithLanguages_ReturnsLanguages()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
  base_url: /
languages:
  default: zh-CN
  supported:
    - zh-CN
    - en-US
content:
  provider: markdown
theme:
  name: starter
""");

        var intent = IntentLoader.Load(path);

        Assert.NotNull(intent.Languages);
        Assert.Equal("zh-CN", intent.Languages.Default);
        Assert.Equal(2, intent.Languages.Supported.Count);
    }

    [Fact]
    public void Load_WithFeatures_ReturnsFeatures()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: markdown
theme:
  name: starter
features:
  sitemap: true
  rss: true
  search: false
""");

        var intent = IntentLoader.Load(path);

        Assert.NotNull(intent.Features);
        Assert.True(intent.Features.Sitemap);
        Assert.True(intent.Features.Rss);
        Assert.False(intent.Features.Search);
    }

    [Fact]
    public void Load_WithDeployment_ReturnsDeployment()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: markdown
theme:
  name: starter
deployment:
  target: github-pages
""");

        var intent = IntentLoader.Load(path);

        Assert.NotNull(intent.Deployment);
        Assert.Equal("github-pages", intent.Deployment.Target);
    }

    [Fact]
    public void Load_FileNotFound_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "nonexistent.yaml");

        var ex = Assert.Throws<InvalidOperationException>(() => IntentLoader.Load(path));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Load_InvalidYaml_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, "::: not yaml :::");

        Assert.ThrowsAny<Exception>(() => IntentLoader.Load(path));
    }

    [Fact]
    public void Load_EmptyFile_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, "");

        var ex = Assert.Throws<InvalidOperationException>(() => IntentLoader.Load(path));
        Assert.Contains("root must be a mapping", ex.Message);
    }

    [Fact]
    public void Load_MissingSiteSection_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
content:
  provider: markdown
theme:
  name: starter
""");

        var ex = Assert.Throws<InvalidOperationException>(() => IntentLoader.Load(path));
        Assert.Contains("site", ex.Message);
    }

    [Fact]
    public void Load_MissingContentSection_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
  base_url: /
theme:
  name: starter
""");

        var ex = Assert.Throws<InvalidOperationException>(() => IntentLoader.Load(path));
        Assert.Contains("content", ex.Message);
    }

    [Fact]
    public void Load_MissingThemeSection_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: markdown
""");

        var ex = Assert.Throws<InvalidOperationException>(() => IntentLoader.Load(path));
        Assert.Contains("theme", ex.Message);
    }

    [Fact]
    public void Load_MissingSiteName_ThrowsInvalidOperationException()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  title: Test
  base_url: /
content:
  provider: markdown
theme:
  name: starter
""");

        var ex = Assert.Throws<InvalidOperationException>(() => IntentLoader.Load(path));
        Assert.Contains("name", ex.Message);
    }

    [Fact]
    public void Load_BaseUrlDefaultsToSlash()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
content:
  provider: markdown
theme:
  name: starter
""");

        var intent = IntentLoader.Load(path);

        Assert.Equal("/", intent.Site.BaseUrl);
    }

    [Fact]
    public void Load_MarkdownDirDefaultsToContent()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: markdown
theme:
  name: starter
""");

        var intent = IntentLoader.Load(path);

        Assert.Equal("markdown", intent.Content.Provider);
        Assert.Equal("content", intent.Content.Markdown?.Dir);
    }

    [Fact]
    public void Load_NotionFieldPolicyDefaults()
    {
        var path = Path.Combine(_tempDir, "intent.yaml");
        File.WriteAllText(path, """
site:
  name: test
  title: Test
  base_url: /
content:
  provider: notion
  notion:
    database_id: abc
theme:
  name: starter
""");

        var intent = IntentLoader.Load(path);

        Assert.Equal("notion", intent.Content.Provider);
        Assert.Equal("whitelist", intent.Content.Notion?.FieldPolicy.Mode);
    }
}
