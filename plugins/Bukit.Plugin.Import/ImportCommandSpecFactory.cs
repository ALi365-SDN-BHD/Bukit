using Bukit.Plugin.Abstractions.Manifest;

namespace Bukit.Plugin.Import;

public static class ImportCommandSpecFactory
{
    public static PluginCommandSpec CreateImportCommand()
        => new(
            Name: "import",
            Description: "Import content into a Bukit site.",
            Subcommands:
            [
                CreateSeedCommand(),
                CreateHtmlDemoCommand()
            ]);

    private static PluginCommandSpec CreateSeedCommand()
        => new(
            Name: "seed",
            Description: "Convert generated seed data into markdown content.",
            Arguments:
            [
                new PluginArgumentSpec(
                    Name: "seed-dir",
                    Description: "Seed directory.",
                    Required: true)
            ],
            Options:
            [
                new PluginOptionSpec(
                    Name: "--output",
                    Type: "string",
                    Description: "Output content directory.",
                    Required: true),
                new PluginOptionSpec(
                    Name: "--force",
                    Type: "flag",
                    Description: "Overwrite existing markdown files.")
            ]);

    private static PluginCommandSpec CreateHtmlDemoCommand()
        => new(
            Name: "html-demo",
            Description: "Import or scan a static HTML demo.",
            Arguments:
            [
                new PluginArgumentSpec(
                    Name: "demo-dir",
                    Description: "HTML demo directory.",
                    Required: true)
            ],
            Options:
            [
                new PluginOptionSpec(
                    Name: "--theme",
                    Type: "string",
                    Description: "Target theme name for later import stages.",
                    Required: true),
                new PluginOptionSpec(
                    Name: "--dry-run",
                    Type: "flag",
                    Description: "Scan only without writing output."),
                new PluginOptionSpec(
                    Name: "--use",
                    Type: "flag",
                    Description: "Point the target site.yaml at the generated theme."),
                new PluginOptionSpec(
                    Name: "--verify",
                    Type: "flag",
                    Description: "Run light file-structure verification after import."),
                new PluginOptionSpec(
                    Name: "--strict",
                    Type: "string",
                    Description: "Treat import diagnostics as warnings or failures."),
                new PluginOptionSpec(
                    Name: "--force",
                    Type: "flag",
                    Description: "Overwrite an existing generated theme."),
                new PluginOptionSpec(
                    Name: "--route-map",
                    Type: "string",
                    Description: "Route map YAML file."),
                new PluginOptionSpec(
                    Name: "--site-path",
                    Type: "string",
                    Description: "Target site directory inside the project root."),
                new PluginOptionSpec(
                    Name: "--language",
                    Type: "string",
                    Description: "Generated site language code."),
                new PluginOptionSpec(
                    Name: "--content-source",
                    Type: "string",
                    Description: "Generated seed content source: markdown, json, yaml, or notion."),
                new PluginOptionSpec(
                    Name: "--build-source",
                    Type: "string",
                    Description: "Generated site build source: markdown or notion."),
                new PluginOptionSpec(
                    Name: "--no-extract-content",
                    Type: "flag",
                    Description: "Skip Markdown content extraction."),
                new PluginOptionSpec(
                    Name: "--no-seed",
                    Type: "flag",
                    Description: "Skip generated seed handoff files."),
                new PluginOptionSpec(
                    Name: "--no-report",
                    Type: "flag",
                    Description: "Skip import report generation.")
            ]);
}
