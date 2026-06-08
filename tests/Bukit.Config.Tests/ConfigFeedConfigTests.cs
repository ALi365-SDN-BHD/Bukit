using Bukit.Config;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class ConfigFeedConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bukit-config-feed-tests-" + Guid.NewGuid().ToString("N"));

    public ConfigFeedConfigTests()
    {
        Directory.CreateDirectory(_dir);
    }

    [Fact]
    public void Load_FeedConfig_ReadsFormatsLimitAndPath()
    {
        var path = Path.Combine(_dir, "site.yaml");
        File.WriteAllText(path, """
            site:
              name: myblog
              title: My Blog
              feed:
                formats: [rss, atom, json]
                limit: 7
                path: feeds
            content:
              sources:
                - type: markdown
                  markdown:
                    dir: content
            """);

        var config = ConfigLoader.Load(path);

        Assert.Equal(["rss", "atom", "json"], config.Site.Feed.Formats);
        Assert.Equal(7, config.Site.Feed.Limit);
        Assert.Equal("feeds", config.Site.Feed.Path);
    }

    public void Dispose()
    {
        TestCleanup.DeleteDirectory(_dir, recursive: true);
    }
}
