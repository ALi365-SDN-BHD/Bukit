using Bukit.Cli.Commands;
using Xunit;

namespace Bukit.Cli.Tests;

public sealed class DevCommandTests
{
    [Fact]
    public void CreateBuildOverrides_ForRebuild_PreservesCustomOutput()
    {
        var overrides = DevCommand.CreateBuildOverrides(
            clean: false,
            outputOverride: "dist-dev-test",
            cacheDir: ".cache-dev");

        Assert.False(overrides.Clean);
        Assert.Equal("dist-dev-test", overrides.Output);
        Assert.True(overrides.Incremental);
        Assert.Equal(".cache-dev", overrides.CacheDir);
    }
}
