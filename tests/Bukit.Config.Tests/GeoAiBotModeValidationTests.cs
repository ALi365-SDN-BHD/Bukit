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
        I18nValidator.ValidateSite(Site(mode));
    }

    [Fact]
    public void ValidateSite_RejectsUnknownAiBotMode()
    {
        var ex = Assert.Throws<ConfigException>(() => I18nValidator.ValidateSite(Site("training-only")));

        Assert.Equal("site.seo.geo.aiBotMode must be allow|block-training|block|selective.", ex.Message);
    }

    private static SiteConfig Site(string mode)
        => new()
        {
            Name = "test",
            Title = "Test",
            Seo = new SeoConfig { Geo = new SeoGeoConfig { AiBotMode = mode } }
        };
}
