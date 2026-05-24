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
}
