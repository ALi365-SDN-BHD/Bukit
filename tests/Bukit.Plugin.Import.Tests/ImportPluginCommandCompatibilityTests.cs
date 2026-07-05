using Bukit.Plugin.Import;
using Xunit;

namespace Bukit.Plugin.Import.Tests;

public sealed class ImportPluginCommandCompatibilityTests
{
    private static readonly string[] HtmlDemoOptions =
    [
        "--config",
        "--site",
        "--theme",
        "--force",
        "--use",
        "--verify",
        "--no-extract-content",
        "--no-seed",
        "--content-source",
        "--build-source",
        "--site-path",
        "--language",
        "--dry-run",
        "--strict",
        "--overwrite",
        "--no-preserve-html",
        "--no-report",
        "--base-url",
        "--route-map",
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

    private static readonly string[] SeedOptions = ["--output", "--force"];

    [Fact]
    public void Manifest_DeclaresFullCurrentImportCommandSurface()
    {
        var response = ImportPluginManifestProvider.CreateManifestResponse("req-compat");
        var import = Assert.Single(response.Commands);
        Assert.Equal("import", import.Name);

        var htmlDemo = Assert.Single(import.Subcommands, command => command.Name == "html-demo");
        Assert.Contains(htmlDemo.Arguments, argument => argument.Name == "demo-dir" && argument.Required);
        Assert.Equal(
            HtmlDemoOptions.OrderBy(value => value, StringComparer.Ordinal),
            htmlDemo.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));

        var seed = Assert.Single(import.Subcommands, command => command.Name == "seed");
        Assert.Contains(seed.Arguments, argument => argument.Name == "seed-dir" && argument.Required);
        Assert.Equal(
            SeedOptions.OrderBy(value => value, StringComparer.Ordinal),
            seed.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void Manifest_RequiresPermissionsNeededByFullImportWorkflow()
    {
        var response = ImportPluginManifestProvider.CreateManifestResponse("req-perms");

        Assert.True(response.RequiredPermissions.Network);
        Assert.Contains(".", response.RequiredPermissions.FileSystem.Read);
        Assert.Contains("./themes", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./sites", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./content", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./data", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("./docs/research", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains(".bukit/reports/plugin-output/import", response.RequiredPermissions.FileSystem.Write);
        Assert.Contains("NOTION_TOKEN", response.RequiredPermissions.Environment.Read);
    }
}
