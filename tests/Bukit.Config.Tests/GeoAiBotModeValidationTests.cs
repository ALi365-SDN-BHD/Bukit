using Bukit.Shared;
using Xunit;

namespace Bukit.Config.Tests;

public sealed class GeoAiBotModeValidationTests
{
    [Theory]
    [InlineData("allow")]
    [InlineData("block-training")]
    [InlineData("Block-Training")]
    [InlineData("block")]
    [InlineData("selective")]
    public void ValidateSite_AcceptsKnownAiBotModes(string mode)
    {
        SiteConfigValidator.ValidateSite(Site(mode));
    }

    [Fact]
    public void ValidateSite_RejectsUnknownAiBotMode()
    {
        var ex = Assert.Throws<ConfigException>(() => SiteConfigValidator.ValidateSite(Site("training-only")));

        Assert.Equal("site.seo.geo.aiBotMode must be allow|block-training|block|selective.", ex.Message);
    }

    [Fact]
    public void ValidateSite_AcceptsCrawlerTokens()
    {
        SiteConfigValidator.ValidateSite(Site("selective", allow: ["OAI-SearchBot", " Claude_User ", "bot.v2"], block: ["GPTBot"]));
    }

    [Theory]
    [InlineData("GPTBot\nDisallow: /")]
    [InlineData("*")]
    [InlineData("Bad Bot")]
    [InlineData("User-agent: X")]
    [InlineData("  ")]
    public void ValidateSite_RejectsNamesThatWouldBreakRobotsTxt(string name)
    {
        var allowEx = Assert.Throws<ConfigException>(() => SiteConfigValidator.ValidateSite(Site("allow", allow: ["GPTBot", name])));
        Assert.StartsWith("site.seo.geo.aiBotAllowList[1] must be a crawler user-agent token", allowEx.Message, StringComparison.Ordinal);

        var blockEx = Assert.Throws<ConfigException>(() => SiteConfigValidator.ValidateSite(Site("allow", block: [name])));
        Assert.StartsWith("site.seo.geo.aiBotBlockList[0] must be a crawler user-agent token", blockEx.Message, StringComparison.Ordinal);
    }

    private static SiteConfig Site(string mode, IReadOnlyList<string>? allow = null, IReadOnlyList<string>? block = null)
        => new()
        {
            Name = "test",
            Title = "Test",
            Seo = new SeoConfig { Geo = new SeoGeoConfig { AiBotMode = mode, AiBotAllowList = allow, AiBotBlockList = block } }
        };
}
