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
            distribution: self-contained
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
        Assert.Equal("self-contained", manifest.Distribution);
        Assert.True(manifest.Platforms.ContainsKey("osx-arm64"));
        Assert.Equal("bin/osx-arm64/bukit-plugin-echo", manifest.Platforms["osx-arm64"].Entry);
        Assert.Equal("echo", Assert.Single(manifest.Commands).Name);
    }

    [Fact]
    public async Task LoadAsync_ReadsFullCommandSpec()
    {
        using var directory = TestDirectory.Create();
        directory.Write("plugins/import/plugin.yaml",
            """
            id: import
            name: Import
            version: 0.1.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: self-contained
            platforms:
              osx-arm64:
                entry: bin/osx-arm64/bukit-plugin-import
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            commands:
              - name: import
                description: Import external content
                aliases:
                  - imp
                arguments:
                  - name: source
                    description: Source path
                    required: true
                options:
                  - name: --format
                    type: string
                    description: Import format
                    required: true
                    valueName: FORMAT
                    allowedValues:
                      - html
                      - markdown
                    conflictWith: --auto
                subcommands:
                  - name: html-demo
                    description: Import HTML demo
                    arguments:
                      - name: demo-dir
                        description: Demo directory
                        required: true
                    options:
                      - name: --force
                        type: flag
                        description: Overwrite existing files
            """);

        var loader = new PluginManifestLoader();

        PluginManifest manifest = await loader.LoadAsync(
            System.IO.Path.Combine(directory.Path, "plugins/import"),
            CancellationToken.None);

        PluginCommandSpec command = Assert.Single(manifest.Commands);
        Assert.Equal("import", command.Name);
        Assert.Equal("Import external content", command.Description);
        Assert.Equal("imp", Assert.Single(command.Aliases));
        PluginArgumentSpec argument = Assert.Single(command.Arguments);
        Assert.Equal("source", argument.Name);
        Assert.True(argument.Required);
        PluginOptionSpec option = Assert.Single(command.Options);
        Assert.Equal("--format", option.Name);
        Assert.Equal("string", option.Type);
        Assert.True(option.Required);
        Assert.Equal("FORMAT", option.ValueName);
        Assert.Equal(["html", "markdown"], option.AllowedValues);
        Assert.Equal("--auto", option.ConflictWith);
        PluginCommandSpec subcommand = Assert.Single(command.Subcommands);
        Assert.Equal("html-demo", subcommand.Name);
        Assert.True(Assert.Single(subcommand.Arguments).Required);
        Assert.Equal("--force", Assert.Single(subcommand.Options).Name);
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
            distribution: self-contained
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

    [Theory]
    [InlineData("Bad")]
    [InlineData("bad_id")]
    [InlineData("bad/id")]
    [InlineData("bad id")]
    [InlineData(".")]
    [InlineData("..")]
    public async Task LoadAsync_InvalidPluginId_ThrowsConfigException(string pluginId)
    {
        using var directory = TestDirectory.Create();
        directory.Write("plugins/echo/plugin.yaml",
            $$"""
            id: "{{pluginId}}"
            name: Echo
            version: 0.1.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: self-contained
            platforms:
              osx-arm64:
                entry: bin/osx-arm64/bukit-plugin-echo
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            """);

        var loader = new PluginManifestLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(System.IO.Path.Combine(directory.Path, "plugins/echo"), CancellationToken.None));

        Assert.Equal(DiagnosticCode.ConfigInvalidValue, exception.Code);
        Assert.Contains("Plugin id must use lowercase letters, digits, and hyphen", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_EnvironmentWildcardRequiredPermission_ThrowsConfigException()
    {
        using var directory = TestDirectory.Create();
        directory.Write("plugins/echo/plugin.yaml",
            """
            id: echo
            name: Echo
            version: 0.1.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: self-contained
            platforms:
              osx-arm64:
                entry: bin/osx-arm64/bukit-plugin-echo
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            requiredPermissions:
              environment:
                read:
                  - "*"
            """);

        var loader = new PluginManifestLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(System.IO.Path.Combine(directory.Path, "plugins/echo"), CancellationToken.None));

        Assert.Contains("environment.read cannot contain '*'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_ExternalDistribution_ThrowsConfigException()
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
            """);

        var loader = new PluginManifestLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(System.IO.Path.Combine(directory.Path, "plugins/echo"), CancellationToken.None));

        Assert.Contains("distribution must be self-contained", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../secret")]
    [InlineData("/tmp")]
    [InlineData(".bukit/bin")]
    [InlineData(".bukit/plugins")]
    public async Task LoadAsync_UnsafeRequiredFileSystemPermission_ThrowsConfigException(string unsafePath)
    {
        using var directory = TestDirectory.Create();
        directory.Write("plugins/echo/plugin.yaml",
            $$"""
            id: echo
            name: Echo
            version: 0.1.0
            protocol: bukit-plugin-v1
            kind: process
            distribution: self-contained
            platforms:
              osx-arm64:
                entry: bin/osx-arm64/bukit-plugin-echo
                sha256: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            requiredPermissions:
              fileSystem:
                write:
                  - {{unsafePath}}
            """);

        var loader = new PluginManifestLoader();

        ConfigException exception = await Assert.ThrowsAsync<ConfigException>(
            () => loader.LoadAsync(System.IO.Path.Combine(directory.Path, "plugins/echo"), CancellationToken.None));

        Assert.Contains("fileSystem.write permission path", exception.Message, StringComparison.Ordinal);
    }
}
