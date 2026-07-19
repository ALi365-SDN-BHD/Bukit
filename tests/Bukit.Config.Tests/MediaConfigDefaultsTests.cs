using Bukit.Config;
using Xunit;

namespace Bukit.Config.Tests;

public class MediaConfigDefaultsTests
{
    [Fact]
    public void MediaConfig_BlockPrivateNetworks_DefaultsToTrue()
    {
        var config = new MediaConfig();
        Assert.True(config.BlockPrivateNetworks);
    }

    [Fact]
    public void MediaConfig_FieldKeys_IncludeSeoImageByDefault()
    {
        var config = new MediaConfig();

        Assert.Equal(
            new[] { "cover", "image", "thumbnail", "og_image", "seo_image", "icon" },
            config.FieldKeys);
    }
}
