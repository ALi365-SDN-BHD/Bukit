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
}
