using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Security;
using Bukit.PluginHost;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginManifestTests
{
    [Fact]
    public void RuntimeManifest_DeclaresSeedAndHtmlDemoDryRunContracts()
    {
        var manifest = ImportPluginManifestProvider.CreateManifestResponse("req-manifest");

        PluginCommandSpec import = Assert.Single(manifest.Commands);
        Assert.Equal("import", import.Name);
        PluginCommandSpec seed = Assert.Single(import.Subcommands, command => command.Name == "seed");
        PluginCommandSpec htmlDemo = Assert.Single(import.Subcommands, command => command.Name == "html-demo");

        PluginArgumentSpec seedDir = Assert.Single(seed.Arguments);
        Assert.Equal("seed-dir", seedDir.Name);
        Assert.True(seedDir.Required);

        PluginOptionSpec output = Assert.Single(seed.Options, option => option.Name == "--output");
        Assert.Equal("string", output.Type);
        Assert.True(output.Required);

        PluginOptionSpec force = Assert.Single(seed.Options, option => option.Name == "--force");
        Assert.Equal("flag", force.Type);
        Assert.False(force.Required);

        Assert.Empty(import.Arguments);
        Assert.Empty(import.Options);
        AssertNoNotionOptions(seed);
        AssertHtmlDemoDryRunContract(htmlDemo);
        AssertNoNotionOptions(htmlDemo);
        AssertSeedPermissions(manifest.RequiredPermissions);
    }

    [Fact]
    public async Task StaticManifest_DeclaresSeedAndHtmlDemoDryRunContracts()
    {
        string pluginRoot = Path.Combine(
            FindRepositoryRoot(),
            "plugins",
            "Bukit.Plugin.Import",
            "examples",
            "minimal",
            "plugins",
            "import");

        PluginManifest manifest = await new PluginManifestLoader().LoadAsync(pluginRoot, CancellationToken.None);

        Assert.Equal("import", manifest.Id);
        Assert.Equal("bukit-plugin-v1", manifest.Protocol);
        Assert.Equal("process", manifest.Kind);
        Assert.Equal("self-contained", manifest.Distribution);

        PluginCommandSpec import = Assert.Single(manifest.Commands);
        Assert.Equal("import", import.Name);
        PluginCommandSpec seed = Assert.Single(import.Subcommands, command => command.Name == "seed");
        PluginCommandSpec htmlDemo = Assert.Single(import.Subcommands, command => command.Name == "html-demo");

        PluginArgumentSpec seedDir = Assert.Single(seed.Arguments);
        Assert.Equal("seed-dir", seedDir.Name);
        Assert.True(seedDir.Required);

        PluginOptionSpec output = Assert.Single(seed.Options, option => option.Name == "--output");
        Assert.Equal("string", output.Type);
        Assert.True(output.Required);

        PluginOptionSpec force = Assert.Single(seed.Options, option => option.Name == "--force");
        Assert.Equal("flag", force.Type);
        Assert.False(force.Required);

        AssertNoNotionOptions(seed);
        AssertHtmlDemoDryRunContract(htmlDemo);
        AssertNoNotionOptions(htmlDemo);
        AssertSeedPermissions(manifest.RequiredPermissions);
    }

    [Fact]
    public async Task StaticManifest_DeclaresRequiredPackageRuntimeIdentifiers()
    {
        string pluginRoot = Path.Combine(
            FindRepositoryRoot(),
            "plugins",
            "Bukit.Plugin.Import",
            "examples",
            "minimal",
            "plugins",
            "import");

        PluginManifest manifest = await new PluginManifestLoader().LoadAsync(pluginRoot, CancellationToken.None);

        Assert.Equal(
            ["linux-x64", "osx-arm64", "win-x64"],
            manifest.Platforms.Keys.Order(StringComparer.Ordinal).ToArray());

        Assert.Equal("bin/linux-x64/bukit-plugin-import", manifest.Platforms["linux-x64"].Entry);
        Assert.Equal("bin/osx-arm64/bukit-plugin-import", manifest.Platforms["osx-arm64"].Entry);
        Assert.Equal("bin/win-x64/bukit-plugin-import.exe", manifest.Platforms["win-x64"].Entry);
        Assert.All(manifest.Platforms.Values, platform => Assert.Matches("^[a-f0-9]{64}$", platform.Sha256));
    }

    [Fact]
    public void PackageBuildAndSmokeScripts_AreCommitted()
    {
        string repoRoot = FindRepositoryRoot();

        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "build", "import-plugin-package.sh")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "smoke", "import-plugin-package.sh")));
    }

    private static void AssertHtmlDemoDryRunContract(PluginCommandSpec htmlDemo)
    {
        PluginArgumentSpec demoDir = Assert.Single(htmlDemo.Arguments);
        Assert.Equal("demo-dir", demoDir.Name);
        Assert.True(demoDir.Required);

        PluginOptionSpec theme = Assert.Single(htmlDemo.Options, option => option.Name == "--theme");
        Assert.Equal("string", theme.Type);
        Assert.True(theme.Required);

        PluginOptionSpec dryRun = Assert.Single(htmlDemo.Options, option => option.Name == "--dry-run");
        Assert.Equal("flag", dryRun.Type);
        Assert.False(dryRun.Required);

        Assert.DoesNotContain(htmlDemo.Options, option => option.Name == "--overwrite");

        PluginOptionSpec use = Assert.Single(htmlDemo.Options, option => option.Name == "--use");
        Assert.Equal("flag", use.Type);
        Assert.False(use.Required);

        PluginOptionSpec verify = Assert.Single(htmlDemo.Options, option => option.Name == "--verify");
        Assert.Equal("flag", verify.Type);
        Assert.False(verify.Required);

        PluginOptionSpec strict = Assert.Single(htmlDemo.Options, option => option.Name == "--strict");
        Assert.Equal("string", strict.Type);
        Assert.False(strict.Required);

        PluginOptionSpec force = Assert.Single(htmlDemo.Options, option => option.Name == "--force");
        Assert.Equal("flag", force.Type);
        Assert.False(force.Required);

        PluginOptionSpec routeMap = Assert.Single(htmlDemo.Options, option => option.Name == "--route-map");
        Assert.Equal("string", routeMap.Type);
        Assert.False(routeMap.Required);

        PluginOptionSpec sitePath = Assert.Single(htmlDemo.Options, option => option.Name == "--site-path");
        Assert.Equal("string", sitePath.Type);
        Assert.False(sitePath.Required);

        PluginOptionSpec language = Assert.Single(htmlDemo.Options, option => option.Name == "--language");
        Assert.Equal("string", language.Type);
        Assert.False(language.Required);

        PluginOptionSpec contentSource = Assert.Single(htmlDemo.Options, option => option.Name == "--content-source");
        Assert.Equal("string", contentSource.Type);
        Assert.False(contentSource.Required);

        PluginOptionSpec buildSource = Assert.Single(htmlDemo.Options, option => option.Name == "--build-source");
        Assert.Equal("string", buildSource.Type);
        Assert.False(buildSource.Required);

        PluginOptionSpec noExtractContent = Assert.Single(htmlDemo.Options, option => option.Name == "--no-extract-content");
        Assert.Equal("flag", noExtractContent.Type);
        Assert.False(noExtractContent.Required);

        PluginOptionSpec noSeed = Assert.Single(htmlDemo.Options, option => option.Name == "--no-seed");
        Assert.Equal("flag", noSeed.Type);
        Assert.False(noSeed.Required);

        PluginOptionSpec noReport = Assert.Single(htmlDemo.Options, option => option.Name == "--no-report");
        Assert.Equal("flag", noReport.Type);
        Assert.False(noReport.Required);
    }

    private static void AssertSeedPermissions(PluginPermissionSet permissions)
    {
        Assert.Equal(["."], permissions.FileSystem.Read);
        Assert.Equal(["./content", "./themes", "./sites", ".bukit/reports/plugin-output/import"], permissions.FileSystem.Write);
        Assert.False(permissions.Network);
        Assert.Empty(permissions.Environment.Read);
    }

    private static void AssertNoNotionOptions(PluginCommandSpec command)
    {
        string[] notionOptions =
        [
            "--push-notion",
            "--notion-database-id",
            "--notion-database-map",
            "--create-missing-notion-databases",
            "--notion-parent-page-id",
            "--notion-generated-database-map",
            "--notion-token-env",
            "--notion-report",
            "--no-validate-notion-schema"
        ];

        foreach (string optionName in notionOptions)
        {
            Assert.DoesNotContain(command.Options, option => option.Name == optionName);
        }
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
