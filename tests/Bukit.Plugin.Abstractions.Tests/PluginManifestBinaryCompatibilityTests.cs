using Bukit.Plugin.Abstractions.Manifest;
using Xunit;

namespace Bukit.Plugin.Abstractions.Tests;

public sealed class PluginManifestBinaryCompatibilityTests
{
    [Fact]
    public void LegacyConstructorAndDeconstruct_KeepNineValues()
    {
        var manifest = new PluginManifest(
            "example", "Example", "1.0.0", "bukit-plugin-v1", "process",
            "self-contained", null, null, null);

        var (id, name, version, protocol, kind, distribution,
            platforms, commands, permissions) = manifest;

        Assert.Equal("example", id);
        Assert.Equal("Example", name);
        Assert.Equal("1.0.0", version);
        Assert.Equal("bukit-plugin-v1", protocol);
        Assert.Equal("process", kind);
        Assert.Equal("self-contained", distribution);
        Assert.NotNull(platforms);
        Assert.NotNull(commands);
        Assert.Empty(platforms);
        Assert.Empty(commands);
        Assert.NotNull(permissions);
        Assert.Equal(1, manifest.ManifestVersion);
        Assert.Equal(2, (manifest with { ManifestVersion = 2 }).ManifestVersion);
    }
}
