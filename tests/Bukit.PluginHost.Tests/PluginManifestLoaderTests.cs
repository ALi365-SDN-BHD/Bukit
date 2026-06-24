using Bukit.Plugin.Abstractions.Manifest;
using Bukit.PluginHost;
using Bukit.Shared;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginManifestLoaderTests
{
    [Fact]
    public async Task LoadAsync_ReadsPluginManifest()
    {
        using var directory = TestDirectory.Create();
        directory.Write("plugins/echo/plugin.yaml",
            """
            id: echo
            name: Echo
            version: 0.1.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: external
            platforms:
              osx-arm64:
                entry: bin/osx-arm64/bukit-plugin-echo
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            commands:
              - name: echo
                summary: Echo input
            requiredPermissions:
              network: false
            """);

        var loader = new PluginManifestLoader();

        PluginManifest manifest = await loader.LoadAsync(
            System.IO.Path.Combine(directory.Path, "plugins/echo"),
            CancellationToken.None);

        Assert.Equal("echo", manifest.Id);
        Assert.Equal("Echo", manifest.Name);
        Assert.Equal("0.1.0", manifest.Version);
        Assert.Equal("bukit-plugin-v1", manifest.Protocol);
        Assert.Equal("process", manifest.Kind);
        Assert.Equal("external", manifest.Distribution);
        Assert.True(manifest.Platforms.ContainsKey("osx-arm64"));
        Assert.Equal("bin/osx-arm64/bukit-plugin-echo", manifest.Platforms["osx-arm64"].Entry);
        Assert.Equal("echo", Assert.Single(manifest.Commands).Name);
    }

    [Theory]
    [InlineData("protocol: bukit-plugin-v0", "protocol")]
    [InlineData("kind: dll", "kind")]
    [InlineData("id: ''", "id")]
    public async Task LoadAsync_InvalidManifest_ThrowsConfigException(string replacement, string expectedMessage)
    {
        using var directory = TestDirectory.Create();
        string manifest =
            """
            id: echo
            name: Echo
            version: 0.1.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: external
            platforms:
              osx-arm64:
                entry: bin/osx-arm64/bukit-plugin-echo
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            """;

        if (replacement.StartsWith("protocol:", StringComparison.Ordinal))
        {
            manifest = manifest.Replace("protocol: bukit-plugin-v1", replacement, StringComparison.Ordinal);
        }
        else if (replacement.StartsWith("kind:", StringComparison.Ordinal))
        {
            manifest = manifest.Replace("kind: process", replacement, StringComparison.Ordinal);
        }
        else
        {
            manifest = manifest.Replace("id: echo", replacement, StringComparison.Ordinal);
        }

        directory.Write("plugins/echo/plugin.yaml", manifest);

        var loader = new PluginManifestLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(System.IO.Path.Combine(directory.Path, "plugins/echo"), CancellationToken.None));

        Assert.Contains(expectedMessage, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
