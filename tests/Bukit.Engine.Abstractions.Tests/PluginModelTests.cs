using Bukit.Engine.Abstractions.Plugins;
using Xunit;

namespace Bukit.Engine.Abstractions.Tests;

public sealed class PluginModelTests
{
    [Fact]
    public void PluginExecutionInfo_RecordsSuccess()
    {
        var info = new PluginExecutionInfo("my-plugin", "AfterBuild", 42, true, null);
        Assert.Equal("my-plugin", info.Name);
        Assert.True(info.Success);
        Assert.Equal(42, info.DurationMs);
    }

    [Fact]
    public void PluginExecutionInfo_Failure()
    {
        var info = new PluginExecutionInfo("bad", "AfterBuild", 0, false, "error msg");
        Assert.False(info.Success);
        Assert.Equal("error msg", info.Error);
    }

    [Fact]
    public void PluginOutputTrackingInfo_IdentifiesOutput()
    {
        var info = new PluginOutputTrackingInfo("p", "AfterBuild", "dist/x.css");
        Assert.Equal("p", info.Plugin);
        Assert.Equal("dist/x.css", info.Path);
    }
}
