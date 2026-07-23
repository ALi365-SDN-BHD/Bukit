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
                manifestPolicy: runtime-only
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
                      - .bukit/reports/plugin-output/echo
                  environment:
                    read:
                      - BUKIT_TEST
            """);

        var loader = new PluginConfigLoader(PluginRuntimeOnlyContext.Test);

        PluginHostConfig config = await loader.LoadAsync(directory.Path, CancellationToken.None);

        var echo = Assert.Single(config.Plugins).Value;
        Assert.True(echo.Enabled);
        Assert.Equal("plugins/echo", echo.Source);
        Assert.Equal(["echo"], echo.ExposeCommands);
        Assert.True(echo.ExposeCommandsDeclared);
        Assert.Equal("runtime-only", echo.ManifestPolicy);
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
        Assert.Equal([".bukit/reports/plugin-output/echo"], echo.Permissions.FileSystem.Write);
        Assert.Equal(["BUKIT_TEST"], echo.Permissions.Environment.Read);
    }

    [Fact]
    public async Task LoadAsync_RuntimeOnlyManifestPolicy_InDefaultContext_ThrowsConfigException()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                exposeCommands: []
                manifestPolicy: runtime-only
                permissions: {}
            """);

        var loader = new PluginConfigLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("runtime-only is only allowed in development, Labs, or test contexts", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(PluginRuntimeOnlyContext.Development))]
    [InlineData(nameof(PluginRuntimeOnlyContext.Labs))]
    [InlineData(nameof(PluginRuntimeOnlyContext.Test))]
    public async Task LoadAsync_RuntimeOnlyManifestPolicy_InPrivilegedContext_LoadsConfig(string contextName)
    {
        PluginRuntimeOnlyContext context = Enum.Parse<PluginRuntimeOnlyContext>(contextName);
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                exposeCommands: []
                manifestPolicy: runtime-only
                permissions: {}
            """);

        var loader = new PluginConfigLoader(context);

        PluginHostConfig config = await loader.LoadAsync(directory.Path, CancellationToken.None);

        PluginConfigEntry entry = Assert.Single(config.Plugins).Value;
        Assert.Equal("runtime-only", entry.ManifestPolicy);
    }

    [Fact]
    public async Task LoadAsync_MissingVersion_ThrowsConfigException()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            plugins:
              echo:
                enabled: true
                source: plugins/echo
            """);

        var loader = new PluginConfigLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, exception.Code);
        Assert.Contains("plugins.yaml version is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_VersionOnly_ReturnsEmptyConfig()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            """);

        var loader = new PluginConfigLoader();

        PluginHostConfig config = await loader.LoadAsync(directory.Path, CancellationToken.None);

        Assert.Empty(config.Plugins);
    }

    [Fact]
    public async Task LoadAsync_MissingPermissions_ThrowsConfigException()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                exposeCommands: []
            """);

        var loader = new PluginConfigLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Equal(DiagnosticCode.ConfigRequiredFieldMissing, exception.Code);
        Assert.Contains("plugins.echo.permissions", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ExposeCommandsMissing_SetsDeclaredFalse()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                permissions: {}
            """);

        var loader = new PluginConfigLoader();

        PluginHostConfig config = await loader.LoadAsync(directory.Path, CancellationToken.None);

        var echo = Assert.Single(config.Plugins).Value;
        Assert.False(echo.ExposeCommandsDeclared);
        Assert.Empty(echo.ExposeCommands);
    }

    [Fact]
    public async Task LoadAsync_ExposeCommandsEmpty_SetsDeclaredTrue()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                exposeCommands: []
                permissions: {}
            """);

        var loader = new PluginConfigLoader();

        PluginHostConfig config = await loader.LoadAsync(directory.Path, CancellationToken.None);

        var echo = Assert.Single(config.Plugins).Value;
        Assert.True(echo.ExposeCommandsDeclared);
        Assert.Empty(echo.ExposeCommands);
    }

    [Theory]
    [InlineData("Bad")]
    [InlineData("bad_id")]
    [InlineData("bad/id")]
    [InlineData("bad id")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("-bad")]
    [InlineData("bad-")]
    [InlineData("bad--id")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task LoadAsync_InvalidPluginId_ThrowsConfigException(string pluginId)
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            $$"""
            version: 1
            plugins:
              "{{pluginId}}":
                enabled: true
                source: plugins/echo
                exposeCommands: []
            """);

        var loader = new PluginConfigLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("Plugin id must use lowercase letters, digits, and hyphen", exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public async Task LoadAsync_EnvironmentWildcardPermission_ThrowsConfigException()
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            """
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                permissions:
                  environment:
                    read:
                      - "*"
            """);
        var loader = new PluginConfigLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Contains("environment.read cannot contain '*'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/tmp")]
    [InlineData(".bukit/bin")]
    [InlineData(".bukit/plugins")]
    public async Task LoadAsync_UnsafeFileSystemPermission_ThrowsConfigException(string unsafePath)
    {
        using var directory = TestDirectory.Create();
        directory.Write(".bukit/plugins.yaml",
            $$"""
            version: 1
            plugins:
              echo:
                enabled: true
                source: plugins/echo
                permissions:
                  fileSystem:
                    write:
                      - {{unsafePath}}
            """);
        var loader = new PluginConfigLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(directory.Path, CancellationToken.None));

        Assert.Contains("fileSystem.write permission path", exception.Message, StringComparison.Ordinal);
    }
}
