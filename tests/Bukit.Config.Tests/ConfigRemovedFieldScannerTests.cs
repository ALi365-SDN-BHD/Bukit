using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigRemovedFieldScannerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _configPath;

    public ConfigRemovedFieldScannerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bukit-config-removed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _configPath = Path.Combine(_dir, "site.yaml");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Reject_ThrowsOnDeprecatedRssPluginToggle()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                        plugins:
                                          rss:
                                            enabled: true
                                      content:
                                        sources:
                                          - type: markdown
                                            markdown:
                                              dir: content
                                      """);

        var ex = Assert.Throws<ConfigException>(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Equal(DiagnosticCode.ConfigRemovedField, ex.Code);
        Assert.Contains("site.plugins.rss", ex.Message, StringComparison.Ordinal);
        Assert.Contains("site.plugins.feed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_ThrowsOnDeprecatedRssMode()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                        rssMode: root
                                      content:
                                        sources:
                                          - type: markdown
                                            markdown:
                                              dir: content
                                      """);

        var ex = Assert.Throws<ConfigException>(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Equal(DiagnosticCode.ConfigRemovedField, ex.Code);
        Assert.Contains("site.rssMode", ex.Message, StringComparison.Ordinal);
        Assert.Contains("site.feed.formats", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_ThrowsOnDeprecatedOutputPath()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                      outputPath: /custom/
                                      content:
                                        sources:
                                          - type: markdown
                                            markdown:
                                              dir: content
                                      """);

        var ex = Assert.Throws<ConfigException>(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Equal(DiagnosticCode.ConfigRemovedField, ex.Code);
        Assert.Contains("outputPath", ex.Message, StringComparison.Ordinal);
        Assert.Contains("route.url", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_ThrowsOnDeprecatedCollectionRss()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                        collections:
                                          posts:
                                            permalink: /posts/{slug}/
                                            template: pages/post.html
                                            rss: true
                                      content:
                                        sources:
                                          - type: markdown
                                            markdown:
                                              dir: content
                                      """);

        var ex = Assert.Throws<ConfigException>(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Equal(DiagnosticCode.ConfigRemovedField, ex.Code);
        Assert.Contains("collections.posts.rss", ex.Message, StringComparison.Ordinal);
        Assert.Contains("collections.posts.feed", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_ThrowsOnDeprecatedSingularCollection()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                        collection:
                                          posts:
                                            permalink: /posts/{slug}/
                                            template: pages/post.html
                                      content:
                                        sources:
                                          - type: markdown
                                            markdown:
                                              dir: content
                                      """);

        var ex = Assert.Throws<ConfigException>(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Equal(DiagnosticCode.ConfigRemovedField, ex.Code);
        Assert.Contains("site.collection", ex.Message, StringComparison.Ordinal);
        Assert.Contains("site.collections", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_ThrowsOnDeprecatedNotionRootPageId()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                      content:
                                        sources:
                                          - type: notion
                                            notion:
                                              databaseId: abc123
                                              rootPageId: xyz789
                                      """);

        var ex = Assert.Throws<ConfigException>(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Equal(DiagnosticCode.ConfigRemovedField, ex.Code);
        Assert.Contains("content.sources[0].notion.rootPageId", ex.Message, StringComparison.Ordinal);
        Assert.Contains("rootBlockId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_ThrowsOnDeprecatedNotionProvider()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                      content:
                                        provider: notion
                                        notion:
                                          databaseId: test-db
                                      """);

        var ex = Assert.Throws<ConfigException>(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Equal(DiagnosticCode.ConfigRemovedField, ex.Code);
        Assert.Contains("content.provider", ex.Message, StringComparison.Ordinal);
        Assert.Contains("content.sources", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reject_DoesNotThrowForValidConfig()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                      content:
                                        sources:
                                          - type: markdown
                                            markdown:
                                              dir: content
                                      """);

        var exception = Record.Exception(() => ConfigRemovedFieldScanner.RejectRemovedFields(_configPath));
        Assert.Null(exception);
    }
}
