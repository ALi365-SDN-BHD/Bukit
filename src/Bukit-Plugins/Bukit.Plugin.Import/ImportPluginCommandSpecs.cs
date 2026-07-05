using Bukit.Plugin.Abstractions.Manifest;
using Bukit.Plugin.Abstractions.Security;

namespace Bukit.Plugin.Import;

public static class ImportPluginCommandSpecs
{
    private static readonly string[] FileSystemWrites =
    [
        "./themes",
        "./sites",
        "./content",
        "./data",
        "./docs/research",
        ".bukit/reports/plugin-output/import"
    ];

    public static IReadOnlyList<PluginCommandSpec> CreateCommands()
        =>
        [
            new PluginCommandSpec(
                Name: "import",
                Description: "Import content into a Bukit site.",
                Subcommands:
                [
                    new PluginCommandSpec(
                        Name: "html-demo",
                        Description: "Import an HTML demo into a Bukit theme and site.",
                        Arguments:
                        [
                            new PluginArgumentSpec("demo-dir", "HTML demo directory.", Required: true)
                        ],
                        Options:
                        [
                            StringOption("--config", "Config file path."),
                            StringOption("--site", "Site config name."),
                            StringOption("--theme", "Theme name.", required: true),
                            FlagOption("--force", "Overwrite existing theme files."),
                            FlagOption("--use", "Set the imported theme as the active theme."),
                            FlagOption("--verify", "Build the imported site after import."),
                            FlagOption("--no-extract-content", "Disable content extraction."),
                            FlagOption("--no-seed", "Disable seed generation."),
                            StringOption("--content-source", "Generated content source type."),
                            StringOption("--build-source", "Build content source type."),
                            StringOption("--site-path", "Generated site path."),
                            StringOption("--language", "Content language."),
                            FlagOption("--dry-run", "Analyze without writing files."),
                            StringOption("--strict", "Strict mode."),
                            FlagOption("--overwrite", "Overwrite generated content files."),
                            FlagOption("--no-preserve-html", "Do not preserve source HTML."),
                            FlagOption("--no-report", "Do not write import report."),
                            StringOption("--base-url", "Base URL for generated site config."),
                            StringOption("--route-map", "Route map path."),
                            FlagOption("--push-notion", "Push generated seed data to Notion."),
                            StringOption("--notion-database-id", "Single Notion database id."),
                            StringOption("--notion-database-map", "Notion database map path."),
                            FlagOption("--create-missing-notion-databases", "Create missing Notion databases."),
                            StringOption("--notion-parent-page-id", "Parent page for created Notion databases."),
                            StringOption("--notion-generated-database-map", "Generated Notion database map path."),
                            StringOption("--notion-token-env", "Environment variable containing the Notion token."),
                            StringOption("--notion-report", "Notion push report path."),
                            FlagOption("--no-validate-notion-schema", "Disable Notion schema validation.")
                        ]),
                    new PluginCommandSpec(
                        Name: "seed",
                        Description: "Convert import seed files into markdown content.",
                        Arguments:
                        [
                            new PluginArgumentSpec("seed-dir", "Seed directory.", Required: true)
                        ],
                        Options:
                        [
                            StringOption("--output", "Content output directory.", required: true),
                            FlagOption("--force", "Overwrite existing markdown files.")
                        ])
                ])
        ];

    public static PluginPermissionSet CreateRequiredPermissions()
        => new(
            FileSystem: new PluginFileSystemPermission(
                Read: ["."],
                Write: FileSystemWrites),
            Network: true,
            Environment: new PluginEnvironmentPermission(Read: ["NOTION_TOKEN"]));

    private static PluginOptionSpec StringOption(string name, string description, bool required = false)
        => new(name, "string", description, Required: required);

    private static PluginOptionSpec FlagOption(string name, string description)
        => new(name, "flag", description);
}
