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

    private static readonly string[] NotionPushOptions =
    [
        "--input",
        "--database-id",
        "--database-map",
        "--create-missing-databases",
        "--parent-page-id",
        "--generated-database-map",
        "--token-env",
        "--mode",
        "--unique-field",
        "--update-content",
        "--dry-run",
        "--report",
        "--no-validate-schema"
    ];

    private static readonly string[] NotionValidateSchemaOptions = ["--database-id", "--token-env", "--report"];

    [Fact]
    public void Manifest_DeclaresFullCurrentImportAndNotionCommandSurface()
    {
        var response = ImportPluginManifestProvider.CreateManifestResponse("req-compat");
        Assert.Equal(["import", "notion"], response.Commands.Select(command => command.Name).OrderBy(name => name, StringComparer.Ordinal));

        var import = Assert.Single(response.Commands, command => command.Name == "import");
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

        var notion = Assert.Single(response.Commands, command => command.Name == "notion");
        var push = Assert.Single(notion.Subcommands, command => command.Name == "push");
        Assert.Equal(
            NotionPushOptions.OrderBy(value => value, StringComparer.Ordinal),
            push.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));

        var validateSchema = Assert.Single(notion.Subcommands, command => command.Name == "validate-schema");
        Assert.Equal(
            NotionValidateSchemaOptions.OrderBy(value => value, StringComparer.Ordinal),
            validateSchema.Options.Select(option => option.Name).OrderBy(value => value, StringComparer.Ordinal));
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
