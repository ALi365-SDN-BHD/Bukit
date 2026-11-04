using Xunit;
using Bukit.Config;
using Bukit.Engine.Abstractions.Plugins;
using Bukit.Engine.Abstractions.Routing;
using Bukit.Routing;
using Bukit.Shared;

namespace Bukit.Engine.Tests;

public sealed class RobotsTxtWriterTests : IDisposable
{
    private readonly string _outputDir = Path.Combine(Path.GetTempPath(), "bukit-robots", Guid.NewGuid().ToString("N"));

    public RobotsTxtWriterTests()
    {
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose() => TestCleanup.DeleteDirectory(_outputDir, recursive: true);

    [Fact]
    public void AllowMode_AllowsEveryKnownCrawlerAndDropsRetiredNames()
    {
        var robots = Write(Geo("allow"));

        foreach (var crawler in AiCrawlerCatalog.All)
        {
            Assert.Equal("Allow: /", RuleFor(robots, crawler.UserAgent));
        }

        Assert.DoesNotContain("Claude-Web", robots, StringComparison.Ordinal);
        Assert.DoesNotContain("Anthropic-AI", robots, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FacebookBot", robots, StringComparison.Ordinal);
        Assert.StartsWith("User-agent: *" + Environment.NewLine + "Allow: /", robots, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockTrainingMode_BlocksTrainingButKeepsAiSearchAndUserFetch()
    {
        var robots = Write(Geo("block-training"));

        Assert.Equal("Disallow: /", RuleFor(robots, "GPTBot"));
        Assert.Equal("Disallow: /", RuleFor(robots, "ClaudeBot"));
        Assert.Equal("Disallow: /", RuleFor(robots, "Google-Extended"));
        Assert.Equal("Allow: /", RuleFor(robots, "OAI-SearchBot"));
        Assert.Equal("Allow: /", RuleFor(robots, "Claude-SearchBot"));
        Assert.Equal("Allow: /", RuleFor(robots, "PerplexityBot"));
        Assert.Equal("Allow: /", RuleFor(robots, "ChatGPT-User"));
        Assert.Equal("Allow: /", RuleFor(robots, "Claude-User"));
        Assert.Equal("Allow: /", RuleFor(robots, "Perplexity-User"));
    }

    [Fact]
    public void BlockMode_BlocksEveryKnownCrawlerExceptAllowListExceptions()
    {
        var robots = Write(Geo("block", allow: ["OAI-SearchBot"]));

        Assert.Equal("Allow: /", RuleFor(robots, "OAI-SearchBot"));
        foreach (var crawler in AiCrawlerCatalog.All.Where(x => x.UserAgent != "OAI-SearchBot"))
        {
            Assert.Equal("Disallow: /", RuleFor(robots, crawler.UserAgent));
        }
    }

    [Fact]
    public void AllowMode_BlockListEntriesAreBlocked()
    {
        var robots = Write(Geo("allow", block: ["GPTBot", "BadBot"]));

        Assert.Equal("Disallow: /", RuleFor(robots, "GPTBot"));
        Assert.Equal("Disallow: /", RuleFor(robots, "BadBot"));
        Assert.Equal("Allow: /", RuleFor(robots, "ClaudeBot"));
    }

    [Fact]
    public void SelectiveMode_AllowListAllowsOnlyListedCrawlers()
    {
        var robots = Write(Geo("selective", allow: ["OAI-SearchBot", "MyAgent"]));

        Assert.Equal("Allow: /", RuleFor(robots, "OAI-SearchBot"));
        Assert.Equal("Allow: /", RuleFor(robots, "MyAgent"));
        Assert.Equal("Disallow: /", RuleFor(robots, "GPTBot"));
        Assert.Equal("Disallow: /", RuleFor(robots, "PerplexityBot"));
    }

    [Fact]
    public void SelectiveMode_BlockListOnlyBlocksJustThoseCrawlers()
    {
        var robots = Write(Geo("selective", block: ["GPTBot"]));

        Assert.Equal("Disallow: /", RuleFor(robots, "GPTBot"));
        Assert.Null(RuleFor(robots, "ClaudeBot"));
        Assert.Equal(1, CountGroups(robots, "GPTBot"));
    }

    [Fact]
    public void CrawlerOnBothLists_IsBlockedOnce()
    {
        var rules = RobotsTxtWriter.BuildAiBotRules(Geo("selective", allow: ["GPTBot", " gptbot "], block: ["GPTBot"]));

        Assert.Single(rules, rule => rule.UserAgent.Equals("GPTBot", StringComparison.OrdinalIgnoreCase));
        Assert.False(rules.Single(rule => rule.UserAgent == "GPTBot").Allow);
    }

    [Fact]
    public void GeoDisabled_WritesNoAiGroups()
    {
        var robots = Write(Geo("block") with { Enabled = false });

        Assert.DoesNotContain("GPTBot", robots, StringComparison.Ordinal);
    }

    [Fact]
    public void RobotsTxtDisabled_WarnsThatAiPolicyIsInactive()
    {
        var logger = new RecordingLogger();
        RobotsTxtWriter.WriteIfRequested(Config(Geo("block-training"), robotsEnabled: false), _outputDir, "/", IndexableIndex(), logger: logger);

        Assert.False(File.Exists(Path.Combine(_outputDir, "robots.txt")));
        Assert.Contains(logger.Warnings, message =>
            message.Contains("event=robots.ai_policy_inactive reason=robots_txt_disabled", StringComparison.Ordinal));
    }

    [Fact]
    public void DefaultAllowPolicy_DoesNotWarnWhenRobotsTxtIsDisabled()
    {
        var logger = new RecordingLogger();
        RobotsTxtWriter.WriteIfRequested(Config(Geo("allow"), robotsEnabled: false), _outputDir, "/", IndexableIndex(), logger: logger);

        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public void StaticRobotsTxt_IsKeptAndPolicyIsReportedInactive()
    {
        File.WriteAllText(Path.Combine(_outputDir, "robots.txt"), "User-agent: *\nDisallow:\n");
        var logger = new RecordingLogger();
        RobotsTxtWriter.WriteIfRequested(Config(Geo("block")), _outputDir, "/", IndexableIndex(), logger: logger);

        Assert.Equal("User-agent: *\nDisallow:\n", File.ReadAllText(Path.Combine(_outputDir, "robots.txt")));
        Assert.Contains(logger.Infos, message =>
            message.Contains("event=robots.ai_policy_inactive reason=static_robots_txt", StringComparison.Ordinal));
    }

    private string Write(SeoGeoConfig geo)
    {
        RobotsTxtWriter.WriteIfRequested(Config(geo), _outputDir, "/", IndexableIndex());
        return File.ReadAllText(Path.Combine(_outputDir, "robots.txt"));
    }

    private static SeoGeoConfig Geo(string mode, IReadOnlyList<string>? allow = null, IReadOnlyList<string>? block = null)
        => new() { AiBotMode = mode, AiBotAllowList = allow, AiBotBlockList = block };

    private static AppConfig Config(SeoGeoConfig geo, bool robotsEnabled = true)
        => new()
        {
            Site = new SiteConfig
            {
                Name = "test",
                Title = "Test",
                Url = "https://example.com",
                Seo = new SeoConfig
                {
                    RobotsTxt = new SeoRobotsTxtConfig { Enabled = robotsEnabled },
                    Geo = geo
                }
            },
            Content = TestContent.Markdown()
        };

    private static Dictionary<string, SeoIndexEntry> IndexableIndex()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["index.html"] = new(new RouteInfo("/", "index.html", "index.html"), "https://example.com/", null, true, DateTimeOffset.UnixEpoch, null, null)
        };

    /// <summary>Returns the rule line of the group for <paramref name="userAgent"/>, or null when there is no group.</summary>
    private static string? RuleFor(string robots, string userAgent)
    {
        var lines = robots.Split(Environment.NewLine);
        var index = Array.IndexOf(lines, $"User-agent: {userAgent}");
        return index < 0 || index + 1 >= lines.Length ? null : lines[index + 1];
    }

    private static int CountGroups(string robots, string userAgent)
        => robots.Split(Environment.NewLine).Count(line => line == $"User-agent: {userAgent}");

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Infos { get; } = [];
        public List<string> Warnings { get; } = [];
        public void Debug(string message) { }
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warnings.Add(message);
        public void Error(string message) { }
    }
}
