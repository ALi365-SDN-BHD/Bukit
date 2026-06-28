using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Security;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.Plugin.Notion.Tests;

public sealed class NotionPluginManifestTests
{
    [Fact]
    public void RuntimeManifest_DeclaresNotionCommandSurfaceAndPermissions()
    {
        var manifest = NotionPluginManifestProvider.CreateManifestResponse("req-manifest");

        PluginCommandSpec notion = Assert.Single(manifest.Commands);
        Assert.Equal("notion", notion.Name);
        AssertValidateSeedContract(Assert.Single(notion.Subcommands, command => command.Name == "validate-seed"));
        AssertValidateDatabaseMapContract(Assert.Single(notion.Subcommands, command => command.Name == "validate-database-map"));
        AssertSchemaValidateContract(notion);
        AssertPushContract(Assert.Single(notion.Subcommands, command => command.Name == "push"));
        AssertNotionPermissions(manifest.RequiredPermissions);
    }

    [Fact]
    public async Task StaticManifest_DeclaresNotionCommandSurfaceAndPermissions()
    {
        string pluginRoot = Path.Combine(
            FindRepositoryRoot(),
            "plugins",
            "Bukit.Plugin.Notion",
            "examples",
            "minimal",
            "plugins",
            "notion");

        PluginManifest manifest = await new PluginManifestLoader().LoadAsync(pluginRoot, CancellationToken.None);

        Assert.Equal("notion", manifest.Id);
        Assert.Equal("1.0.0-rc.1", manifest.Version);
        Assert.Equal("bukit-plugin-v1", manifest.Protocol);
        Assert.Equal("process", manifest.Kind);
        Assert.Equal("self-contained", manifest.Distribution);

        PluginCommandSpec notion = Assert.Single(manifest.Commands);
        Assert.Equal("notion", notion.Name);
        AssertValidateSeedContract(Assert.Single(notion.Subcommands, command => command.Name == "validate-seed"));
        AssertValidateDatabaseMapContract(Assert.Single(notion.Subcommands, command => command.Name == "validate-database-map"));
        AssertSchemaValidateContract(notion);
        AssertPushContract(Assert.Single(notion.Subcommands, command => command.Name == "push"));
        AssertNotionPermissions(manifest.RequiredPermissions);
    }

    [Fact]
    public async Task StaticManifest_DeclaresRequiredPackageRuntimeIdentifiers()
    {
        string pluginRoot = Path.Combine(
            FindRepositoryRoot(),
            "plugins",
            "Bukit.Plugin.Notion",
            "examples",
            "minimal",
            "plugins",
            "notion");

        PluginManifest manifest = await new PluginManifestLoader().LoadAsync(pluginRoot, CancellationToken.None);

        Assert.Equal(
            ["linux-x64", "osx-arm64", "win-x64"],
            manifest.Platforms.Keys.Order(StringComparer.Ordinal).ToArray());

        Assert.Equal("bin/linux-x64/bukit-plugin-notion", manifest.Platforms["linux-x64"].Entry);
        Assert.Equal("bin/osx-arm64/bukit-plugin-notion", manifest.Platforms["osx-arm64"].Entry);
        Assert.Equal("bin/win-x64/bukit-plugin-notion.exe", manifest.Platforms["win-x64"].Entry);
        Assert.All(manifest.Platforms.Values, platform => Assert.Matches("^[a-f0-9]{64}$", platform.Sha256));
    }

    [Fact]
    public void MinimalPluginsYaml_DoesNotDeclareEntry()
    {
        string pluginsYaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "plugins",
            "Bukit.Plugin.Notion",
            "examples",
            "minimal",
            ".bukit",
            "plugins.yaml"));

        Assert.DoesNotContain("entry:", pluginsYaml, StringComparison.Ordinal);
    }

    private static void AssertValidateSeedContract(PluginCommandSpec command)
    {
        PluginArgumentSpec seedDir = Assert.Single(command.Arguments);
        Assert.Equal("seed-dir", seedDir.Name);
        Assert.True(seedDir.Required);
        Assert.Empty(command.Options);
    }

    private static void AssertValidateDatabaseMapContract(PluginCommandSpec command)
    {
        PluginArgumentSpec databaseMap = Assert.Single(command.Arguments);
        Assert.Equal("database-map", databaseMap.Name);
        Assert.True(databaseMap.Required);
        Assert.Empty(command.Options);
    }

    private static void AssertSchemaValidateContract(PluginCommandSpec notion)
    {
        PluginCommandSpec schema = Assert.Single(notion.Subcommands, command => command.Name == "schema");
        PluginCommandSpec validate = Assert.Single(schema.Subcommands, command => command.Name == "validate");
        Assert.Empty(validate.Arguments);

        PluginOptionSpec databaseMap = Assert.Single(validate.Options, option => option.Name == "--database-map");
        Assert.Equal("string", databaseMap.Type);
        Assert.True(databaseMap.Required);

        PluginOptionSpec tokenEnv = Assert.Single(validate.Options, option => option.Name == "--token-env");
        Assert.Equal("string", tokenEnv.Type);
        Assert.False(tokenEnv.Required);
        Assert.Equal(["NOTION_TOKEN"], tokenEnv.AllowedValues);

        PluginOptionSpec report = Assert.Single(validate.Options, option => option.Name == "--report");
        Assert.Equal("string", report.Type);
        Assert.False(report.Required);
    }

    private static void AssertPushContract(PluginCommandSpec command)
    {
        Assert.Empty(command.Arguments);

        PluginOptionSpec seed = Assert.Single(command.Options, option => option.Name == "--seed");
        Assert.Equal("string", seed.Type);
        Assert.True(seed.Required);

        PluginOptionSpec databaseMap = Assert.Single(command.Options, option => option.Name == "--database-map");
        Assert.Equal("string", databaseMap.Type);
        Assert.True(databaseMap.Required);

        PluginOptionSpec tokenEnv = Assert.Single(command.Options, option => option.Name == "--token-env");
        Assert.Equal("string", tokenEnv.Type);
        Assert.False(tokenEnv.Required);
        Assert.Equal(["NOTION_TOKEN"], tokenEnv.AllowedValues);

        PluginOptionSpec mode = Assert.Single(command.Options, option => option.Name == "--mode");
        Assert.Equal("string", mode.Type);
        Assert.True(mode.Required);
        Assert.Equal(["create", "upsert", "replace"], mode.AllowedValues);

        PluginOptionSpec dryRun = Assert.Single(command.Options, option => option.Name == "--dry-run");
        Assert.Equal("flag", dryRun.Type);
        Assert.False(dryRun.Required);

        PluginOptionSpec report = Assert.Single(command.Options, option => option.Name == "--report");
        Assert.Equal("string", report.Type);
        Assert.False(report.Required);
    }

    private static void AssertNotionPermissions(PluginPermissionSet permissions)
    {
        Assert.Equal(["."], permissions.FileSystem.Read);
        Assert.Equal(["./.bukit/reports/plugin-output/notion", "./.bukit/tmp/notion"], permissions.FileSystem.Write);
        Assert.True(permissions.Network);
        Assert.Equal(["NOTION_TOKEN"], permissions.Environment.Read);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "bukit.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
