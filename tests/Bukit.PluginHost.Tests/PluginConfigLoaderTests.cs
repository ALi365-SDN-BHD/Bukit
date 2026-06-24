using Bukit.Plugin.Abstractions.Config;
using Bukit.PluginHost;
using Bukit.Shared;
using Xunit;

namespace Bukit.PluginHost.Tests;

public sealed class PluginConfigLoaderTests
{
    [Fact]
    public async Task LoadAsync_MissingPluginsYaml_ReturnsEmptyConfigAndDoesNotCreateBukitDirectory()
    {
        using var directory = TestDirectory.Create();
        var loader = new PluginConfigLoader();

        PluginHostConfig config = await loader.LoadAsync(directory.Path, CancellationToken.None);

        Assert.Empty(config.Plugins);
        Assert.False(Directory.Exists(System.IO.Path.Combine(directory.Path, ".bukit")));
    }

    [Fact]
    public async Task LoadAsync_ReadsPluginEntries()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                exposeCommands:
                  - echo
                failMode: warn
                allowInCi: true
                description: Echo plugin
                timeout:
                  handshakeMs: 1000
                  manifestMs: 1500
                  invokeMs: 2000
                output:
                  stdoutMaxBytes: 4096
                  stderrMaxBytes: 2048
                  responseMaxBytes: 8192
                permissions:
                  network: true
                  fileSystem:
                    read:
                      - content
                    write:
                      - .bukit/reports/plugins
                  environment:
                    read:
                      - BUKIT_TEST
            """);

        var loader = new PluginConfigLoader();

        PluginHostConfig config = await loader.LoadAsync(directory.Path, CancellationToken.None);

        var echo = Assert.Single(config.Plugins).Value;
        Assert.True(echo.Enabled);
        Assert.Equal("plugins/echo", echo.Source);
        Assert.Equal(["echo"], echo.ExposeCommands);
        Assert.Equal("warn", echo.FailMode);
        Assert.True(echo.AllowInCi);
        Assert.Equal("Echo plugin", echo.Description);
        Assert.Equal(1000, echo.Timeout.HandshakeMs);
        Assert.Equal(1500, echo.Timeout.ManifestMs);
        Assert.Equal(2000, echo.Timeout.InvokeMs);
        Assert.Equal(4096, echo.Output.StdoutMaxBytes);
        Assert.Equal(2048, echo.Output.StderrMaxBytes);
        Assert.Equal(8192, echo.Output.ResponseMaxBytes);
        Assert.True(echo.Permissions.Network);
        Assert.True(echo.PermissionsExplicit);
        Assert.Equal(["content"], echo.Permissions.FileSystem.Read);
        Assert.Equal([".bukit/reports/plugins"], echo.Permissions.FileSystem.Write);
        Assert.Equal(["BUKIT_TEST"], echo.Permissions.Environment.Read);
    }

    [Fact]
    public async Task LoadAsync_InvalidYaml_ThrowsConfigException()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml", "version: [");
        var loader = new PluginConfigLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Equal(DiagnosticCode.ConfigYamlSyntaxError, exception.Code);
    }
}
