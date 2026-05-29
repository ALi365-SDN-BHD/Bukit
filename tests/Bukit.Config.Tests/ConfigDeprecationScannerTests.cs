using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigDeprecationScannerTests : IDisposable
{
    private readonly string _dir;
    private readonly string _configPath;

    public ConfigDeprecationScannerTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bukit-config-deprecated-" + Guid.NewGuid().ToString("N"));
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
    public void Scan_ReportsDeprecatedRssPluginToggle()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                        plugins:
                                          rss:
                                            enabled: true
                                      content:
                                        provider: markdown
                                      """);

        var warnings = ConfigDeprecationScanner.ScanFile(_configPath);

        var warning = Assert.Single(warnings);
        Assert.Contains("site.plugins.rss", warning.Message, StringComparison.Ordinal);
        Assert.Contains("site.plugins.feed", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_ReportsDeprecatedRssMode()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                        rssMode: root
                                      content:
                                        provider: markdown
                                      """);

        var warnings = ConfigDeprecationScanner.ScanFile(_configPath);

        var warning = Assert.Single(warnings);
        Assert.Contains("site.rssMode", warning.Message, StringComparison.Ordinal);
        Assert.Contains("site.feed.formats", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_ReportsDeprecatedOutputPath()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                      outputPath: /custom/
                                      content:
                                        provider: markdown
                                      """);

        var warnings = ConfigDeprecationScanner.ScanFile(_configPath);

        var warning = Assert.Single(warnings);
        Assert.Contains("outputPath", warning.Message, StringComparison.Ordinal);
        Assert.Contains("route.outputPath", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_ReportsDeprecatedCollectionRss()
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
                                        provider: markdown
                                      """);

        var warnings = ConfigDeprecationScanner.ScanFile(_configPath);

        Assert.NotEmpty(warnings);
        var rssWarning = warnings.FirstOrDefault(w => w.Message.Contains("collections.posts.rss", StringComparison.Ordinal));
        Assert.NotNull(rssWarning);
        Assert.Contains("collections.posts.feed", rssWarning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_ReportsDeprecatedSingularCollection()
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
                                        provider: markdown
                                      """);

        var warnings = ConfigDeprecationScanner.ScanFile(_configPath);

        var warning = Assert.Single(warnings);
        Assert.Contains("site.collection", warning.Message, StringComparison.Ordinal);
        Assert.Contains("site.collections", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_ReportsDeprecatedNotionRootPageId()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                      content:
                                        provider: notion
                                        notion:
                                          databaseId: abc123
                                          rootPageId: xyz789
                                      """);

        var warnings = ConfigDeprecationScanner.ScanFile(_configPath);

        var notionWarning = warnings.FirstOrDefault(w => w.Message.Contains("rootPageId", StringComparison.Ordinal));
        Assert.NotNull(notionWarning);
        Assert.Contains("content.notion.rootPageId", notionWarning.Message, StringComparison.Ordinal);
        Assert.Contains("rootBlockId", notionWarning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_ReportsDeprecatedNotionProvider()
    {
        File.WriteAllText(_configPath, """
                                      site:
                                        name: test
                                        title: Test
                                      content:
                                        provider: notion
                                      """);

        var warnings = ConfigDeprecationScanner.ScanFile(_configPath);

        var providerWarning = warnings.FirstOrDefault(w => w.Message.Contains("content.provider", StringComparison.Ordinal));
        Assert.NotNull(providerWarning);
        Assert.Contains("content.sources", providerWarning.Message, StringComparison.Ordinal);
    }
}
