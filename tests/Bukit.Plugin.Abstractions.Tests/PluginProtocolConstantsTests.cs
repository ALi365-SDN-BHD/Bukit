using Bukit.Plugin.Abstractions.Protocol;
using Xunit;

namespace Bukit.Plugin.Abstractions.Tests;

public sealed class PluginProtocolConstantsTests
{
    [Fact]
    public void Constants_MatchBukitPluginV1Protocol()
    {
        Assert.Equal("bukit-plugin-v1", PluginProtocolConstants.ProtocolVersion);
        Assert.Equal("handshake", PluginProtocolConstants.Handshake);
        Assert.Equal("manifest", PluginProtocolConstants.Manifest);
        Assert.Equal("invoke", PluginProtocolConstants.Invoke);
    }
}
