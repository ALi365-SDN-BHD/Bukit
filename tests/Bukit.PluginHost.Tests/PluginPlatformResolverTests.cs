using Bukit.PluginHost;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginPlatformResolverTests
{
    [Fact]
    public void GetCurrentRid_ReturnsSupportedRuntimeIdentifier()
    {
        var resolver = new PluginPlatformResolver();

        string rid = resolver.GetCurrentRid();

        Assert.Contains(rid, PluginPlatformResolver.SupportedRuntimeIdentifiers);
    }

    [Fact]
    public void SupportedRuntimeIdentifiers_ContainsExpectedRids()
    {
        Assert.Equal(
            ["win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64"],
            PluginPlatformResolver.SupportedRuntimeIdentifiers);
    }
}
